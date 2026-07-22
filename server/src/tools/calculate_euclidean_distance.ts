import { z } from 'zod';
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerCalculateEuclideanDistanceTool(server: McpServer) {
  server.tool(
    "calculate_euclidean_distance",
    "Calculates the Euclidean distance between two Revit elements.",
{
      element1_id: z.number().describe("The ElementId of the first element"),
      element2_id: z.number().describe("The ElementId of the second element")
    },
    async (args, extra) => {
      const { element1_id, element2_id } = args;
      
      try {
        const response = await withRevitConnection(async (revitClient) => {
          // Call C# function to get element locations and calculate distance
          // Using sendCommand as in other working tools
          const result = await revitClient.sendCommand('calculateEuclideanDistance', {
            element1Id: element1_id, 
            element2Id: element2_id 
          });
          
          return {
            content: [
              {
                type: "text" as const,
                text: JSON.stringify({
                  success: true,
                  distance: result.distance,
                  formatted: `The Euclidean distance between elements ${element1_id} and ${element2_id} is ${result.distance.toFixed(2)} mm.`
                }, null, 2)
              }
            ]
          };
        });

        return response;
      } catch (error) {
        return {
          content: [
            {
              type: "text" as const,
              text: JSON.stringify({
                success: false,
                error: `Failed to calculate Euclidean distance: ${error instanceof Error ? error.message : String(error)}`
              }, null, 2),
            },
          ],
        };
      }
    }
  );
}
