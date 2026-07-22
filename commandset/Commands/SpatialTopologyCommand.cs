using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Spatial;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Spatial
{
    public class SpatialTopologyCommand : ExternalEventCommandBase
    {
        private SpatialTopologyEventHandler _handler => (SpatialTopologyEventHandler)Handler;

        public override string CommandName => "get_spatial_topology";

        public SpatialTopologyCommand(UIApplication uiApp)
            : base(new SpatialTopologyEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // No parameters needed for this command
                _handler.SetParameters();

                // Execute and wait
                if (RaiseAndWaitForCompletion(60000)) // 60 second timeout
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Get spatial topology operation timed out");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get spatial topology: {ex.Message}");
            }
        }
    }
}