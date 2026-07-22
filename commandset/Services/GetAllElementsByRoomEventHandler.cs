using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetAllElementsByRoomEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _roomId;
        private int _maxElements = 500;

        public GetAllElementsByRoomResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(long roomId, int maxElements = 500)
        {
            _roomId = roomId;
            _maxElements = maxElements;
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
                    ResultInfo = new GetAllElementsByRoomResult
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
                    ResultInfo = new GetAllElementsByRoomResult
                    {
                        RoomId = _roomId,
                        RoomName = roomName,
                        TotalElements = 0,
                        Elements = new List<ElementInfo>(),
                        Success = true,
                        Message = "Room has no bounding box (may be unplaced)"
                    };
                    return;
                }

                var outline = new Outline(boundingBox.Min, boundingBox.Max);
                var filter = new BoundingBoxIntersectsFilter(outline);

                var allElements = new FilteredElementCollector(doc)
                    .WherePasses(filter)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var elements = new List<ElementInfo>();
                foreach (var element in allElements)
                {
                    if (elements.Count >= _maxElements) break;

                    var location = element.Location;
                    double x = 0, y = 0, z = 0;

                    if (location is LocationPoint locPoint)
                    {
                        var point = locPoint.Point;
                        x = point.X;
                        y = point.Y;
                        z = point.Z;
                    }
                    else if (location is LocationCurve locCurve)
                    {
                        var curve = locCurve.Curve;
                        if (curve != null)
                        {
                            var center = curve.Evaluate(0.5, true);
                            x = center.X;
                            y = center.Y;
                            z = center.Z;
                        }
                    }

                    elements.Add(new ElementInfo
                    {
#if REVIT2024_OR_GREATER
                        Id = element.Id.Value,
#else
                        Id = element.Id.IntegerValue,
#endif
                        UniqueId = element.UniqueId,
                        Name = element.Name,
                        Category = element.Category?.Name ?? "Unknown",
                        FamilyName = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsString() ?? "",
                        TypeName = element.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsString() ?? "",
                        Level = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsValueString() ?? "",
                        LocationX = x,
                        LocationY = y,
                        LocationZ = z
                    });
                }

                ResultInfo = new GetAllElementsByRoomResult
                {
#if REVIT2024_OR_GREATER
                    RoomId = room.Id.Value,
#else
                    RoomId = room.Id.IntegerValue,
#endif
                    RoomName = roomName,
                    TotalElements = elements.Count,
                    Elements = elements,
                    Success = true,
                    Message = $"Found {elements.Count} elements in room '{roomName}'"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetAllElementsByRoomResult
                {
                    Success = false,
                    Message = $"Error getting all elements by room: {ex.Message}"
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
            return "Get All Elements By Room";
        }
    }
}