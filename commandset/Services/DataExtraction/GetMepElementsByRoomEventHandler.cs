using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetMepElementsByRoomEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private string _roomIdStr;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public GetMepElementsByRoomResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }

        public void SetParameters(string roomIdStr)
        {
            _roomIdStr = roomIdStr;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        private long ParseRoomId(string roomIdStr)
        {
            if (roomIdStr.StartsWith("Rm_"))
            {
                var idPart = roomIdStr.Substring(3);
                if (long.TryParse(idPart, out long id))
                    return id;
            }
            
            if (long.TryParse(roomIdStr, out long directId))
                return directId;

            throw new ArgumentException($"Invalid room ID format: {roomIdStr}");
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                
                long roomIdLong = ParseRoomId(_roomIdStr);
                var room = doc.GetElement(new ElementId(roomIdLong)) as Room;

                if (room == null)
                {
                    ResultInfo = new GetMepElementsByRoomResult
                    {
                        Success = false,
                        Message = $"Room with ID {_roomIdStr} not found"
                    };
                    return;
                }

                var boundingBox = room.get_BoundingBox(null);

                if (boundingBox == null)
                {
                    ResultInfo = new GetMepElementsByRoomResult
                    {
                        RoomId = _roomIdStr,
                        Elements = new System.Collections.Generic.List<MepElementInfo>(),
                        Success = true,
                        Message = "Room has no bounding box (may be unplaced)"
                    };
                    return;
                }

                var outline = new Outline(boundingBox.Min, boundingBox.Max);
                var filter = new BoundingBoxIntersectsFilter(outline);

                var mepCategories = new System.Collections.Generic.List<BuiltInCategory>
                {
                    BuiltInCategory.OST_ElectricalFixtures,
                    BuiltInCategory.OST_PlumbingFixtures,
                    BuiltInCategory.OST_DuctCurves,
                    BuiltInCategory.OST_PipeCurves,
                    BuiltInCategory.OST_ElectricalEquipment,
                    BuiltInCategory.OST_MechanicalEquipment,
                    BuiltInCategory.OST_LightingFixtures,
                    BuiltInCategory.OST_LightingDevices
                };

                var categoryFilter = new ElementMulticategoryFilter(mepCategories);

                var allElements = new FilteredElementCollector(doc)
                    .WherePasses(filter)
                    .WherePasses(categoryFilter)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var elements = new System.Collections.Generic.List<MepElementInfo>();

                foreach (var element in allElements)
                {
                    var location = element.Location;
                    double x = 0, y = 0, z = 0;

                    if (location is LocationPoint locPoint)
                    {
                        var point = locPoint.Point;
                        x = point.X * 30.48;
                        y = point.Y * 30.48;
                        z = point.Z * 30.48;
                    }
                    else if (location is LocationCurve locCurve)
                    {
                        var curve = locCurve.Curve;
                        if (curve != null)
                        {
                            var center = curve.Evaluate(0.5, true);
                            x = center.X * 30.48;
                            y = center.Y * 30.48;
                            z = center.Z * 30.48;
                        }
                    }

                    var categoryName = element.Category?.Name ?? "";
                    bool isWaterproof = categoryName.Contains("Water") || categoryName.Contains("Plumbing");

                    elements.Add(new MepElementInfo
                    {
                        Guid = element.UniqueId,
                        Category = element.Category?.Name ?? "Unknown",
                        FamilyName = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsString() ?? "",
                        TypeName = element.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsString() ?? "",
                        IsWaterproof = isWaterproof,
                        CoordinatesCm = new CoordinatesCm { X = Math.Round(x, 1), Y = Math.Round(y, 1), Z = Math.Round(z, 1) }
                    });
                }

                ResultInfo = new GetMepElementsByRoomResult
                {
                    RoomId = _roomIdStr,
                    Elements = elements,
                    Success = true,
                    Message = $"Found {elements.Count} MEP elements in room"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new GetMepElementsByRoomResult
                {
                    Success = false,
                    Message = $"Error getting MEP elements by room: {ex.Message}"
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
            return "Get MEP Elements By Room";
        }
    }
}