import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerSwitchActiveViewTool(server: McpServer) {
  server.tool(
    "switch_active_view",
    "Switch the active Revit view by view id, unique id, or name.",
    {
      viewId: z
        .string()
        .optional()
        .describe("Revit view id as a string (for example, '12345')"),
      viewUniqueId: z
        .string()
        .optional()
        .describe("Revit view unique id"),
      viewName: z
        .string()
        .optional()
        .describe("Revit view name"),
    },
    async (args) => {
      try {
        const params = {
          viewId: args.viewId,
          viewUniqueId: args.viewUniqueId,
          viewName: args.viewName,
        };

        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("switch_active_view", params);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `switch active view failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
