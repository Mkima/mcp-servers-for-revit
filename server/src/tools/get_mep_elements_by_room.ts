import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetMepElementsByRoomTool(server: McpServer) {
  server.tool(
    "get_mep_elements_by_room",
    "Contextually scopes data retrieval to a specific space. Queries the spatial geometry of the requested room and isolates MEP (Mechanical, Electrical, Plumbing) elements located strictly inside its 3D boundaries.",
    {
      room_id: z
        .string()
        .describe("The unique identifier extracted from get_spatial_topology (e.g., 'Rm_101')"),
    },
    async (args, extra) => {
      const params = {
        room_id: args.room_id,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_mep_elements_by_room", params);
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
                error: `Get MEP elements by room failed: ${error instanceof Error ? error.message : String(error)}`
              }, null, 2),
            },
          ],
        };
      }
    }
  );
}