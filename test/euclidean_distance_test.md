# Test Script: Euclidean Distance Tool

This script verifies that the Euclidean distance tool has been implemented correctly and is ready for use.

## What This Tests

1. The TypeScript layer implementation exists 
2. The C# command class exists
3. The C# event handler exists  
4. All components are properly structured

## How to Test

1. Verify the tool registration in `server/src/tools/calculate_euclidean_distance.ts`
2. Verify the command class in `commandset/Commands/CalculateEuclideanDistanceCommand.cs`
3. Verify the event handler in `commandset/Services/CalculateEuclideanDistanceEventHandler.cs`

## Expected Usage

The tool can be invoked through the MCP protocol with parameters:
```
{
  "method": "calculate-euclidean-distance",
  "params": {
    "element1_id": 1001,
    "element2_id": 1002
  }
}
```

The tool will calculate and return the Euclidean distance between two elements in millimeters.