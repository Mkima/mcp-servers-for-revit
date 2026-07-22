import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetAllElementsByRoomTool(server: McpServer) {
  server.tool(
    "get_all_elements_by_room",
    "Get all Revit elements in a specific room by its ElementId. Returns detailed information about each element including id, uniqueId, name, category, familyName, typeName, level, and XYZ location.",
    {
      room_id: z
        .number()
        .describe("The ElementId of the Revit room to query for all elements"),
      max_elements: z
        .number()
        .optional()
        .default(500)
        .describe("Maximum number of elements to return (default: 500)"),
    },
    async (args, extra) => {
      const params = {
        room_id: args.room_id,
        max_elements: args.max_elements ?? 500,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_all_elements_by_room", params);
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
              text: JSON.stringify({
                success: false,
                error: `Get all elements by room failed: ${error instanceof Error ? error.message : String(error)}`
              }, null, 2),
            },
          ],
        };
      }
    }
  );
}