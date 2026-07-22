import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerColorElementsTool(server: McpServer) {
  server.tool(
      "color_elements",
      "Color elements in the current view based on a category and parameter value. Each unique parameter value gets assigned a distinct color.",
      {
        categoryName: z
            .string()
            .describe("The name of the Revit category to color (e.g., 'Walls', 'Doors', 'Rooms')"),
        parameterName: z
            .string()
            .describe("The name of the parameter to use for grouping and coloring elements"),
        useGradient: z
            .boolean()
            .optional()
            .default(false)
            .describe("Whether to use a gradient color scheme instead of random colors"),
        customColors: z
            .array(
                z.object({
                  r: z.number().int().min(0).max(255),
                  g: z.number().int().min(0).max(255),
                  b: z.number().int().min(0).max(255),
                })
            )
            .optional()
            .describe("Optional array of custom RGB colors to use for specific parameter values"),
      },
      async (args, extra) => {
        const params = args;
        try {
          const response = await withRevitConnection(async (revitClient) => {
            return await revitClient.sendCommand("color_splash", params);
          });

          // Format the response into a JSON-formatted output
          if (response.success) {
            const coloredGroups = response.results || [];

            return {
              content: [
                {
                  type: "text",
                  text: JSON.stringify({
                    success: true,
                    message: `Successfully colored ${response.totalElements} elements across ${response.coloredGroups} groups`,
                    results: coloredGroups
                  }, null, 2),
                },
              ],
            };
          } else {
            return {
              content: [
                {
                  type: "text",
                  text: JSON.stringify({
                    success: false,
                    error: `Color operation failed: ${response.message}`
                  }, null, 2),
                },
              ],
            };
          }
        } catch (error) {
          return {
            content: [
              {
                type: "text",
                text: JSON.stringify({
                  success: false,
                  error: `Color operation failed: ${error instanceof Error ? error.message : String(error)}`
                }, null, 2),
              },
            ],
          };
        }
      }
  );
}