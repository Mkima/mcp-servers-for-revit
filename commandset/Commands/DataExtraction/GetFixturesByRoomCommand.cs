using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.DataExtraction
{
    public class GetFixturesByRoomCommand : ExternalEventCommandBase
    {
        private GetFixturesByRoomEventHandler _handler => (GetFixturesByRoomEventHandler)Handler;

        public override string CommandName => "get_fixtures_by_room";

        public GetFixturesByRoomCommand(UIApplication uiApp)
            : base(new GetFixturesByRoomEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long roomId = parameters?["room_id"]?.Value<long>() ?? -1;

                if (roomId == -1)
                {
                    throw new ArgumentException("room_id is required and must be a valid element ID");
                }

                _handler.SetParameters(roomId);

                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get fixtures by room operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get fixtures by room: {ex.Message}");
            }
        }
    }
}