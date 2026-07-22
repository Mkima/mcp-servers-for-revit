using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.DataExtraction;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class CalculatePairwiseDistancesByRoomEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private long _roomId;
        private int _maxElements = 100;

        public PairwiseDistanceResult ResultInfo { get; private set; }
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public void SetParameters(long roomId, int maxElements = 100)
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

        private XYZ GetElementCenter(Element element, UIDocument uiDoc)
        {
            var bbox = element.get_BoundingBox(uiDoc.ActiveView);
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) / 2.0;
            }

            var location = element.Location;
            if (location is LocationPoint locPoint)
            {
                return locPoint.Point;
            }
            else if (location is LocationCurve locCurve)
            {
                var curve = locCurve.Curve;
                if (curve != null)
                {
                    return curve.Evaluate(0.5, true);
                }
            }

            return XYZ.Zero;
        }

        private double CalculateDistanceMm(XYZ p1, XYZ p2)
        {
            double dx = (p2.X - p1.X) * 304.8;
            double dy = (p2.Y - p1.Y) * 304.8;
            double dz = (p2.Z - p1.Z) * 304.8;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;

                var room = doc.GetElement(new ElementId(_roomId)) as Room;

                if (room == null)
                {
                    ResultInfo = new PairwiseDistanceResult
                    {
                        Success = false,
                        Message = $"Room with ID {_roomId} not found"
                    };
                    return;
                }

                var boundingBox = room.get_BoundingBox(null);

                if (boundingBox == null)
                {
                    ResultInfo = new PairwiseDistanceResult
                    {
                        RoomId = _roomId,
                        RoomName = room.Name,
                        TotalElements = 0,
                        ElementCount = 0,
                        PairCount = 0,
                        Distances = new List<ElementPairDistance>(),
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

                var distances = new List<ElementPairDistance>();
                
                for (int i = 0; i < elements.Count; i++)
                {
                    for (int j = i + 1; j < elements.Count; j++)
                    {
                        var elem1 = doc.GetElement(new ElementId(elements[i].Id));
                        var elem2 = doc.GetElement(new ElementId(elements[j].Id));

                        if (elem1 != null && elem2 != null)
                        {
                            var center1 = GetElementCenter(elem1, uiDoc);
                            var center2 = GetElementCenter(elem2, uiDoc);
                            var distanceMm = CalculateDistanceMm(center1, center2);

                            distances.Add(new ElementPairDistance
                            {
                                Element1Id = elements[i].Id,
                                Element1Name = elements[i].Name,
                                Element1Category = elements[i].Category,
                                Element2Id = elements[j].Id,
                                Element2Name = elements[j].Name,
                                Element2Category = elements[j].Category,
                                DistanceMm = distanceMm
                            });
                        }
                    }
                }

                ResultInfo = new PairwiseDistanceResult
                {
#if REVIT2024_OR_GREATER
                    RoomId = room.Id.Value,
#else
                    RoomId = room.Id.IntegerValue,
#endif
                    RoomName = room.Name,
                    TotalElements = allElements.Count,
                    ElementCount = elements.Count,
                    PairCount = distances.Count,
                    Distances = distances,
                    Success = true,
                    Message = $"Calculated {distances.Count} pairwise distances among {elements.Count} elements in room '{room.Name}'"
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new PairwiseDistanceResult
                {
                    Success = false,
                    Message = $"Error calculating pairwise distances by room: {ex.Message}"
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
            return "Calculate Pairwise Distances By Room";
        }
    }
}