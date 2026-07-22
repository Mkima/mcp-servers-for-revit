using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Spatial
{
    public class SpatialTopologyEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        
        public Models.Spatial.SpatialTopologyResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }

        public void SetParameters()
        {
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                
                var projectInfo = doc.ProjectInformation;
                var projectName = projectInfo?.Name ?? "Unknown Project";

                var levelCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                var levels = new System.Collections.Generic.List<Models.Spatial.LevelInfo>();

                foreach (var level in levelCollector)
                {
                    var roomCollector = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .Where(r => r.Level?.Id == level.Id && r.Area > 0);

                    var rooms = roomCollector.Select(r => new Models.Spatial.RoomInfo
                    {
#if REVIT2024_OR_GREATER
                        RoomId = $"Rm_{r.Id.Value}",
#else
                        RoomId = $"Rm_{r.Id.IntegerValue}",
#endif
                        RoomName = r.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unnamed",
                        AreaSqM = r.Area * 0.092903
                    }).ToList();

                    levels.Add(new Models.Spatial.LevelInfo
                    {
#if REVIT2024_OR_GREATER
                        LevelId = $"Lvl_{level.Id.Value}",
#else
                        LevelId = $"Lvl_{level.Id.IntegerValue}",
#endif
                        LevelName = level.Name,
                        ElevationCm = level.Elevation * 30.48,
                        Rooms = rooms
                    });
                }

                ResultInfo = new Models.Spatial.SpatialTopologyResult
                {
                    ProjectName = projectName.Replace(" ", "_"),
                    Levels = levels,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new Models.Spatial.SpatialTopologyResult
                {
                    Success = false,
                    Message = $"Error getting spatial topology: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Spatial Topology";
    }
}