import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetSpatialTopologyTool(server: McpServer) {
  server.tool(
    "get_spatial_topology",
    "Fetches a clean hierarchical dictionary of the active project boundaries, mapping out available levels and rooms to prevent token flooding. No input parameters - implicitly acts on currently open .rvt document.",
    {},
    async (args, extra) => {
      const response = await withRevitConnection(async (revitClient) => {
        return await revitClient.sendCommand('get_spatial_topology', args);
      });
      return { content: [{ type: "text", text: JSON.stringify(response) }] };
    }
  );
}