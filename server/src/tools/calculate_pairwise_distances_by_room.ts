import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCalculatePairwiseDistancesByRoomTool(server: McpServer) {
  server.tool(
    "calculate_pairwise_distances_by_room",
    "Get all elements in a room and calculate Euclidean distance between every pair. Returns a matrix of distances between all element pairs.",
    {
      room_id: z
        .number()
        .describe("The ElementId of the Revit room to analyze"),
      max_elements: z
        .number()
        .optional()
        .default(100)
        .describe("Maximum number of elements to process (default: 100, reduce if performance issues)"),
    },
    async (args, extra) => {
      const params = {
        room_id: args.room_id,
        max_elements: args.max_elements ?? 100,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("calculate_pairwise_distances_by_room", params);
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
                error: `Calculate pairwise distances by room failed: ${error instanceof Error ? error.message : String(error)}`
              }, null, 2),
            },
          ],
        };
      }
    }
  );
}