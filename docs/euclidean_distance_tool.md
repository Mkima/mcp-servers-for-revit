# MCP Tool: Calculate Euclidean Distance

## Overview
This implementation adds a new MCP tool that calculates the Euclidean distance between two Revit elements. The tool works across both the TypeScript/MCP layer and C#/Revit layer.

## Architecture

### Folder Structure
- `server/src/tools/calculate_euclidean_distance.ts` - TypeScript/MCP layer implementation
- `commandset/Commands/CalculateEuclideanDistanceCommand.cs` - C# command for Revit integration
- `commandset/Services/CalculateEuclideanDistanceEventHandler.cs` - C# event handler for distance calculation

## Implementation Details

### 1. TypeScript/MCP Layer (`calculate_euclidean_distance.ts`)
```typescript
import { z } from 'zod';
import { McpServer } from '../utils/McpServer';

export function registerCalculateEuclideanDistanceTool(server: McpServer) {
  server.tool(
    'calculate-euclidean-distance',
    'Calculates the Euclidean distance between two Revit elements.',
    z.object({
      element1_id: z.number().describe('The ElementId of the first element'),
      element2_id: z.number().describe('The ElementId of the second element')
    }),
    async ({ element1_id, element2_id }) => {
      const result = await server.withRevitConnection(async (client) => {
        try {
          const response = await client.send({
            jsonrpc: '2.0',
            id: 1,
            method: 'calculateEuclideanDistance',
            params: { 
              element1Id: element1_id, 
              element2Id: element2_id 
            }
          });
          
          return {
            content: `The Euclidean distance between elements ${element1_id} and ${element2_id} is ${response.result.distance.toFixed(2)} mm.`
          };
        } catch (error) {
          console.error('Error calculating Euclidean distance:', error);
          throw new Error(`Failed to calculate Euclidean distance: ${(error as Error).message}`);
        }
      });
      
      return result;
    }
  );
}
```

### 2. C# Command Layer (`CalculateEuclideanDistanceCommand.cs`)
```csharp
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class CalculateEuclideanDistanceCommand : ExternalEventCommandBase
    {
        private CalculateEuclideanDistanceEventHandler _handler => (CalculateEuclideanDistanceEventHandler)Handler;

        /// <summary>
        /// Command name for MCP protocol
        /// </summary>
        public override string CommandName => "calculate_euclidean_distance";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public CalculateEuclideanDistanceCommand(UIApplication uiApp)
            : base(new CalculateEuclideanDistanceEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parse parameters
                var data = parameters.ToObject<CalculateEuclideanDistanceParams>();

                if (data == null)
                    throw new ArgumentNullException(nameof(data), "Distance calculation data is null");

                // Set parameters and trigger event
                _handler.SetParameters(data);

                // Wait for completion with 10 second timeout
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Distance calculation operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to calculate Euclidean distance: {ex.Message}");
            }
        }
    }
}
```

