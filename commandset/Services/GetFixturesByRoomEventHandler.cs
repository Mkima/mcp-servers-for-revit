using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.MEP;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetFixturesByRoomEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _roomId;

        public GetFixturesByRoomResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(long roomId)
        {
            _roomId = roomId;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                var room = doc.GetElement(new ElementId(_roomId)) as Room;

                if (room == null)
                {
                    ResultInfo = new GetFixturesByRoomResult
                    {
                        Success = false,
                        Message = $"Room with ID {_roomId} not found"
                    };
                    return;
                }

                var roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unnamed Room";
                var boundingBox = room.get_BoundingBox(null);

                if (boundingBox == null)
                {
                    ResultInfo = new GetFixturesByRoomResult
                    {
                        RoomId = _roomId,
                        RoomName = roomName,
                        TotalFixtures = 0,
                        Fixtures = new List<FixtureInfo>(),
                        Success = true,
                        Message = "Room has no bounding box (may be unplaced)"
                    };
                    return;
                }

                var outline = new Outline(boundingBox.Min, boundingBox.Max);
                var filter = new BoundingBoxIntersectsFilter(outline);

                var plumbingFixtures = new FilteredElementCollector(doc)
                    .WherePasses(filter)
                    .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var fixtures = new List<FixtureInfo>();
                foreach (var fixture in plumbingFixtures)
                {
                    var location = fixture.Location;
                    double x = 0, y = 0, z = 0;

                    if (location is LocationPoint locPoint)
                    {
                        var point = locPoint.Point;
                        x = point.X;
                        y = point.Y;
                        z = point.Z;
                    }

                    fixtures.Add(new FixtureInfo
                    {
#if REVIT2024_OR_GREATER
                        Id = fixture.Id.Value,
#else
                        Id = fixture.Id.IntegerValue,
#endif
                        UniqueId = fixture.UniqueId,
                        Name = fixture.Name,
                        Category = fixture.Category?.Name ?? "Unknown",
                        FamilyName = fixture.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsString() ?? "",
                        TypeName = fixture.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsString() ?? "",
                        Level = fixture.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsValueString() ?? "",
                        LocationX = x,
                        LocationY = y,
                        LocationZ = z
                    });
                }

                ResultInfo = new GetFixturesByRoomResult
                {
#if REVIT2024_OR_GREATER
                    RoomId = room.Id.Value,
#else
                    RoomId = room.Id.IntegerValue,
#endif
                    RoomName = roomName,
                    TotalFixtures = fixtures.Count,
                    Fixtures = fixtures,
                    Success = true,
                    Message = $"Found {fixtures.Count} plumbing fixtures in room '{roomName}'"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetFixturesByRoomResult
                {
                    Success = false,
                    Message = $"Error getting fixtures by room: {ex.Message}"
                };
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Get Fixtures By Room";
        }
    }
}