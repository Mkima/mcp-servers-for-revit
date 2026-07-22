using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetAllElementsByRoomCommand : ExternalEventCommandBase
    {
        private GetAllElementsByRoomEventHandler _handler => (GetAllElementsByRoomEventHandler)Handler;

        public override string CommandName => "get_all_elements_by_room";

        public GetAllElementsByRoomCommand(UIApplication uiApp)
            : base(new GetAllElementsByRoomEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long roomId = parameters?["room_id"]?.Value<long>() ?? -1;
                int maxElements = parameters?["max_elements"]?.Value<int>() ?? 500;

                if (roomId == -1)
                {
                    throw new ArgumentException("room_id is required and must be a valid element ID");
                }

                _handler.SetParameters(roomId, maxElements);

                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get all elements by room operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get all elements by room: {ex.Message}");
            }
        }
    }
}