### 3. C# Event Handler (`CalculateEuclideanDistanceEventHandler.cs`)
```csharp
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class CalculateEuclideanDistanceEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        /// <summary>
        /// Event synchronization object
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// Distance calculation parameters
        /// </summary>
        public CalculateEuclideanDistanceParams Parameters { get; private set; }

        /// <summary>
        /// Execution result
        /// </summary>
        public AIResult<CalculateEuclideanDistanceResult> Result { get; private set; }

        /// <summary>
        /// Set parameters for distance calculation
        /// </summary>
        public void SetParameters(CalculateEuclideanDistanceParams parameters)
        {
            Parameters = parameters;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                // Get elements from document using their IDs
                var element1 = doc.GetElement(new ElementId(Parameters.Element1Id));
                var element2 = doc.GetElement(new ElementId(Parameters.Element2Id));

                if (element1 == null)
                {
                    Result = new AIResult<CalculateEuclideanDistanceResult>
                    {
                        Success = false,
                        Message = $"First element with ID {Parameters.Element1Id} not found",
                        Response = null
                    };
                    return;
                }

                if (element2 == null)
                {
                    Result = new AIResult<CalculateEuclideanDistanceResult>
                    {
                        Success = false,
                        Message = $"Second element with ID {Parameters.Element2Id} not found",
                        Response = null
                    };
                    return;
                }

                // Get bounding box centers for 3D distance calculation
                var bbox1 = element1.get_BoundingBox(uiDoc.ActiveView);
                var bbox2 = element2.get_BoundingBox(uiDoc.ActiveView);

                if (bbox1 == null || bbox2 == null)
                {
                    Result = new AIResult<CalculateEuclideanDistanceResult>
                    {
                        Success = false,
                        Message = "One or both elements don't have valid bounding boxes",
                        Response = null
                    };
                    return;
                }

                // Calculate center points of bounding boxes
                var center1 = (bbox1.Min + bbox2.Max) / 2.0;
                var center2 = (bbox1.Min + bbox2.Max) / 2.0;

                // Calculate Euclidean distance in mm
                double dx = (center2.X - center1.X) * 304.8; // Convert from ft to mm
                double dy = (center2.Y - center1.Y) * 304.8;
                double dz = (center2.Z - center1.Z) * 304.8;

                double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                Result = new AIResult<CalculateEuclideanDistanceResult>
                {
                    Success = true,
                    Message = "Successfully calculated Euclidean distance",
                    Response = new CalculateEuclideanDistanceResult
                    {
                        Distance = distance
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<CalculateEuclideanDistanceResult>
                {
                    Success = false,
                    Message = $"Failed to calculate Euclidean distance: {ex.Message}",
                    Response = null
                };
            }
            finally
            {
                _resetEvent.Set(); // Signal completion
            }
        }

        /// <summary>
        /// Wait for calculation to complete
        /// </summary>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// Get handler name
        /// </summary>
        public string GetName()
        {
            return "Calculate Euclidean Distance";
        }
    }

    /// <summary>
    /// Parameters for distance calculation
    /// </summary>
    public class CalculateEuclideanDistanceParams
    {
        [Newtonsoft.Json.JsonProperty("element1Id")]
        public long Element1Id { get; set; }

        [Newtonsoft.Json.JsonProperty("element2Id")]
        public long Element2Id { get; set; }
    }

    /// <summary>
    /// Result of distance calculation
    /// </summary>
    public class CalculateEuclideanDistanceResult
    {
        [Newtonsoft.Json.JsonProperty("distance")]
        public double Distance { get; set; }
    }
}
```

## Usage

The tool can be invoked with the following parameters:
- `element1_id`: ID of first element (number)
- `element2_id`: ID of second element (number)

Example MCP request:
```json
{
  "method": "calculate-euclidean-distance",
  "params": {
    "element1_id": 1001,
    "element2_id": 1002
  }
}
```

## Files Created

1. `server/src/tools/calculate_euclidean_distance.ts` - TypeScript tool definition and registration
2. `commandset/Commands/CalculateEuclideanDistanceCommand.cs` - C# command wrapper
3. `commandset/Services/CalculateEuclideanDistanceEventHandler.cs` - C# event handler with distance calculation logic

## Integration Points

1. The TypeScript layer registers the tool automatically through the existing registration system
2. When invoked, it sends a JSON-RPC request to the C# plugin via socket connection 
3. The C# command handles the request and uses an event handler for safe Revit API access
4. Distance calculation is done using bounding box centers in 3D space with proper unit conversion (ft to mm)
5. Returns structured result containing distance value in millimeters

## Design Considerations

- Uses existing socket connection pattern established by other tools
- Follows same error handling and validation patterns as existing commands
- Handles Revit API safety through transactional approach and event-based execution
- Proper unit conversion from Revit's internal units (feet) to millimeters for user-friendly output