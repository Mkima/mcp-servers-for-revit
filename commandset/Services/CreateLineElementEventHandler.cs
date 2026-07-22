using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System.Diagnostics;

namespace RevitMCPCommandSet.Services
{
    public class CreateLineElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;
        
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        public List<LineElement> CreatedInfo { get; private set; }
        public AIResult<List<int>> Result { get; private set; }

        public string _wallName = "Generic - ";
        public string _ductName = "Rectangular Duct - ";

        public void SetParameters(List<LineElement> data)
        {
            CreatedInfo = data;
            _resetEvent.Reset();
            Debug.WriteLine($"[CreateLineElement] SetParameters called with {data?.Count ?? 0} items");
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;
            Debug.WriteLine("[CreateLineElement] Execute started");

            try
            {
                var elementIds = new List<int>();
                Debug.WriteLine($"[CreateLineElement] Processing {CreatedInfo?.Count ?? 0} line elements");

                if (CreatedInfo == null || CreatedInfo.Count == 0)
                {
                    Debug.WriteLine("[CreateLineElement] WARNING: No creation data provided");
                    Result = new AIResult<List<int>>
                    {
                        Success = false,
                        Message = "No line element data provided",
                        Response = elementIds,
                    };
                    return;
                }

                int itemIndex = 0;
                foreach (var data in CreatedInfo)
                {
                    itemIndex++;
                    Debug.WriteLine($"[CreateLineElement] Processing item {itemIndex}/{CreatedInfo.Count}");

                    // Step 0: Get element category
                    BuiltInCategory builtInCategory = BuiltInCategory.INVALID;
                    Enum.TryParse(data.Category.Replace(".", ""), true, out builtInCategory);
                    Debug.WriteLine($"[CreateLineElement] Category: {data.Category} -> {builtInCategory}");

                    // Step 1: Get level and offset
                    Level baseLevel = null;
                    Level topLevel = null;
                    double topOffset = -1;
                    double baseOffset = -1;
                    
                    baseLevel = doc.FindNearestLevel(data.BaseLevel / 304.8);
                    baseOffset = (data.BaseOffset + data.BaseLevel) / 304.8 - baseLevel.Elevation;
                    topLevel = doc.FindNearestLevel((data.BaseLevel + data.BaseOffset + data.Height) / 304.8);
                    topOffset = (data.BaseLevel + data.BaseOffset + data.Height) / 304.8 - topLevel.Elevation;
                    
                    Debug.WriteLine($"[CreateLineElement] Base Level: {baseLevel?.Name ?? "NULL"}, Offset: {baseOffset}");

                    if (baseLevel == null)
                    {
                        Debug.WriteLine("[CreateLineElement] ERROR: Base level is null, skipping item");
                        continue;
                    }

                    // Step 2: Get family symbol type
                    FamilySymbol symbol = null;
                    WallType wallType = null;
                    DuctType ductType = null;

                    if (data.TypeId != -1 && data.TypeId != 0)
                    {
                        ElementId typeELeId = new ElementId(data.TypeId);
                        Debug.WriteLine($"[CreateLineElement] Looking up TypeId: {data.TypeId}");
                        
                        if (typeELeId != null)
                        {
                            Element typeEle = doc.GetElement(typeELeId);
                            if (typeEle != null && typeEle is FamilySymbol)
                            {
                                symbol = typeEle as FamilySymbol;
                                builtInCategory = (BuiltInCategory)symbol.Category.Id.IntegerValue;
                                Debug.WriteLine($"[CreateLineElement] Found FamilySymbol: {symbol.Name}");
                            }
                            else if (typeEle != null && typeEle is WallType)
                            {
                                wallType = typeEle as WallType;
                                builtInCategory = (BuiltInCategory)wallType.Category.Id.IntegerValue;
                                Debug.WriteLine($"[CreateLineElement] Found WallType: {wallType.Name}");
                            }
                            else if (typeEle != null && typeEle is DuctType)
                            {
                                ductType = typeEle as DuctType;
                                builtInCategory = (BuiltInCategory)ductType.Category.Id.IntegerValue;
                                Debug.WriteLine($"[CreateLineElement] Found DuctType: {ductType.Name}");
                            }
                            else
                            {
                                Debug.WriteLine($"[CreateLineElement] WARNING: TypeId {data.TypeId} not found or wrong type");
                            }
                        }
                    }

                    if (builtInCategory == BuiltInCategory.INVALID)
                    {
                        Debug.WriteLine("[CreateLineElement] ERROR: Invalid category, skipping item");
                        continue;
                    }

                    switch (builtInCategory)
                    {
                        case BuiltInCategory.OST_Walls:
                            if (wallType == null)
                            {
                                Debug.WriteLine("[CreateLineElement] Creating wall type...");
                                using (Transaction transaction = new Transaction(doc, "Create Wall Type"))
                                {
                                    transaction.Start();
                                    wallType = CreateOrGetWallType(doc, data.Thickness / 304.8);
                                    transaction.Commit();
                                }
                                if (wallType == null)
                                {
                                    Debug.WriteLine("[CreateLineElement] ERROR: Failed to create wall type");
                                    continue;
                                }
                                Debug.WriteLine($"[CreateLineElement] Wall type created: {wallType.Name}");
                            }
                            break;

                        case BuiltInCategory.OST_DuctCurves:
                            if (ductType == null)
                            {
                                Debug.WriteLine("[CreateLineElement] Creating duct type...");
                                using (Transaction transaction = new Transaction(doc, "Create Duct Type"))
                                {
                                    transaction.Start();
                                    ductType = CreateOrGetDuctType(doc, data.Thickness / 304.8, data.Height / 304.8);
                                    transaction.Commit();
                                }
                                if (ductType == null)
                                {
                                    Debug.WriteLine("[CreateLineElement] ERROR: Failed to create duct type");
                                    continue;
                                }
                                Debug.WriteLine($"[CreateLineElement] Duct type created: {ductType.Name}");
                            }
                            break;

                        default:
                            if (symbol == null)
                            {
                                Debug.WriteLine($"[CreateLineElement] Finding default symbol for category: {builtInCategory}");
                                symbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(builtInCategory)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault(fs => fs.IsActive);
                                if (symbol == null)
                                {
                                    symbol = new FilteredElementCollector(doc)
                                        .OfClass(typeof(FamilySymbol))
                                        .OfCategory(builtInCategory)
                                        .Cast<FamilySymbol>()
                                        .FirstOrDefault();
                                }
                                if (symbol != null)
                                    Debug.WriteLine($"[CreateLineElement] Default symbol found: {symbol.Name}");
                            }
                            if (symbol == null)
                            {
                                Debug.WriteLine("[CreateLineElement] ERROR: No symbol found, skipping item");
                                continue;
                            }
                            break;
                    }

                    // Step 3: Create element using appropriate method
                    Debug.WriteLine($"[CreateLineElement] Creating element for category: {builtInCategory}");
                    using (Transaction transaction = new Transaction(doc, "Create Line-Based Element"))
                    {
                        transaction.Start();
                        try
                        {
                            switch (builtInCategory)
                            {
                                case BuiltInCategory.OST_Walls:
                                    Wall wall = null;
                                    Debug.WriteLine("[CreateLineElement] Creating Wall...");
                                    wall = Wall.Create(
                                        doc,
                                        JZLine.ToLine(data.LocationLine),
                                        wallType.Id,
                                        baseLevel.Id,
                                        data.Height / 304.8,
                                        baseOffset,
                                        false,
                                        false
                                    );
                                    if (wall != null)
                                    {
                                        elementIds.Add(wall.Id.IntegerValue);
                                        Debug.WriteLine($"[CreateLineElement] Wall created successfully. ElementId: {wall.Id.IntegerValue}");
                                    }
                                    else
                                    {
                                        Debug.WriteLine("[CreateLineElement] ERROR: Wall.Create returned null");
                                    }
                                    break;

                                case BuiltInCategory.OST_DuctCurves:
                                    Duct duct = null;
                                    Debug.WriteLine("[CreateLineElement] Creating Duct...");
                                    MEPSystemType mepSystemType = new FilteredElementCollector(doc)
                                        .OfClass(typeof(MEPSystemType))
                                        .Cast<MEPSystemType>()
                                        .FirstOrDefault(m => m.SystemClassification == MEPSystemClassification.SupplyAir);

                                    if (mepSystemType == null)
                                    {
                                        Debug.WriteLine("[CreateLineElement] ERROR: MEPSystemType not found");
                                    }
                                    else
                                    {
                                        duct = Duct.Create(
                                            doc,
                                            mepSystemType.Id,
                                            ductType.Id,
                                            baseLevel.Id,
                                            JZLine.ToLine(data.LocationLine).GetEndPoint(0),
                                            JZLine.ToLine(data.LocationLine).GetEndPoint(1)
                                        );

                                        if (duct != null)
                                        {
                                            Parameter offsetParam = duct.get_Parameter(BuiltInParameter.RBS_OFFSET_PARAM);
                                            if (offsetParam != null)
                                                offsetParam.Set(baseOffset);
                                            elementIds.Add(duct.Id.IntegerValue);
                                            Debug.WriteLine($"[CreateLineElement] Duct created successfully. ElementId: {duct.Id.IntegerValue}");
                                        }
                                        else
                                        {
                                            Debug.WriteLine("[CreateLineElement] ERROR: Duct.Create returned null");
                                        }
                                    }
                                    break;

                                default:
                                    if (!symbol.IsActive)
                                    {
                                        Debug.WriteLine("[CreateLineElement] Activating symbol...");
                                        symbol.Activate();
                                    }

                                    Debug.WriteLine($"[CreateLineElement] Creating FamilyInstance with symbol: {symbol.Name}");
                                    Line line = JZLine.ToLine(data.LocationLine);
                                    FamilyInstance instance = doc.Create.NewFamilyInstance(
                                        line,
                                        symbol,
                                        baseLevel,
                                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                                    );

                                    if (instance != null)
                                    {
                                        if (baseOffset != -1)
                                        {
                                            Parameter offsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                                            if (offsetParam != null && !offsetParam.IsReadOnly)
                                                offsetParam.Set(baseOffset);
                                        }
                                        elementIds.Add(instance.Id.IntegerValue);
                                        Debug.WriteLine($"[CreateLineElement] FamilyInstance created successfully. ElementId: {instance.Id.IntegerValue}");
                                    }
                                    else
                                    {
                                        Debug.WriteLine("[CreateLineElement] ERROR: FamilyInstance creation returned null");
                                    }
                                    break;
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[CreateLineElement] ERROR during element creation: {innerEx.Message}");
                            Debug.WriteLine($"[CreateLineElement] Stack trace: {innerEx.StackTrace}");
                            throw;
                        }
                        finally
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started)
                            {
                                transaction.Commit();
                                Debug.WriteLine("[CreateLineElement] Transaction committed");
                            }
                        }
                    }
                }

                Result = new AIResult<List<int>>
                {
                    Success = true,
                    Message = $"Successfully created {elementIds.Count} family instances. ElementIds are stored in the Response property.",
                    Response = elementIds,
                };
                Debug.WriteLine($"[CreateLineElement] Execution completed successfully. Created {elementIds.Count} elements");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CreateLineElement] FATAL ERROR: {ex.Message}");
                Debug.WriteLine($"[CreateLineElement] Stack trace: {ex.StackTrace}");
                
