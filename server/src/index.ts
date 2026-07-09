#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { appendFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { registerTools } from "./tools/register.js";

const serverRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const logDir = path.join(serverRoot, "logs");
const logFile = path.join(logDir, `server-${new Date().toISOString().replace(/[:.]/g, "-")}.log`);

async function appendLog(message: string) {
  try {
    await mkdir(logDir, { recursive: true });
    await appendFile(logFile, `${new Date().toISOString()} ${message}\n`, "utf8");
  } catch {
    // Ignore logging failures so the server can continue running.
  }
}

function formatLogArgs(args: unknown[]) {
  return args
    .map((arg) => {
      if (typeof arg === "string") {
        return arg;
      }

      try {
        return JSON.stringify(arg);
      } catch {
        return String(arg);
      }
    })
    .join(" ");
}

const originalConsoleError = console.error.bind(console);
const originalConsoleLog = console.log.bind(console);

console.error = (...args: unknown[]) => {
  void appendLog(`[stderr] ${formatLogArgs(args)}`);
  originalConsoleError(...args);
};

console.log = (...args: unknown[]) => {
  void appendLog(`[stdout] ${formatLogArgs(args)}`);
  originalConsoleLog(...args);
};

// 创建服务器实例
const server = new McpServer({
  name: "mcp-server-for-revit",
  version: "1.0.0",
});

// 启动服务器
async function main() {
  await appendLog("Starting Revit MCP server");

  // 注册工具
  await registerTools(server);

  // 连接到传输层
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Revit MCP Server start success");
}

main().catch(async (error) => {
  const errorMessage = error instanceof Error ? error.stack ?? error.message : String(error);
  await appendLog(`Error starting Revit MCP Server: ${errorMessage}`);
  console.error("Error starting Revit MCP Server:", error);
  process.exit(1);
});
