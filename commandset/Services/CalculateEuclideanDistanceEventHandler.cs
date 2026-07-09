using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Common;

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
                var center1 = (bbox1.Min + bbox1.Max) / 2.0;
                var center2 = (bbox2.Min + bbox2.Max) / 2.0;

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