                Result = new AIResult<List<int>>
                {
                    Success = false,
                    Message = $"Error creating line-based element: {ex.Message}",
                };
                TaskDialog.Show("Error", $"Error creating line-based element: {ex.Message}\n\nCheck Debug Output for details");
            }
            finally
            {
                _resetEvent.Set();
                Debug.WriteLine("[CreateLineElement] Execute completed");
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "Create Line-Based Element";
        }

        private WallType CreateOrGetWallType(Document doc, double width = 200 / 304.8)
        {
            Debug.WriteLine($"[CreateWallType] Creating wall type with width: {width}mm");
            
            WallType existingType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(w => w.Name == $"{_wallName}{width * 304.8}mm");
            if (existingType != null)
            {
                Debug.WriteLine($"[CreateWallType] Existing wall type found: {existingType.Name}");
                return existingType;
            }

            WallType baseWallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(w => w.Name.Contains("Generic"));
            if (baseWallType == null)
            {
                baseWallType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .FirstOrDefault();
            }

            if (baseWallType == null)
            {
                Debug.WriteLine("[CreateWallType] ERROR: No base wall type found");
                throw new InvalidOperationException("No available base wall type found");
            }

            Debug.WriteLine($"[CreateWallType] Base wall type: {baseWallType.Name}");
            WallType newWallType = baseWallType.Duplicate($"{_wallName}{width * 304.8}mm") as WallType;

            CompoundStructure cs = newWallType.GetCompoundStructure();
            if (cs != null)
            {
                ElementId materialId = cs.GetLayers().First().MaterialId;
                CompoundStructureLayer newLayer = new CompoundStructureLayer(
                    width,
                    MaterialFunctionAssignment.Structure,
                    materialId
                );

                IList<CompoundStructureLayer> newLayers = new List<CompoundStructureLayer> { newLayer };
                cs.SetLayers(newLayers);
                newWallType.SetCompoundStructure(cs);
                Debug.WriteLine($"[CreateWallType] Wall type created: {newWallType.Name}");
            }
            return newWallType;
        }

        private DuctType CreateOrGetDuctType(Document doc, double width, double height)
        {
            string typeName = $"{_ductName}{width * 304.8}x{height * 304.8}mm";
            Debug.WriteLine($"[CreateDuctType] Creating duct type: {typeName}");

            DuctType existingType = new FilteredElementCollector(doc)
                .OfClass(typeof(DuctType))
                .Cast<DuctType>()
                .FirstOrDefault(d => d.Name == typeName && d.Shape == ConnectorProfileType.Rectangular);

            if (existingType != null)
            {
                Debug.WriteLine($"[CreateDuctType] Existing duct type found: {existingType.Name}");
                return existingType;
            }

            DuctType baseDuctType = new FilteredElementCollector(doc)
                .OfClass(typeof(DuctType))
                .Cast<DuctType>()
                .FirstOrDefault(d => d.Shape == ConnectorProfileType.Rectangular);

            if (baseDuctType == null)
            {
                Debug.WriteLine("[CreateDuctType] ERROR: No base duct type found");
                throw new InvalidOperationException("No available base rectangular duct type found");
            }

            Debug.WriteLine($"[CreateDuctType] Base duct type: {baseDuctType.Name}");
            DuctType newDuctType = baseDuctType.Duplicate(typeName) as DuctType;

            Parameter widthParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            Parameter heightParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

            if (widthParam != null && heightParam != null)
            {
                widthParam.Set(width);
                heightParam.Set(height);
                Debug.WriteLine($"[CreateDuctType] Duct type created: {newDuctType.Name}");
            }
            else
            {
                Debug.WriteLine("[CreateDuctType] WARNING: Could not set width/height parameters");
            }

            return newDuctType;
        }
    }
}
