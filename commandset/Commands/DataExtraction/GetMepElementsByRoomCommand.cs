using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.DataExtraction;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetMepElementsByRoomCommand : ExternalEventCommandBase
    {
        private GetMepElementsByRoomEventHandler _handler => (GetMepElementsByRoomEventHandler)Handler;

        public override string CommandName => "get_mep_elements_by_room";

        public GetMepElementsByRoomCommand(UIApplication uiApp)
            : base(new GetMepElementsByRoomEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var roomIdStr = parameters?["room_id"]?.Value<string>();
                
                if (string.IsNullOrWhiteSpace(roomIdStr))
                {
                    throw new ArgumentException("room_id is required and must be a valid room identifier");
                }

                _handler.SetParameters(roomIdStr);

                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get MEP elements by room operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get MEP elements by room: {ex.Message}");
            }
        }
    }
}