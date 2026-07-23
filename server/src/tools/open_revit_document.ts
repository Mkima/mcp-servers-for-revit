import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerOpenRevitDocumentTool(server: McpServer) {
  server.tool(
    "open_revit_document",
    "Open and activate a Revit document. The document path should be the full local Windows path to the .rvt file on the shared folder mounted from the Mac.",
    {
      documentPath: z
        .string()
        .describe("Full path to the Revit project file (.rvt) on the shared folder (e.g., 'C:/Shared/revit/projects/myproject.rvt')"),
    },
    async (args) => {
      try {
        const params = {
          documentPath: args.documentPath,
        };

        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("open_revit_document", params);
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
                error: `open_revit_document failed: ${error instanceof Error ? error.message : String(error)}`
              }, null, 2),
            },
          ],
        };
      }
    }
  );
}