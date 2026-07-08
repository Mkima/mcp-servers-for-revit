using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RevitMCPCommandSet.Services
{
    public class AIElementFilterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;
        private static readonly object _traceLock = new object();
        private static TextWriterTraceListener _fileTraceListener;
        private static string _traceLogPath;
        /// <summary>
        /// Event wait handle
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        /// <summary>
        /// Input data for the event
        /// </summary>
        public FilterSetting FilterSetting { get; private set; }
        /// <summary>
        /// Output result from the event
        /// </summary>
        public AIResult<List<object>> Result { get; private set; }

        public AIElementFilterEventHandler()
        {
            WriteTrace("AIElementFilter handler initialized");
            WriteRuntimeLog("AIElementFilter handler initialized");
        }

        /// <summary>
        /// Set the input parameters
        /// </summary>
        public void SetParameters(FilterSetting data)
        {
            FilterSetting = data;
            _resetEvent.Reset();
            WriteTrace($"Filter settings received for {data?.FilterElementType ?? "unknown"}");
            WriteRuntimeLog($"Filter settings received for {data?.FilterElementType ?? "unknown"}");
        }

        public static void WriteRuntimeLog(string message)
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "revit_ai_element_filter.log"),
                    Path.Combine(Path.GetTempPath(), "revit_ai_element_filter.log"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autodesk", "Revit", "Autodesk Revit 2024", "Journals", "revit_ai_element_filter.log")
                };

                foreach (var logPath in candidates)
                {
                    try
                    {
                        var directory = Path.GetDirectoryName(logPath);
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [AIElementFilter] {message}{Environment.NewLine}");
                        break;
                    }
                    catch
                    {
                        // Try the next location.
                    }
                }
            }
            catch
            {
                // Best-effort logging; do not break the plugin.
            }
        }

        private static void WriteTrace(string message)
        {
            try
            {
                lock (_traceLock)
                {
                    if (_fileTraceListener == null)
                    {
                        var candidateDirectories = new[]
                        {
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autodesk", "Revit", "Autodesk Revit 2024", "Journals"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "Revit", "Addins", "2024", "revit_mcp_plugin", "Commands", "RevitMCPCommandSet"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RevitAIElementFilterLogs")
                        };

                        foreach (var directory in candidateDirectories)
                        {
                            try
                            {
                                Directory.CreateDirectory(directory);
                                var logPath = Path.Combine(directory, "ai_element_filter.log");
                                _traceLogPath = logPath;
                                _fileTraceListener = new TextWriterTraceListener(logPath);
                                Trace.Listeners.Add(_fileTraceListener);
                                Trace.AutoFlush = true;
                                break;
                            }
                            catch
                            {
                                // Try the next location.
                            }
                        }

                        if (_fileTraceListener == null)
                        {
                            var fallbackPath = Path.Combine(Path.GetTempPath(), "ai_element_filter.log");
                            _traceLogPath = fallbackPath;
                            _fileTraceListener = new TextWriterTraceListener(fallbackPath);
                            Trace.Listeners.Add(_fileTraceListener);
                            Trace.AutoFlush = true;
                        }
                    }
                }

                Trace.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [AIElementFilter] {message}");
            }
            catch
            {
                // Best-effort logging; do not break the plugin.
            }
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementInfoList = new List<object>();
                WriteRuntimeLog("Execute started");
                // Validate whether the filter settings are valid
                if (!FilterSetting.Validate(out string errorMessage))
                    throw new Exception(errorMessage);
                // Get the IDs of elements matching the specified conditions
                var elementList = GetFilteredElements(doc, FilterSetting);
                if (elementList == null || !elementList.Any())
                    throw new Exception("No matching elements were found in the project. Please check whether the filter settings are correct.");
                // Enforce the maximum number of elements
                string message = "";
                if (FilterSetting.MaxElements > 0)
                {
                    if (elementList.Count > FilterSetting.MaxElements)
                    {
                        elementList = elementList.Take(FilterSetting.MaxElements).ToList();
                        message = $" In addition, {elementList.Count} elements match the filter criteria and only the first {FilterSetting.MaxElements} are shown.";
                    }
                }

                // Get information for the matching elements
                elementInfoList = GetElementFullInfo(doc, elementList);

                Result = new AIResult<List<object>>
                {
                    Success = true,
                    Message = $"Successfully retrieved information for {elementInfoList.Count} elements. The details are stored in the Response property." + message,
                    Response = elementInfoList,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<object>>
                {
                    Success = false,
                    Message = $"An error occurred while retrieving element information: {ex.Message}",
                };
            }
            finally
            {
                _resetEvent.Set(); // Notify the waiting thread that the operation is complete
            }
        }

        /// <summary>
        /// Wait for the operation to complete
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds</param>
        /// <returns>Whether the operation completed before the timeout</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// Implementation of IExternalEventHandler.GetName
        /// </summary>
        public string GetName()
        {
            return "Get element information";
        }

        /// <summary>
        /// Get elements from the Revit document that match the filter settings, supporting combined filters.
        /// </summary>
        /// <param name="doc">The Revit document</param>
        /// <param name="settings">The filter settings</param>
        /// <returns>A collection of elements that satisfy all filter conditions</returns>
        public static IList<Element> GetFilteredElements(Document doc, FilterSetting settings)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            // Validate the filter settings
            if (!settings.Validate(out string errorMessage))
            {
                System.Diagnostics.Trace.WriteLine($"Filter settings are invalid: {errorMessage}");
                return new List<Element>();
            }
            // Track which filters were applied
            List<string> appliedFilters = new List<string>();
            List<Element> result = new List<Element>();

            // Collect elements from the host document first
            WriteTrace($"Starting host document scan for {doc?.Title ?? "unknown"}");
            if (settings.IncludeTypes)
            {
                var hostTypeResults = GetElementsByKind(doc, settings, true, appliedFilters, true);
                WriteTrace($"Host document type results: {hostTypeResults.Count}");
                result.AddRange(hostTypeResults);
            }
            if (settings.IncludeInstances)
            {
                var hostInstanceResults = GetElementsByKind(doc, settings, false, appliedFilters, true);
                WriteTrace($"Host document instance results: {hostInstanceResults.Count}");
                result.AddRange(hostInstanceResults);
            }

            // Also collect elements from linked documents so filters can discover MEP/plumbing and other linked models
            try
            {
                var linkedInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                WriteTrace($"Found {linkedInstances.Count} linked instance(s)");

                foreach (RevitLinkInstance linkInstance in linkedInstances)
                {
                    Document linkDoc = linkInstance.GetLinkDocument();
                    if (linkDoc == null)
                    {
                        WriteTrace($"Linked instance '{linkInstance.Name}' has no accessible linked document");
                        continue;
                    }

                    string linkName = !string.IsNullOrWhiteSpace(linkInstance.Name)
                        ? linkInstance.Name
                        : linkDoc.Title;

                    appliedFilters.Add($"Linked document: {linkName}");
                    WriteTrace($"Scanning linked document '{linkName}' ({linkDoc.Title})");

                    if (settings.IncludeTypes)
                    {
                        var linkedTypeResults = GetElementsByKind(linkDoc, settings, true, appliedFilters, false);
                        WriteTrace($"Linked document type results for '{linkName}': {linkedTypeResults.Count}");
                        result.AddRange(linkedTypeResults);
                    }
                    if (settings.IncludeInstances)
                    {
                        var linkedInstanceResults = GetElementsByKind(linkDoc, settings, false, appliedFilters, false);
                        WriteTrace($"Linked document instance results for '{linkName}': {linkedInstanceResults.Count}");
                        result.AddRange(linkedInstanceResults);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteTrace($"Unable to enumerate linked documents: {ex.Message}");
            }

            // Output information about the applied filters
            if (appliedFilters.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine($"Applied {appliedFilters.Count} filter conditions: {string.Join(", ", appliedFilters)}");
                System.Diagnostics.Trace.WriteLine($"Final filter result: found {result.Count} elements");
            }
            return result;

        }

        /// <summary>
        /// Get elements matching the filter conditions based on element kind (type or instance)
        /// </summary>
        private static List<Element> GetElementsByKind(Document doc, FilterSetting settings, bool isElementType, List<string> appliedFilters, bool useCurrentViewContext)
        {
            // Create the base FilteredElementCollector
            FilteredElementCollector collector;
            // Check whether the current view's visible elements should be filtered (instances only)
            if (!isElementType && useCurrentViewContext && settings.FilterVisibleInCurrentView && doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                appliedFilters.Add("Visible in current view");
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }
            // Filter by element kind
            if (isElementType)
            {
                collector = collector.WhereElementIsElementType();
                appliedFilters.Add("Element types only");
            }
            else
            {
                collector = collector.WhereElementIsNotElementType();
                appliedFilters.Add("Element instances only");
            }
            // Create the filter list
            List<ElementFilter> filters = new List<ElementFilter>();
            // 1. Category filter
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                BuiltInCategory category;
                if (!Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    throw new ArgumentException($"Cannot convert '{settings.FilterCategory}' to a valid Revit category.");
                }
                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(category);
                filters.Add(categoryFilter);
                appliedFilters.Add($"Category: {settings.FilterCategory}");
            }
            // 2. Element class filter
            if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
            {

                Type elementType = null;
                // Try parsing the type name in various possible forms
                string[] possibleTypeNames = new string[]
                {
                    settings.FilterElementType,                                    // Original input
                    $"Autodesk.Revit.DB.{settings.FilterElementType}, RevitAPI",  // Revit API namespace
                    $"{settings.FilterElementType}, RevitAPI"                      // Fully qualified with assembly
                };
                foreach (string typeName in possibleTypeNames)
                {
                    elementType = Type.GetType(typeName);
                    if (elementType != null)
                        break;
                }
                if (elementType != null)
                {
                    ElementClassFilter classFilter = new ElementClassFilter(elementType);
                    filters.Add(classFilter);
                    appliedFilters.Add($"Element type: {elementType.Name}");
                }
                else
                {
                    throw new Exception($"Warning: Unable to find type '{settings.FilterElementType}'");
                }
            }
            // 3. Family symbol filter (instance elements only)
            if (!isElementType && settings.FilterFamilySymbolId > 0)
            {
                ElementId symbolId = new ElementId((long)settings.FilterFamilySymbolId);
                // Check whether the element exists and is a family symbol
                Element symbolElement = doc.GetElement(symbolId);
                if (symbolElement != null && symbolElement is FamilySymbol)
                {
                    FamilyInstanceFilter familyFilter = new FamilyInstanceFilter(doc, symbolId);
                    filters.Add(familyFilter);
                    // Add more detailed family information to the log
                    FamilySymbol symbol = symbolElement as FamilySymbol;
                    string familyName = symbol.Family?.Name ?? "Unknown family";
                    string symbolName = symbol.Name ?? "Unknown type";
                    appliedFilters.Add($"Family symbol: {familyName} - {symbolName} (ID: {settings.FilterFamilySymbolId})");
                }
                else
                {
                    string elementType = symbolElement != null ? symbolElement.GetType().Name : "does not exist";
                    System.Diagnostics.Trace.WriteLine($"Warning: Element with ID {settings.FilterFamilySymbolId} {(symbolElement == null ? "does not exist" : "is not a valid FamilySymbol")} (actual type: {elementType})");
                }
            }
            // 4. Bounding box filter
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                // Convert to Revit XYZ coordinates (millimeters to internal units)
                XYZ minXYZ = JZPoint.ToXYZ(settings.BoundingBoxMin);
                XYZ maxXYZ = JZPoint.ToXYZ(settings.BoundingBoxMax);
                // Create the bounding box Outline object
                Outline outline = new Outline(minXYZ, maxXYZ);
                // Create an intersection filter
                BoundingBoxIntersectsFilter boundingBoxFilter = new BoundingBoxIntersectsFilter(outline);
                filters.Add(boundingBoxFilter);
                appliedFilters.Add($"Bounding box filter: Min({settings.BoundingBoxMin.X:F2}, {settings.BoundingBoxMin.Y:F2}, {settings.BoundingBoxMin.Z:F2}), " +
                                  $"Max({settings.BoundingBoxMax.X:F2}, {settings.BoundingBoxMax.Y:F2}, {settings.BoundingBoxMax.Z:F2}) mm");
            }
            // Apply the combined filter
            if (filters.Count > 0)
            {
                ElementFilter combinedFilter = filters.Count == 1
                    ? filters[0]
                    : new LogicalAndFilter(filters);
                collector = collector.WherePasses(combinedFilter);
                if (filters.Count > 1)
                {
                    WriteTrace($"Applied a combined filter with {filters.Count} conditions (logical AND)");
                }
            }

            var matchedElements = collector.ToElements().ToList();
            WriteTrace($"Document '{doc?.Title ?? "unknown"}' matched {matchedElements.Count} element(s) for {(isElementType ? "types" : "instances")}");
            return matchedElements;
        }

        /// <summary>
        /// Get model element information
        /// </summary>
        public static List<object> GetElementFullInfo(Document doc, IList<Element> elementCollector)
        {
            List<object> infoList = new List<object>();

            // Retrieve and process the elements
            foreach (var element in elementCollector)
            {
                // Determine whether it is a physical model element
                // Get instance information
                if (element?.Category?.HasMaterialQuantities ?? false)
                {
                    var info = CreateElementFullInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // Handle element type information
                else if (element is ElementType elementType)
                {
                    var info = CreateTypeFullInfo(doc, elementType);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 3. Positioning elements (high frequency)
                else if (element is Level || element is Grid)
                {
                    var info = CreatePositioningElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 4. Spatial elements (medium-high frequency)
                else if (element is SpatialElement) // Room, Area, etc.
                {
                    var info = CreateSpatialElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 5. View elements (high frequency)
                else if (element is View)
                {
                    var info = CreateViewInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 6. Annotation elements (medium frequency)
                else if (element is TextNote || element is Dimension ||
                         element is IndependentTag || element is AnnotationSymbol ||
                         element is SpotDimension)
                {
                    var info = CreateAnnotationInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 7. Handle groups and links
                else if (element is Group || element is RevitLinkInstance)
                {
                    var info = CreateGroupOrLinkInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 8. Fall back to basic element information
                else
                {
                    var info = CreateElementBasicInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
            }

            return infoList;
        }

        /// <summary>
        /// Create a complete ElementInfo object for a single element
        /// </summary>
        public static ElementInstanceInfo CreateElementFullInfo(Document doc, Element element)
        {
            try
            {
                if (element?.Category == null)
                    return null;

                ElementInstanceInfo elementInfo = new ElementInstanceInfo();        // Create a custom class to store complete element information
                // ID
                elementInfo.Id = element.Id.GetIntValue();
                // UniqueId
                elementInfo.UniqueId = element.UniqueId;
                // Type name
                elementInfo.Name = element.Name;
                // Family name
                elementInfo.FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString();
                // Category
                elementInfo.Category = element.Category.Name;
                // Built-in category
                elementInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue());
                // Type ID
                elementInfo.TypeId = element.GetTypeId().GetIntValue();
                // Room ID
                if (element is FamilyInstance instance)
                    elementInfo.RoomId = instance.Room?.Id.GetIntValue() ?? -1;
                // Level
                elementInfo.Level = GetElementLevel(doc, element);
                // Bounding box
                BoundingBoxInfo boundingBoxInfo = new BoundingBoxInfo();
                elementInfo.BoundingBox = GetBoundingBoxInfo(element);
                // Parameters
                //elementInfo.Parameters = GetDimensionParameters(element);
                ParameterInfo thicknessParam = GetThicknessInfo(element);      // Thickness parameter
                if (thicknessParam != null)
                {
                    elementInfo.Parameters.Add(thicknessParam);
                }
                ParameterInfo heightParam = GetBoundingBoxHeight(elementInfo.BoundingBox);      // Height parameter
                if (heightParam != null)
                {
                    elementInfo.Parameters.Add(heightParam);
                }

                return elementInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create a complete TypeFullInfo object for a single element type
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public static ElementTypeInfo CreateTypeFullInfo(Document doc, ElementType elementType)
        {
            ElementTypeInfo typeInfo = new ElementTypeInfo();
            // Id
            typeInfo.Id = elementType.Id.GetIntValue();
            // UniqueId
            typeInfo.UniqueId = elementType.UniqueId;
            // Type name
            typeInfo.Name = elementType.Name;
            // Family name
            typeInfo.FamilyName = elementType.FamilyName;
            // Category
            typeInfo.Category = elementType.Category.Name;
            // Built-in category
            typeInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), elementType.Category.Id.GetIntValue());
            // Parameter dictionary
            typeInfo.Parameters = GetDimensionParameters(elementType);
            ParameterInfo thicknessParam = GetThicknessInfo(elementType);      // Thickness parameter
            if (thicknessParam != null)
            {
                typeInfo.Parameters.Add(thicknessParam);
            }
            return typeInfo;
        }

        /// <summary>
        /// Create information for positioning elements
        /// </summary>
        public static PositioningElementInfo CreatePositioningElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                PositioningElementInfo info = new PositioningElementInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Handle levels
                if (element is Level level)
                {
                    // Convert to millimeters
                    info.Elevation = level.Elevation * 304.8;
                }
                // Handle grids
                else if (element is Grid grid)
                {
                    Curve curve = grid.Curve;
                    if (curve != null)
                    {
                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        // Create JZLine (convert to millimeters)
                        info.GridLine = new JZLine(
                            start.X * 304.8, start.Y * 304.8, start.Z * 304.8,
                            end.X * 304.8, end.Y * 304.8, end.Z * 304.8);
                    }
                }

                // Get level information
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"An error occurred while creating positioning element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for spatial elements
        /// </summary>
        public static SpatialElementInfo CreateSpatialElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is SpatialElement))
                    return null;
                SpatialElement spatialElement = element as SpatialElement;
                SpatialElementInfo info = new SpatialElementInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get the room or area number
                if (element is Room room)
                {
                    info.Number = room.Number;
                    // Convert to mm³
                    info.Volume = room.Volume * Math.Pow(304.8, 3);
                }
                else if (element is Area area)
                {
                    info.Number = area.Number;
                }

                // Get area
                Parameter areaParam = element.get_Parameter(BuiltInParameter.ROOM_AREA);
                if (areaParam != null && areaParam.HasValue)
                {
                    // Convert to mm²
                    info.Area = areaParam.AsDouble() * Math.Pow(304.8, 2);
                }

                // Get perimeter
                Parameter perimeterParam = element.get_Parameter(BuiltInParameter.ROOM_PERIMETER);
                if (perimeterParam != null && perimeterParam.HasValue)
                {
                    // Convert to millimeters
                    info.Perimeter = perimeterParam.AsDouble() * 304.8;
                }

                // Get level
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"An error occurred while creating spatial element information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for view elements
        /// </summary>
        public static ViewInfo CreateViewInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is View))
                    return null;
                View view = element as View;

                ViewInfo info = new ViewInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    IsTemplate = view.IsTemplate,
                    DetailLevel = view.DetailLevel.ToString(),
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get the level associated with the view
                if (view is ViewPlan viewPlan && viewPlan.GenLevel != null)
                {
                    Level level = viewPlan.GenLevel;
                    info.AssociatedLevel = new LevelInfo
                    {
                        Id = level.Id.GetIntValue(),
                        Name = level.Name,
                        Height = level.Elevation * 304.8 // Convert to mm
                    };
                }

                // Check whether the view is open and active
                UIDocument uidoc = new UIDocument(doc);

                // Get all open views
                IList<UIView> openViews = uidoc.GetOpenUIViews();

                foreach (UIView uiView in openViews)
                {
                    // Check whether the view is open
                    if (uiView.ViewId.GetValue() == view.Id.GetValue())
                    {
                        info.IsOpen = true;

                        // Check whether the view is the currently active view
                        if (uidoc.ActiveView.Id.GetValue() == view.Id.GetValue())
                        {
                            info.IsActive = true;
                        }
                        break;
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"An error occurred while creating view information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for annotation elements
        /// </summary>
        public static AnnotationInfo CreateAnnotationInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                AnnotationInfo info = new AnnotationInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Get the owning view
                Parameter viewParam = element.get_Parameter(BuiltInParameter.VIEW_NAME);
                if (viewParam != null && viewParam.HasValue)
                {
                    info.OwnerView = viewParam.AsString();
                }
                else if (element.OwnerViewId != ElementId.InvalidElementId)
                {
                    View ownerView = doc.GetElement(element.OwnerViewId) as View;
                    info.OwnerView = ownerView?.Name;
                }

                // Handle text notes
                if (element is TextNote textNote)
                {
                    info.TextContent = textNote.Text;
                    XYZ position = textNote.Coord;
                    // Convert to mm
                    info.Position = new JZPoint(
                        position.X * 304.8,
                        position.Y * 304.8,
                        position.Z * 304.8);
                }
                // Handle dimension annotations
                else if (element is Dimension dimension)
                {
                    info.DimensionValue = dimension.Value.ToString();
                    XYZ origin = dimension.Origin;
                    // Convert to mm
                    info.Position = new JZPoint(
                        origin.X * 304.8,
                        origin.Y * 304.8,
                        origin.Z * 304.8);
                }
                // Handle other annotation elements
                else if (element is AnnotationSymbol annotationSymbol)
                {
                    if (annotationSymbol.Location is LocationPoint locationPoint)
                    {
                        XYZ position = locationPoint.Point;
                        // Convert to mm
                        info.Position = new JZPoint(
                            position.X * 304.8,
                            position.Y * 304.8,
                            position.Z * 304.8);
                    }
                }
                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"An error occurred while creating annotation information: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// Create information for groups or links
        /// </summary>
        public static GroupOrLinkInfo CreateGroupOrLinkInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                GroupOrLinkInfo info = new GroupOrLinkInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Handle groups
                if (element is Group group)
                {
                    ICollection<ElementId> memberIds = group.GetMemberIds();
                    info.MemberCount = memberIds?.Count;
                    info.GroupType = group.GroupType?.Name;
                }
                // Handle links
                else if (element is RevitLinkInstance linkInstance)
                {
                    RevitLinkType linkType = doc.GetElement(linkInstance.GetTypeId()) as RevitLinkType;
                    if (linkType != null)
                    {
                        ExternalFileReference extFileRef = linkType.GetExternalFileReference();
                        // Get the absolute path
                        string absPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extFileRef.GetAbsolutePath());
                        info.LinkPath = absPath;

                        // Use GetLinkedFileStatus to get the link status
                        LinkedFileStatus linkStatus = linkType.GetLinkedFileStatus();
                        info.LinkStatus = linkStatus.ToString();
                    }
                    else
                    {
                        info.LinkStatus = LinkedFileStatus.Invalid.ToString();
                    }

                    // Get the position
                    LocationPoint location = linkInstance.Location as LocationPoint;
                    if (location != null)
                    {
                        XYZ point = location.Point;
                        // Convert to mm
                        info.Position = new JZPoint(
                            point.X * 304.8,
                            point.Y * 304.8,
                            point.Z * 304.8);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"An error occurred while creating group and link information: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create enhanced basic information for an element
        /// </summary>
        public static ElementBasicInfo CreateElementBasicInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                ElementBasicInfo basicInfo = new ElementBasicInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    BoundingBox = GetBoundingBoxInfo(element)
                };
                return basicInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"An error occurred while creating basic element information: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get thickness parameter information for system-family components
        /// </summary>
        /// <param name="element">A system-family component (wall, floor, door, etc.)</param>
        /// <returns>A parameter info object, or null if invalid</returns>
        public static ParameterInfo GetThicknessInfo(Element element)
        {
            if (element == null)
            {
                return null;
            }

            // Get the element type
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            if (elementType == null)
            {
                return null;
            }

            // Get the appropriate built-in thickness parameter for the component type
            Parameter thicknessParam = null;

            if (elementType is WallType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
            }
            else if (elementType is FloorType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            }
            else if (elementType is FamilySymbol familySymbol)
            {
                switch (familySymbol.Category?.Id.GetIntValue())
                {
                    case (int)BuiltInCategory.OST_Doors:
                    case (int)BuiltInCategory.OST_Windows:
                        thicknessParam = elementType.get_Parameter(BuiltInParameter.FAMILY_THICKNESS_PARAM);
                        break;
                }
            }
            else if (elementType is CeilingType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.CEILING_THICKNESS);
            }

            if (thicknessParam != null && thicknessParam.HasValue)
            {
                return new ParameterInfo
                {
                    Name = "Thickness",
                    Value = $"{thicknessParam.AsDouble() * 304.8}"
                };
            }
            return null;
        }

        /// <summary>
        /// Get the level information associated with an element
        /// </summary>
        public static LevelInfo GetElementLevel(Document doc, Element element)
        {
            try
            {
                Level level = null;

                // Handle level retrieval for different element types
                if (element is Wall wall) // Wall
                {
                    level = doc.GetElement(wall.LevelId) as Level;
                }
                else if (element is Floor floor) // Floor
                {
                    Parameter levelParam = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }
                else if (element is FamilyInstance familyInstance) // Family instance (including standard model families)
                {
                    // Try to retrieve the family instance's level parameter
                    Parameter levelParam = familyInstance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                    // If the previous method does not work, try SCHEDULE_LEVEL_PARAM
                    if (level == null)
                    {
                        levelParam = familyInstance.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                        if (levelParam != null && levelParam.HasValue)
                        {
                            level = doc.GetElement(levelParam.AsElementId()) as Level;
                        }
                    }
                }
                else // Other elements
                {
                    // Try to retrieve a general level parameter
                    Parameter levelParam = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }

                if (level != null)
                {
                    LevelInfo levelInfo = new LevelInfo
                    {
                        Id = level.Id.GetIntValue(),
                        Name = level.Name,
                        Height = level.Elevation * 304.8
                    };
                    return levelInfo;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the bounding box information for an element
        /// </summary>
        public static BoundingBoxInfo GetBoundingBoxInfo(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null)
                    return null;
                return new BoundingBoxInfo
                {
                    Min = new JZPoint(
                        bbox.Min.X * 304.8,
                        bbox.Min.Y * 304.8,
                        bbox.Min.Z * 304.8),
                    Max = new JZPoint(
                        bbox.Max.X * 304.8,
                        bbox.Max.Y * 304.8,
                        bbox.Max.Z * 304.8)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the height parameter information from the bounding box
        /// </summary>
        /// <param name="boundingBoxInfo">Bounding box information</param>
        /// <returns>A parameter info object, or null if invalid</returns>
        public static ParameterInfo GetBoundingBoxHeight(BoundingBoxInfo boundingBoxInfo)
        {
            try
            {
                // Parameter validation
                if (boundingBoxInfo?.Min == null || boundingBoxInfo?.Max == null)
                {
                    return null;
                }

                // The difference along the Z axis is the height
                double height = Math.Abs(boundingBoxInfo.Max.Z - boundingBoxInfo.Min.Z);

                return new ParameterInfo
                {
                    Name = "Height",
                    Value = $"{height}"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the names and values of all non-empty parameters on an element
        /// </summary>
        /// <param name="element">A Revit element</param>
        /// <returns>A list of parameter information</returns>
        public static List<ParameterInfo> GetDimensionParameters(Element element)
        {
            // Check whether the element is null
            if (element == null)
            {
                return new List<ParameterInfo>();
            }

            var parameters = new List<ParameterInfo>();

            // Get all parameters on the element
            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    // Skip invalid parameters
                    if (!param.HasValue || param.IsReadOnly)
                    {
                        continue;
                    }

                    // If the current parameter is a dimension-related parameter
                    if (IsDimensionParameter(param))
                    {
                        // Get the parameter value as a string
                        string value = param.AsValueString();

                        // If the value is not empty, add it to the list
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            parameters.Add(new ParameterInfo
                            {
                                Name = param.Definition.Name,
                                Value = value
                            });
                        }
                    }
                }
                catch
                {
                    // If retrieving a parameter value fails, continue with the next one
                    continue;
                }
            }

            // Return the parameters sorted by name
            return parameters.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// Determine whether a parameter is a writable dimension parameter
        /// </summary>
        public static bool IsDimensionParameter(Parameter param)
        {

#if REVIT2023_OR_GREATER
            // In Revit 2023 and later, use Definition.GetDataType() to get the parameter type
            ForgeTypeId paramTypeId = param.Definition.GetDataType();

            // Determine whether the parameter type is dimension-related
            bool isDimensionType = paramTypeId.Equals(SpecTypeId.Length) ||
                                   paramTypeId.Equals(SpecTypeId.Angle) ||
                                   paramTypeId.Equals(SpecTypeId.Area) ||
                                   paramTypeId.Equals(SpecTypeId.Volume);
            // Only store dimension-type parameters
            return isDimensionType;
#else
            // Determine whether the parameter type is dimension-related
            bool isDimensionType = param.Definition.ParameterType == ParameterType.Length ||
                                   param.Definition.ParameterType == ParameterType.Angle ||
                                   param.Definition.ParameterType == ParameterType.Area ||
                                   param.Definition.ParameterType == ParameterType.Volume;

            // Only store dimension-type parameters
            return isDimensionType;
#endif
        }

    }

    /// <summary>
    /// Custom class for storing complete element information
    /// </summary>
    public class ElementInstanceInfo
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Type ID
        /// </summary>
        public int TypeId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Room ID
        /// </summary>
        public int RoomId { get; set; }
        /// <summary>
        /// Associated level name
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Instance parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// Custom class for storing complete element type information
    /// </summary>
    public class ElementTypeInfo
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category ID
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// Type parameters
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// Base information class for positioning elements (levels, grids, etc.)
    /// </summary>
    public class PositioningElementInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique element ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// .NET class name of the element
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Elevation value (for levels, in mm)
        /// </summary>
        public double? Elevation { get; set; }
        /// <summary>
        /// Associated level
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Grid line (for grids)
        /// </summary>
        public JZLine GridLine { get; set; }
    }
    /// <summary>
    /// Base information class for spatial elements (rooms, areas, etc.)
    /// </summary>
    public class SpatialElementInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique element ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Number
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// .NET class name of the element
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Area (in mm²)
        /// </summary>
        public double? Area { get; set; }
        /// <summary>
        /// Volume (in mm³)
        /// </summary>
        public double? Volume { get; set; }
        /// <summary>
        /// Perimeter (in mm)
        /// </summary>
        public double? Perimeter { get; set; }
        /// <summary>
        /// Associated level
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// Base information class for view elements
    /// </summary>
    public class ViewInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique element ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// .NET class name of the element
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// View type
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// View scale
        /// </summary>
        public int? Scale { get; set; }

        /// <summary>
        /// Whether the view is a template
        /// </summary>
        public bool IsTemplate { get; set; }

        /// <summary>
        /// Detail level
        /// </summary>
        public string DetailLevel { get; set; }

        /// <summary>
        /// Associated level
        /// </summary>
        public LevelInfo AssociatedLevel { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// Whether the view is open
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Whether the view is the currently active view
        /// </summary>
        public bool IsActive { get; set; }
    }
    /// <summary>
    /// Base information class for annotation elements
    /// </summary>
    public class AnnotationInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique element ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// .NET class name of the element
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Owning view
        /// </summary>
        public string OwnerView { get; set; }
        /// <summary>
        /// Text content (for text notes)
        /// </summary>
        public string TextContent { get; set; }
        /// <summary>
        /// Position information (in mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// Dimension value (for dimensions)
        /// </summary>
        public string DimensionValue { get; set; }
    }
    /// <summary>
    /// Base information class for groups and links
    /// </summary>
    public class GroupOrLinkInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique element ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// .NET class name of the element
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// Group member count
        /// </summary>
        public int? MemberCount { get; set; }
        /// <summary>
        /// Group type
        /// </summary>
        public string GroupType { get; set; }
        /// <summary>
        /// Link status
        /// </summary>
        public string LinkStatus { get; set; }
        /// <summary>
        /// Link path
        /// </summary>
        public string LinkPath { get; set; }
        /// <summary>
        /// Position information (in mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// Enhanced base information class for elements
    /// </summary>
    public class ElementBasicInfo
    {
        /// <summary>
        /// Element ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Unique element ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Family name
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// Category name
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// Built-in category (optional)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// Location information
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }



    /// <summary>
    /// Custom class for storing complete parameter information
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// Custom class for storing bounding box information
    /// </summary>
    public class BoundingBoxInfo
    {
        public JZPoint Min { get; set; }
        public JZPoint Max { get; set; }
    }

    /// <summary>
    /// Custom class for storing level information
    /// </summary>
    public class LevelInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Height { get; set; }
    }



}
