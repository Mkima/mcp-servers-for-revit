using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class CalculatePairwiseDistancesByRoomCommand : ExternalEventCommandBase
    {
        private CalculatePairwiseDistancesByRoomEventHandler _handler => (CalculatePairwiseDistancesByRoomEventHandler)Handler;

        public override string CommandName => "calculate_pairwise_distances_by_room";

        public CalculatePairwiseDistancesByRoomCommand(UIApplication uiApp)
            : base(new CalculatePairwiseDistancesByRoomEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long roomId = parameters?["room_id"]?.Value<long>() ?? -1;
                int maxElements = parameters?["max_elements"]?.Value<int>() ?? 100;

                if (roomId == -1)
                {
                    throw new ArgumentException("room_id is required and must be a valid element ID");
                }

                _handler.SetParameters(roomId, maxElements);

                if (RaiseAndWaitForCompletion(120000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Calculate pairwise distances by room operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to calculate pairwise distances by room: {ex.Message}");
            }
        }
    }
}