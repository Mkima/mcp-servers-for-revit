using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;

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