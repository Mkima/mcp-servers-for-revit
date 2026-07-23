#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { createMcpExpressApp } from "@modelcontextprotocol/sdk/server/express.js";
import type { Request, Response } from "express";
import * as crypto from "node:crypto";
import { appendFile, mkdir } from "node:fs/promises";
import path from "path";
import { fileURLToPath } from "node:url";
import { registerTools } from "./tools/register.js";

const serverRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const logDir = path.join(serverRoot, "logs");
const logFile = path.join(logDir, `server-http-${new Date().toISOString().replace(/[:.]/g, "-")}.log`);

async function appendLog(message: string) {
  try {
    await mkdir(logDir, { recursive: true });
    await appendFile(logFile, `${new Date().toISOString()} ${message}\n`, "utf8");
  } catch {
    // Ignore logging failures so the server can continue running.
  }
}

const originalConsoleError = console.error.bind(console);

console.error = (...args: unknown[]) => {
  void appendLog(`[stderr] ${args.map(a => typeof a === 'string' ? a : JSON.stringify(a)).join(' ')}`);
  originalConsoleError(...args);
};

const PORT = process.env.MCP_PORT ? parseInt(process.env.MCP_PORT, 10) : 3001;

const server = new McpServer({
  name: "mcp-server-for-revit",
  version: "1.0.0",
});

const app = createMcpExpressApp();
const transports: Record<string, StreamableHTTPServerTransport> = {};

app.post("/mcp", async (req: Request, res: Response) => {
  try {
    const sessionId = req.headers["mcp-session-id"] as string;
    let transport: StreamableHTTPServerTransport;

    if (sessionId && transports[sessionId]) {
      transport = transports[sessionId];
    }
    else if (!sessionId && req.body?.method === "initialize") {
      transport = new StreamableHTTPServerTransport({
        sessionIdGenerator: () => crypto.randomUUID(),
        onsessioninitialized: (sid) => {
          console.error(`Session initialized: ${sid}`);
          transports[sid] = transport;
        }
      });

      await server.connect(transport);
      await transport.handleRequest(req, res, req.body);
      return;
    }
    else {
      res.status(400).json({
        jsonrpc: "2.0",
        error: { code: -32000, message: "Bad Request" },
        id: null
      });
      return;
    }

    await transport.handleRequest(req, res, req.body);
  } catch (error) {
    console.error("MCP request error:", error);
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: "2.0",
        error: { code: -32603, message: "Internal server error" },
        id: null
      });
    }
  }
});

app.get("/mcp", async (req: Request, res: Response) => {
  const sessionId = req.headers["mcp-session-id"] as string;
  if (!sessionId || !transports[sessionId]) {
    res.status(400).send("Invalid or missing session ID");
    return;
  }
  await transports[sessionId].handleRequest(req, res);
});

app.delete("/mcp", async (req: Request, res: Response) => {
  const sessionId = req.headers["mcp-session-id"] as string;
  if (!sessionId || !transports[sessionId]) {
    res.status(400).send("Invalid or missing session ID");
    return;
  }
  await transports[sessionId].handleRequest(req, res);
});

async function main() {
  await appendLog(`Starting Revit MCP HTTP server on port ${PORT}`);
  await registerTools(server);

  app.listen(PORT, () => {
    console.error(`Revit MCP HTTP Server listening on port ${PORT}`);
  });
}

main().catch(async (error) => {
  const errorMessage = error instanceof Error ? error.stack ?? error.message : String(error);
  await appendLog(`Error: ${errorMessage}`);
  console.error("Server error:", error);
  process.exit(1);
});