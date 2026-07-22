import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetFixturesByRoomTool(server: McpServer) {
  server.tool(
    "get_fixtures_by_room",
    "Get all plumbing fixtures (e.g., sinks, toilets, water heaters) associated with a specific room in Revit. Returns detailed information about each fixture including location, family, type, and level.",
    {
      room_id: z
        .number()
        .describe("The ElementId of the Revit room to query for fixtures"),
    },
    async (args, extra) => {
      const params = {
        room_id: args.room_id,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_fixtures_by_room", params);
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
                error: `Get fixtures by room failed: ${error instanceof Error ? error.message : String(error)}`
              }, null, 2),
            },
          ],
        };
      }
    }
  );
}