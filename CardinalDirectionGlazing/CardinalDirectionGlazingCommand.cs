using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CardinalDirectionGlazing
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class CardinalDirectionGlazingCommand : IExternalCommand
    {
        // Значения-эталоны (можно вынести в настройки, если потребуется)
        private const string CurtainPanelConstructionTypeGlazing = "Остекление";
        private const string BasicWallModelGroupGlazing = "Остекления";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CalculationTrace trace = new CalculationTrace(
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                "NotSelected");

            try
            {
                try
                {
                    _ = GetPluginStartInfo();
                }
                catch { }

                Document doc = commandData.Application.ActiveUIDocument.Document;
                Selection sel = commandData.Application.ActiveUIDocument.Selection;
                trace.HostDocument = CreateDocumentTrace(doc);

            // GUIDы параметров окон по сторонам света
            Guid windowsAreaNorthGuid = new Guid("820af414-f6ec-472d-887c-a2046a0c5988");
            Guid windowsAreaSouthGuid = new Guid("81ab8e02-45c6-4d26-b0e5-a6736b0c352d");
            Guid windowsAreaWestGuid = new Guid("65fe3416-f836-48ff-bed9-3fdf2126a1f9");
            Guid windowsAreaEastGuid = new Guid("fc33c487-9bbb-43d6-a7f4-aba5f9638fe3");
            Guid windowsAreaNorthwestGuid = new Guid("f78f8a53-cea7-4e00-955c-3748aa7a37c7");
            Guid windowsAreaNortheastGuid = new Guid("b8120c53-0793-4932-bc71-845302914573");
            Guid windowsAreaSouthwestGuid = new Guid("3ff1f178-2cff-4b54-a0d3-eee58fa1622c");
            Guid windowsAreaSoutheastGuid = new Guid("c5e261ae-68f5-4a91-a55f-8686d278f5ab");

            // Получаем список связанных файлов
            List<RevitLinkInstance> revitLinkInstanceList = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            // Открываем окно WPF для выбора опций
            CardinalDirectionGlazingWPF cardinalDirectionGlazingWPF = new CardinalDirectionGlazingWPF(revitLinkInstanceList);
            cardinalDirectionGlazingWPF.ShowDialog();
            if (cardinalDirectionGlazingWPF.DialogResult != true)
            {
                CompleteRunTrace(trace, "Cancelled", "DialogCancelled");
                return Result.Cancelled;
            }

            trace.Mode = cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Spaces"
                ? "Spaces"
                : "Rooms";

            // Проверка выбранного связанного файла
            if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Spaces")
            {
                // Если выбрана обработка пространств, связанный файл обязателен
                if (cardinalDirectionGlazingWPF.SelectedRevitLinkInstance == null)
                {
                    TaskDialog.Show("Revit", "Связанный файл не выбран! Для обработки пространств необходим связанный файл.");
                    CompleteRunTrace(trace, "Cancelled", "MissingLinkForSpaces");
                    return Result.Cancelled;
                }
            }


            Document linkDoc = null;
            Transform transform = null;

            // Всегда берем истинный север из текущего документа
            ProjectPosition position = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
            Transform trueNorthTransform = Transform.CreateRotationAtPoint(XYZ.BasisZ, -position.Angle, XYZ.Zero);
            XYZ trueNorthBasisY = trueNorthTransform.OfVector(XYZ.BasisY);
            XYZ trueNorthBasisX = trueNorthTransform.OfVector(XYZ.BasisX);

            // Если выбран связанный файл, используем его для обработки, но истинный север берем из текущего документа
            if (cardinalDirectionGlazingWPF.SelectedRevitLinkInstance != null)
            {
                linkDoc = cardinalDirectionGlazingWPF.SelectedRevitLinkInstance.GetLinkDocument();
                transform = cardinalDirectionGlazingWPF.SelectedRevitLinkInstance.GetTotalTransform();
                trace.SelectedLink = CreateLinkTrace(cardinalDirectionGlazingWPF.SelectedRevitLinkInstance, linkDoc, transform);
            }

            trace.TrueNorth = new DirectionTrace
            {
                EastBasis = CreateTraceVector(trueNorthBasisX),
                NorthBasis = CreateTraceVector(trueNorthBasisY)
            };

            // Выбираем обработку: пространства или помещения
            List<Element> elementsList = new List<Element>();
            // Проверка, обрабатываются ли пространства или помещения
            if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Spaces")
            {
                if (cardinalDirectionGlazingWPF.SpacesForProcessingButtonName == "radioButton_Selected")
                {
                    // Получаем выбранные пространства
                    List<Space> selectedSpaces = GetSpacesFromCurrentSelection(doc, sel);

                    // Если ничего не выбрано, даем пользователю возможность выбрать пространства
                    if (selectedSpaces.Count == 0)
                    {
                        try
                        {
                            IList<Reference> selectedReferences = sel.PickObjects(ObjectType.Element, new SpaceSelectionFilter(), "Выберите пространства");
                            selectedSpaces = selectedReferences.Select(r => doc.GetElement(r.ElementId) as Space).ToList();
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            CompleteRunTrace(trace, "Cancelled", "SpaceSelectionCancelled");
                            return Result.Cancelled;
                        }
                    }

                    // Преобразуем пространства в список элементов для дальнейшей обработки
                    elementsList = selectedSpaces.Cast<Element>().ToList();
                }
                else
                {
                    // Обрабатываем все пространства
                    elementsList = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_MEPSpaces)
                        .WhereElementIsNotElementType()
                        .ToList();
                }
            }
            else if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Rooms")
            {
                if (cardinalDirectionGlazingWPF.SpacesForProcessingButtonName == "radioButton_Selected")
                {
                    // Получаем выбранные помещения
                    List<Room> selectedRooms = GetRoomsFromCurrentSelection(doc, sel);

                    // Если ничего не выбрано, даем пользователю возможность выбрать помещения
                    if (selectedRooms.Count == 0)
                    {
                        try
                        {
                            IList<Reference> selectedReferences = sel.PickObjects(ObjectType.Element, new RoomSelectionFilter(), "Выберите помещения");
                            selectedRooms = selectedReferences.Select(r => doc.GetElement(r.ElementId) as Room).ToList();
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            CompleteRunTrace(trace, "Cancelled", "RoomSelectionCancelled");
                            return Result.Cancelled;
                        }
                    }

                    // Преобразуем помещения в список элементов для дальнейшей обработки
                    elementsList = selectedRooms.Cast<Element>().ToList();
                }
                else
                {
                    // Обрабатываем все помещения
                    elementsList = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .ToList();
                }
            }

            if (elementsList.Count == 0)
            {
                TaskDialog.Show("Revit", "Не найдены пространства или помещения для обработки.");
                CompleteRunTrace(trace, "Cancelled", "NoTargets");
                return Result.Cancelled;
            }

            trace.TargetCount = elementsList.Count;

            // Проверка наличия параметров в первом элементе
            if (elementsList.Count != 0)
            {
                Element firstElement = elementsList.First();

                // Проверка каждого параметра по отдельности и вывод сообщения, если параметр отсутствует
                if (firstElement.get_Parameter(windowsAreaNorthGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_С\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterNorth");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaSouthGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_Ю\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterSouth");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaWestGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_З\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterWest");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaEastGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_В\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterEast");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaNorthwestGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_СЗ\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterNorthwest");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaNortheastGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_СВ\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterNortheast");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaSouthwestGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_ЮЗ\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterSouthwest");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaSoutheastGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_ЮВ\" не найден! Добавьте параметр.");
                    CompleteRunTrace(trace, "Cancelled", "MissingParameterSoutheast");
                    return Result.Cancelled;
                }
            }

            // Инициализируем списки для окон и стен с остеклением
            List<FamilyInstance> windowsList = new List<FamilyInstance>();
            List<Wall> curtainWallsList = new List<Wall>();
            List<Wall> glazingBasicWallsList = new List<Wall>();

            // Инициализируем списки для окон и стен из связанного документа
            List<FamilyInstance> linkedWindowsList = new List<FamilyInstance>();
            List<Wall> linkedCurtainWallsList = new List<Wall>();
            List<Wall> linkedGlazingBasicWallsList = new List<Wall>();

            if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Spaces")
            {
                // Если обрабатываем пространства, берем окна и стены только из связанного файла
                if (linkDoc != null)
                {
                    linkedWindowsList = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Windows)
                        .OfClass(typeof(FamilyInstance))
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .Where(w => w.SuperComponent == null)
                        .ToList();

                    linkedCurtainWallsList = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Walls)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .Cast<Wall>()
                        .Where(w => w.CurtainGrid != null)
                        .ToList();

                    linkedGlazingBasicWallsList = CollectGlazingBasicWalls(linkDoc);
                }
                else
                {
                    TaskDialog.Show("Revit", "Связанный файл для обработки пространств не найден.");
                    CompleteRunTrace(trace, "Cancelled", "LinkDocumentUnavailable");
                    return Result.Cancelled;
                }
            }
            else if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Rooms")
            {
                // Если обрабатываем помещения, берем окна и стены как из связанного файла, так и из текущего документа

                // Окна и стены из текущего документа
                windowsList = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(w => w.SuperComponent == null)
                    .ToList();

                curtainWallsList = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType()
                    .Cast<Wall>()
                    .Where(w => w.CurtainGrid != null)
                    .Where(IsOuterCurtainWallByModelGroup)
                    .ToList();

                glazingBasicWallsList = CollectGlazingBasicWalls(doc);

                // Если выбран связанный файл, добавляем окна и стены из связанного файла
                if (linkDoc != null)
                {
                    linkedWindowsList = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Windows)
                        .OfClass(typeof(FamilyInstance))
                        .WhereElementIsNotElementType()
                        .Cast<FamilyInstance>()
                        .Where(w => w.SuperComponent == null)
                        .ToList();

                    linkedCurtainWallsList = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Walls)
                        .OfClass(typeof(Wall))
                        .WhereElementIsNotElementType()
                        .Cast<Wall>()
                        .Where(w => w.CurtainGrid != null)
                        .Where(IsOuterCurtainWallByModelGroup)
                        .ToList();

                    linkedGlazingBasicWallsList = CollectGlazingBasicWalls(linkDoc);
                }
            }

            trace.SourceCollectionCounts.Add(new SourceCollectionTrace { Source = "CurrentDocument.Windows", Count = windowsList.Count });
            trace.SourceCollectionCounts.Add(new SourceCollectionTrace { Source = "CurrentDocument.CurtainWalls", Count = curtainWallsList.Count });
            trace.SourceCollectionCounts.Add(new SourceCollectionTrace { Source = "CurrentDocument.GlazingBasicWalls", Count = glazingBasicWallsList.Count });
            trace.SourceCollectionCounts.Add(new SourceCollectionTrace { Source = "LinkedDocument.Windows", Count = linkedWindowsList.Count });
            trace.SourceCollectionCounts.Add(new SourceCollectionTrace { Source = "LinkedDocument.CurtainWalls", Count = linkedCurtainWallsList.Count });
            trace.SourceCollectionCounts.Add(new SourceCollectionTrace { Source = "LinkedDocument.GlazingBasicWalls", Count = linkedGlazingBasicWallsList.Count });
            if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Rooms")
            {
                trace.SourceCollectionCounts.Add(new SourceCollectionTrace
                {
                    Source = "CurrentDocument.CurtainWallsExcludedByOuterModelGroup",
                    Count = CountCurtainWallsExcludedByOuterModelGroup(doc)
                });
                if (linkDoc != null)
                {
                    trace.SourceCollectionCounts.Add(new SourceCollectionTrace
                    {
                        Source = "LinkedDocument.CurtainWallsExcludedByOuterModelGroup",
                        Count = CountCurtainWallsExcludedByOuterModelGroup(linkDoc)
                    });
                }
            }

            // Начинаем транзакцию для обновления данных в Revit
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Остекление по сторонам света");

                foreach (Element element in elementsList)
                {
                    TargetTrace targetTrace = trace.StartTarget(element.UniqueId);
                    targetTrace.ElementId = element.Id.ToString();
                    targetTrace.ElementType = element is Space ? "Space" : element is Room ? "Room" : element.GetType().Name;
                    targetTrace.Number = GetSpatialNumber(element);
                    targetTrace.Name = GetSpatialName(element);
                    targetTrace.ParameterWrites = CreateParameterWriteTraces(
                        element,
                        windowsAreaNorthGuid,
                        windowsAreaSouthGuid,
                        windowsAreaWestGuid,
                        windowsAreaEastGuid,
                        windowsAreaNorthwestGuid,
                        windowsAreaNortheastGuid,
                        windowsAreaSouthwestGuid,
                        windowsAreaSoutheastGuid);

                    double windowsAreaNorth = 0;
                    double windowsAreaSouth = 0;
                    double windowsAreaWest = 0;
                    double windowsAreaEast = 0;
                    double windowsAreaNorthwest = 0;
                    double windowsAreaNortheast = 0;
                    double windowsAreaSouthwest = 0;
                    double windowsAreaSoutheast = 0;

                    Solid elementSolid = GetSolidFromElement(element);
                    targetTrace.SolidFound = elementSolid != null;
                    targetTrace.SolidVolume = elementSolid?.Volume;
                    if (elementSolid == null)
                    {
                        targetTrace.Complete("Skipped", "NoTargetSolid");
                        continue;
                    }

                    Transform tr = transform ?? Transform.Identity;

                    ProcessWindows(
                        windowsList,
                        Transform.Identity,
                        element,
                        elementSolid,
                        doc,
                        targetTrace,
                        "CurrentWindows",
                        trueNorthBasisX,
                        trueNorthBasisY,
                        ref windowsAreaNorth,
                        ref windowsAreaSouth,
                        ref windowsAreaWest,
                        ref windowsAreaEast,
                        ref windowsAreaNorthwest,
                        ref windowsAreaNortheast,
                        ref windowsAreaSouthwest,
                        ref windowsAreaSoutheast);

                    // Обработка панелей витражей из текущего документа (Transform.Identity)
                    ProcessCurtainWallFills(
                        doc,
                        curtainWallsList,
                        Transform.Identity,
                        element,
                        elementSolid,
                        targetTrace,
                        "CurrentCurtainWalls",
                        trueNorthBasisX,
                        trueNorthBasisY,
                        ref windowsAreaNorth,
                        ref windowsAreaSouth,
                        ref windowsAreaWest,
                        ref windowsAreaEast,
                        ref windowsAreaNorthwest,
                        ref windowsAreaNortheast,
                        ref windowsAreaSouthwest,
                        ref windowsAreaSoutheast);

                    if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Rooms")
                        TraceExcludedOuterCurtainWalls(doc, targetTrace, "CurrentCurtainWalls");

                    TraceExcludedBasicWallCandidates(doc, targetTrace, "CurrentGlazingWalls");
                    ProcessGlazingBasicWalls(
                        glazingBasicWallsList,
                        Transform.Identity,
                        element,
                        elementSolid,
                        targetTrace,
                        "CurrentGlazingWalls",
                        trueNorthBasisX,
                        trueNorthBasisY,
                        ref windowsAreaNorth,
                        ref windowsAreaSouth,
                        ref windowsAreaWest,
                        ref windowsAreaEast,
                        ref windowsAreaNorthwest,
                        ref windowsAreaNortheast,
                        ref windowsAreaSouthwest,
                        ref windowsAreaSoutheast);

                    ProcessWindows(
                        linkedWindowsList,
                        tr,
                        element,
                        elementSolid,
                        doc,
                        targetTrace,
                        "LinkedWindows",
                        trueNorthBasisX,
                        trueNorthBasisY,
                        ref windowsAreaNorth,
                        ref windowsAreaSouth,
                        ref windowsAreaWest,
                        ref windowsAreaEast,
                        ref windowsAreaNorthwest,
                        ref windowsAreaNortheast,
                        ref windowsAreaSouthwest,
                        ref windowsAreaSoutheast);

                    // Обработка панелей витражей из связанного документа (используем tr)
                    ProcessCurtainWallFills(
                        linkDoc,
                        linkedCurtainWallsList,
                        tr,
                        element,
                        elementSolid,
                        targetTrace,
                        "LinkedCurtainWalls",
                        trueNorthBasisX,
                        trueNorthBasisY,
                        ref windowsAreaNorth,
                        ref windowsAreaSouth,
                        ref windowsAreaWest,
                        ref windowsAreaEast,
                        ref windowsAreaNorthwest,
                        ref windowsAreaNortheast,
                        ref windowsAreaSouthwest,
                        ref windowsAreaSoutheast);

                    if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Rooms")
                        TraceExcludedOuterCurtainWalls(linkDoc, targetTrace, "LinkedCurtainWalls");

                    TraceExcludedBasicWallCandidates(linkDoc, targetTrace, "LinkedGlazingWalls");
                    ProcessGlazingBasicWalls(
                        linkedGlazingBasicWallsList,
                        tr,
                        element,
                        elementSolid,
                        targetTrace,
                        "LinkedGlazingWalls",
                        trueNorthBasisX,
                        trueNorthBasisY,
                        ref windowsAreaNorth,
                        ref windowsAreaSouth,
                        ref windowsAreaWest,
                        ref windowsAreaEast,
                        ref windowsAreaNorthwest,
                        ref windowsAreaNortheast,
                        ref windowsAreaSouthwest,
                        ref windowsAreaSoutheast);

                    targetTrace.Totals = CreateDirectionalAreasTrace(
                        windowsAreaNorth, windowsAreaSouth, windowsAreaWest, windowsAreaEast,
                        windowsAreaNorthwest, windowsAreaNortheast, windowsAreaSouthwest, windowsAreaSoutheast);

                    SetParameterWithTrace(element, windowsAreaNorthGuid, windowsAreaNorth, targetTrace.ParameterWrites[0]);
                    SetParameterWithTrace(element, windowsAreaSouthGuid, windowsAreaSouth, targetTrace.ParameterWrites[1]);
                    SetParameterWithTrace(element, windowsAreaWestGuid, windowsAreaWest, targetTrace.ParameterWrites[2]);
                    SetParameterWithTrace(element, windowsAreaEastGuid, windowsAreaEast, targetTrace.ParameterWrites[3]);
                    SetParameterWithTrace(element, windowsAreaNorthwestGuid, windowsAreaNorthwest, targetTrace.ParameterWrites[4]);
                    SetParameterWithTrace(element, windowsAreaNortheastGuid, windowsAreaNortheast, targetTrace.ParameterWrites[5]);
                    SetParameterWithTrace(element, windowsAreaSouthwestGuid, windowsAreaSouthwest, targetTrace.ParameterWrites[6]);
                    SetParameterWithTrace(element, windowsAreaSoutheastGuid, windowsAreaSoutheast, targetTrace.ParameterWrites[7]);
                    targetTrace.Complete("Counted", "Completed");
                }

                t.Commit();
            }

            CompleteRunTrace(trace, "Succeeded", "Completed");
            return Result.Succeeded;
            }
            catch (Exception ex)
            {
                CompleteRunTrace(trace, "Failed", "UnhandledException", ex.ToString());
                throw;
            }
            finally
            {
                if (!CalculationTraceWriter.TryWrite(trace, out _, out string writeError))
                {
                    try
                    {
                        TaskDialog.Show("Revit", "Не удалось записать диагностический JSON-лог: " + writeError);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void CompleteRunTrace(CalculationTrace trace, string outcome, string reasonCode, string error = null)
        {
            trace.Outcome = outcome;
            trace.ReasonCode = reasonCode;
            trace.Error = error;
        }

        private static DocumentTrace CreateDocumentTrace(Document document)
        {
            if (document == null) return null;

            return new DocumentTrace
            {
                Title = document.Title,
                PathName = document.PathName
            };
        }

        private static LinkTrace CreateLinkTrace(RevitLinkInstance link, Document linkDocument, Transform transform)
        {
            return new LinkTrace
            {
                ElementId = link?.Id.ToString(),
                UniqueId = link?.UniqueId,
                Document = CreateDocumentTrace(linkDocument),
                Transform = CreateTransformTrace(transform)
            };
        }

        private static TransformTrace CreateTransformTrace(Transform transform)
        {
            if (transform == null) return null;

            return new TransformTrace
            {
                Origin = CreateTraceVector(transform.Origin),
                BasisX = CreateTraceVector(transform.BasisX),
                BasisY = CreateTraceVector(transform.BasisY),
                BasisZ = CreateTraceVector(transform.BasisZ)
            };
        }

        private static TraceVector CreateTraceVector(XYZ value)
        {
            return value == null ? null : new TraceVector(value.X, value.Y, value.Z);
        }

        private static TracePoint CreateTracePoint(string name, XYZ value)
        {
            return new TracePoint(name, value.X, value.Y, value.Z);
        }

        private static void AddTraceDetail(TraceStep step, string key, object value)
        {
            if (step != null)
            {
                step.Details[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static bool TraceSpatialMembership(TraceStep step, string pointName, Element targetElement, Document hostDocument, XYZ point)
        {
            try
            {
                bool inside = IsPointInSpatialElement(targetElement, point);
                step?.Details.Add(pointName + "Inside", inside.ToString());
                return inside;
            }
            catch (Exception ex)
            {
                step?.Details.Add("apiException", ex.ToString());
                throw;
            }
        }

        private static string GetSpatialNumber(Element element)
        {
            if (element is Space space) return space.Number;
            if (element is Room room) return room.Number;
            return null;
        }

        private static string GetSpatialName(Element element)
        {
            if (element is Space space) return space.Name;
            if (element is Room room) return room.Name;
            return null;
        }

        private static List<ParameterWriteTrace> CreateParameterWriteTraces(Element element, params Guid[] parameterGuids)
        {
            var traces = new List<ParameterWriteTrace>();
            foreach (Guid parameterGuid in parameterGuids)
            {
                Parameter parameter = element.get_Parameter(parameterGuid);
                traces.Add(new ParameterWriteTrace
                {
                    Guid = parameterGuid.ToString(),
                    Exists = parameter != null,
                    IsReadOnly = parameter?.IsReadOnly ?? false,
                    OldValue = parameter != null && parameter.StorageType == StorageType.Double ? parameter.AsDouble() : (double?)null
                });
            }

            return traces;
        }

        private static DirectionalAreasTrace CreateDirectionalAreasTrace(
            double north, double south, double west, double east,
            double northwest, double northeast, double southwest, double southeast)
        {
            return new DirectionalAreasTrace
            {
                North = north,
                South = south,
                West = west,
                East = east,
                Northwest = northwest,
                Northeast = northeast,
                Southwest = southwest,
                Southeast = southeast
            };
        }

        private static void SetParameterWithTrace(Element element, Guid parameterGuid, double value, ParameterWriteTrace trace)
        {
            trace.NewValue = value;
            Parameter parameter = element.get_Parameter(parameterGuid);
            if (parameter == null)
            {
                trace.SetSucceeded = null;
                return;
            }

            try
            {
                trace.SetSucceeded = parameter.Set(value);
            }
            catch (Exception ex)
            {
                trace.Error = ex.ToString();
                trace.SetSucceeded = false;
                throw;
            }
        }

        // Дополнительные методы для получения Solid, вычисления площадей и проверки панели
        private Solid GetSolidFromElement(Element element)
        {
            if (element == null) return null;

            Options opt = new Options();
            GeometryElement ge = element.get_Geometry(opt);
            if (ge == null) return null;

            foreach (GeometryObject obj in ge)
            {
                if (obj is Solid s && s.Volume > 0)
                    return s;

                if (obj is GeometryInstance gi)
                {
                    GeometryElement instGe = gi.GetInstanceGeometry();
                    if (instGe == null) continue;

                    foreach (GeometryObject instObj in instGe)
                    {
                        if (instObj is Solid si && si.Volume > 0)
                            return si;
                    }
                }
            }

            return null;
        }

        private double GetWindowArea(FamilyInstance window, SourceTrace trace = null)
        {
            TraceStep step = trace?.StartStep("Area");
            if (window?.Symbol == null)
            {
                step?.Details.Add("symbol", "null");
                return 0.0;
            }

            double roughHeight = window.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM)?.AsDouble() ?? 0.0;
            double roughWidth = window.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM)?.AsDouble() ?? 0.0;
            double caseworkHeight = window.Symbol.get_Parameter(BuiltInParameter.CASEWORK_HEIGHT)?.AsDouble() ?? 0.0;
            double caseworkWidth = window.Symbol.get_Parameter(BuiltInParameter.CASEWORK_WIDTH)?.AsDouble() ?? 0.0;
            double selectedHeight = Math.Max(roughHeight, caseworkHeight);
            double selectedWidth = Math.Max(roughWidth, caseworkWidth);
            double area = selectedHeight * selectedWidth;
            AddTraceDetail(step, "roughHeight", roughHeight);
            AddTraceDetail(step, "roughWidth", roughWidth);
            AddTraceDetail(step, "caseworkHeight", caseworkHeight);
            AddTraceDetail(step, "caseworkWidth", caseworkWidth);
            AddTraceDetail(step, "selectedHeight", selectedHeight);
            AddTraceDetail(step, "selectedWidth", selectedWidth);
            AddTraceDetail(step, "area", area);
            return area;
        }

        private void ProcessWindows(
            IEnumerable<FamilyInstance>? windows,
            Transform sourceToHostTransform,
            Element targetElement,
            Solid targetSolid,
            Document hostDocument,
            TargetTrace targetTrace,
            string sourcePass,
            XYZ trueNorthBasisX,
            XYZ trueNorthBasisY,
            ref double windowsAreaNorth,
            ref double windowsAreaSouth,
            ref double windowsAreaWest,
            ref double windowsAreaEast,
            ref double windowsAreaNorthwest,
            ref double windowsAreaNortheast,
            ref double windowsAreaSouthwest,
            ref double windowsAreaSoutheast)
        {
            if (windows == null || targetElement == null || targetSolid == null || hostDocument == null)
                return;

            Transform tr = sourceToHostTransform ?? Transform.Identity;

            foreach (FamilyInstance window in windows)
            {
                if (window == null)
                    continue;

                SourceTrace sourceTrace = targetTrace?.StartSource("Window", window.UniqueId);
                if (sourceTrace != null)
                {
                    sourceTrace.SourcePass = sourcePass;
                    sourceTrace.ElementId = window.Id.ToString();
                    sourceTrace.Document = CreateDocumentTrace(window.Document);
                    sourceTrace.SuperComponent = window.SuperComponent?.UniqueId;
                }

                double windowArea = GetWindowArea(window, sourceTrace);
                if (windowArea <= 0)
                {
                    sourceTrace?.Complete("Skipped", "NoArea");
                    continue;
                }

                if (!TryGetWindowExteriorDirection(window, tr, targetElement, targetSolid, hostDocument, sourceTrace, out XYZ exteriorDirection))
                {
                    sourceTrace?.Complete("Skipped", sourceTrace?.ReasonCode ?? "NoSpatialAssociation");
                    continue;
                }

                UpdateWindowAreas(
                    ref windowsAreaNorth,
                    ref windowsAreaSouth,
                    ref windowsAreaWest,
                    ref windowsAreaEast,
                    ref windowsAreaNorthwest,
                    ref windowsAreaNortheast,
                    ref windowsAreaSouthwest,
                    ref windowsAreaSoutheast,
                    windowArea,
                    exteriorDirection,
                    trueNorthBasisX,
                    trueNorthBasisY,
                    sourceTrace);

                sourceTrace?.Complete(sourceTrace?.Direction?.Accepted == true ? "Counted" : "Skipped", sourceTrace?.Direction?.Accepted == true ? sourceTrace.ReasonCode ?? "SpatialProbe" : "InvalidDirection");
            }
        }

        private bool TryGetWindowExteriorDirection(
            FamilyInstance window,
            Transform sourceToHostTransform,
            Element targetElement,
            Solid targetSolid,
            Document hostDocument,
            SourceTrace sourceTrace,
            out XYZ exteriorDirection)
        {
            exteriorDirection = null;

            if (window == null || targetElement == null || hostDocument == null)
                return false;

            Transform tr = sourceToHostTransform ?? Transform.Identity;
            BoundingBoxXYZ windowBoundingBox = window.get_BoundingBox(null);
            XYZ facing = tr.OfVector(window.FacingOrientation);
            bool isInteriorOpening = false;

            TraceStep geometryStep = sourceTrace?.StartStep("Geometry");
            geometryStep?.Points.Add(CreateTracePoint("facing", facing));
            if (windowBoundingBox == null)
            {
                geometryStep?.Details.Add("boundingBox", "null");
                sourceTrace?.Complete("Skipped", "NoBoundingBox");
            }
            else
            {
                XYZ center = tr.OfPoint((windowBoundingBox.Max + windowBoundingBox.Min) / 2);
                geometryStep?.Points.Add(CreateTracePoint("boundingBoxMin", tr.OfPoint(windowBoundingBox.Min)));
                geometryStep?.Points.Add(CreateTracePoint("boundingBoxMax", tr.OfPoint(windowBoundingBox.Max)));
                geometryStep?.Points.Add(CreateTracePoint("center", center));
            }
            if (facing.GetLength() <= 1e-9)
                sourceTrace?.Complete("Skipped", "NoOrientation");

            if (windowBoundingBox != null
                && TryGetExteriorDirectionFromSpatialProbes(
                    tr.OfPoint((windowBoundingBox.Max + windowBoundingBox.Min) / 2),
                    facing,
                    targetElement,
                    hostDocument,
                    sourceTrace,
                    out exteriorDirection,
                    out isInteriorOpening))
            {
                return true;
            }

            if (isInteriorOpening)
            {
                sourceTrace?.Complete("Skipped", "InteriorOpening");
                return false;
            }

            if (TryGetWindowExteriorDirectionFromCalculationPoints(
                window,
                sourceToHostTransform,
                targetElement,
                hostDocument,
                sourceTrace,
                out exteriorDirection,
                out isInteriorOpening))
            {
                return true;
            }

            if (isInteriorOpening)
            {
                sourceTrace?.Complete("Skipped", "InteriorOpening");
                return false;
            }

            bool fallback = TryGetWindowExteriorDirectionFromFallbackRay(
                window,
                sourceToHostTransform,
                targetSolid,
                sourceTrace,
                out exteriorDirection);
            if (fallback) sourceTrace.ReasonCode = "SolidRay";
            return fallback;
        }

        private bool TryGetWindowExteriorDirectionFromCalculationPoints(
            FamilyInstance window,
            Transform sourceToHostTransform,
            Element targetElement,
            Document hostDocument,
            SourceTrace sourceTrace,
            out XYZ exteriorDirection,
            out bool isInteriorOpening)
        {
            exteriorDirection = null;
            isInteriorOpening = false;

            if (window == null || targetElement == null || hostDocument == null)
                return false;

            TraceStep step = sourceTrace?.StartStep("CalculationPoints");
            bool hasCalculationPoints = window.HasSpatialElementFromToCalculationPoints;
            step?.Details.Add("hasSpatialElementFromToCalculationPoints", hasCalculationPoints.ToString());
            if (!hasCalculationPoints)
            {
                step?.Details.Add("pointCount", "notRequested");
                step?.Complete("Skipped", "NoCalculationPoints");
                return false;
            }

            IList<XYZ> points;
            try
            {
                points = window.GetSpatialElementFromToCalculationPoints();
            }
            catch (Exception ex)
            {
                step?.Details.Add("apiException", ex.ToString());
                step?.Complete("Skipped", "ApiException");
                return false;
            }

            step?.Details.Add("pointCount", points == null ? "null" : points.Count.ToString());
            if (points == null || points.Count < 2)
            {
                step?.Complete("Skipped", points == null ? "NoCalculationPoints" : "InsufficientCalculationPoints");
                return false;
            }

            Transform tr = sourceToHostTransform ?? Transform.Identity;

            XYZ pointA = tr.OfPoint(points[0]);
            XYZ pointB = tr.OfPoint(points[1]);
            step?.Points.Add(CreateTracePoint("rawPointA", points[0]));
            step?.Points.Add(CreateTracePoint("rawPointB", points[1]));
            step?.Points.Add(CreateTracePoint("pointA", pointA));
            step?.Points.Add(CreateTracePoint("pointB", pointB));

            bool pointAInside = TraceSpatialMembership(step, "pointA", targetElement, hostDocument, pointA);
            bool pointBInside = TraceSpatialMembership(step, "pointB", targetElement, hostDocument, pointB);

            if (pointAInside == pointBInside)
            {
                step?.Complete("Skipped", "EqualCalculationPointMembership");
                return false;
            }

            XYZ insidePoint = pointAInside ? pointA : pointB;
            XYZ outsidePoint = pointAInside ? pointB : pointA;

            if (IsPointInAnotherSpatialElement(hostDocument, targetElement, outsidePoint, step, "outside"))
            {
                isInteriorOpening = true;
                return false;
            }

            XYZ direction = outsidePoint - insidePoint;
            if (direction.GetLength() <= 1e-9)
            {
                step?.Complete("Skipped", "ZeroCalculationPointDirection");
                return false;
            }

            exteriorDirection = direction.Normalize();
            sourceTrace.ReasonCode = "CalculationPoints";
            step?.Complete("Accepted", "CalculationPoints");
            return true;
        }

        private bool TryGetExteriorDirectionFromSpatialProbes(
            XYZ center,
            XYZ facing,
            Element targetElement,
            Document hostDocument,
            SourceTrace sourceTrace,
            out XYZ exteriorDirection,
            out bool isInteriorOpening)
        {
            exteriorDirection = null;
            isInteriorOpening = false;

            if (center == null || facing == null || targetElement == null || hostDocument == null)
                return false;

            XYZ horizontalFacing = new XYZ(facing.X, facing.Y, 0);
            if (horizontalFacing.GetLength() <= 1e-9)
                return false;

            horizontalFacing = horizontalFacing.Normalize();

            foreach (double distance in new[] { 150.0 / 304.8, 300.0 / 304.8, 700.0 / 304.8 })
            {
                XYZ front = center + horizontalFacing * distance;
                XYZ back = center - horizontalFacing * distance;
                TraceStep step = sourceTrace?.StartStep("SpatialProbe");
                AddTraceDetail(step, "distanceMm", distance * 304.8);
                step?.Points.Add(CreateTracePoint("front", front));
                step?.Points.Add(CreateTracePoint("back", back));
                bool frontInside = TraceSpatialMembership(step, "front", targetElement, hostDocument, front);
                bool backInside = TraceSpatialMembership(step, "back", targetElement, hostDocument, back);

                if (frontInside == backInside)
                {
                    MarkOtherSpatialLookupNotEvaluated(step, "front");
                    MarkOtherSpatialLookupNotEvaluated(step, "back");
                    continue;
                }

                XYZ outsidePoint = frontInside ? back : front;
                string outsidePointName = frontInside ? "back" : "front";
                if (IsPointInAnotherSpatialElement(hostDocument, targetElement, outsidePoint, step, outsidePointName))
                {
                    isInteriorOpening = true;
                    return false;
                }

                exteriorDirection = frontInside ? horizontalFacing.Negate() : horizontalFacing;
                if (sourceTrace != null) sourceTrace.ReasonCode = "SpatialProbe";
                step?.Complete("Accepted", "SpatialProbe");
                return true;
            }

            return false;
        }

        private bool TryGetWindowExteriorDirectionFromFallbackRay(
            FamilyInstance window,
            Transform sourceToHostTransform,
            Solid targetSolid,
            SourceTrace sourceTrace,
            out XYZ exteriorDirection)
        {
            exteriorDirection = null;

            TraceStep step = sourceTrace?.StartStep("FallbackSolidRay");

            if (window == null || targetSolid == null)
            {
                step?.Complete("Skipped", "NoTargetSolid");
                return false;
            }

            BoundingBoxXYZ windowBoundingBox = window.get_BoundingBox(null);
            if (windowBoundingBox == null)
            {
                step?.Complete("Skipped", "NoBoundingBox");
                return false;
            }

            Transform tr = sourceToHostTransform ?? Transform.Identity;
            XYZ facing = tr.OfVector(window.FacingOrientation);
            if (facing.GetLength() <= 1e-9)
            {
                step?.Complete("Skipped", "NoOrientation");
                return false;
            }

            XYZ windowCenter = tr.OfPoint((windowBoundingBox.Max + windowBoundingBox.Min) / 2);
            Curve windowCurve = Line.CreateBound(windowCenter, windowCenter + facing.Negate() * 700 / 304.8);

            SolidCurveIntersection intersection;
            step?.Points.Add(CreateTracePoint("start", windowCenter));
            step?.Points.Add(CreateTracePoint("end", windowCenter + facing.Negate() * 700 / 304.8));
            try
            {
                intersection = targetSolid.IntersectWithCurve(windowCurve, new SolidCurveIntersectionOptions());
            }
            catch (Exception ex)
            {
                step?.Details.Add("apiException", ex.ToString());
                throw;
            }
            AddTraceDetail(step, "segmentCount", intersection.SegmentCount);
            if (intersection.SegmentCount == 0)
            {
                step?.Complete("Skipped", "NoSolidRayIntersection");
                return false;
            }

            exteriorDirection = facing;
            step?.Complete("Accepted", "SolidRay");
            return true;
        }

        private static bool IsPointInSpatialElement(Element targetElement, XYZ point)
        {
            return targetElement switch
            {
                Room room => room.IsPointInRoom(point),
                Space space => space.IsPointInSpace(point),
                _ => false
            };
        }

        private static bool IsPointInAnotherSpatialElement(Document hostDocument, Element targetElement, XYZ point, TraceStep traceStep = null, string pointName = null)
        {
            try
            {
                Phase? phase = GetElementPhase(hostDocument, targetElement);

                if (targetElement is Room)
                {
                    Room containingRoom = phase != null
                        ? hostDocument.GetRoomAtPoint(point, phase)
                        : hostDocument.GetRoomAtPoint(point);

                    TraceOtherSpatialElement(traceStep, pointName, containingRoom);
                    bool isAnotherRoom = containingRoom != null && containingRoom.Id != targetElement.Id;
                    TraceOtherSpatialLookupResult(traceStep, pointName, isAnotherRoom);
                    return isAnotherRoom;
                }

                if (targetElement is Space)
                {
                    Space containingSpace = phase != null
                        ? hostDocument.GetSpaceAtPoint(point, phase)
                        : hostDocument.GetSpaceAtPoint(point);

                    TraceOtherSpatialElement(traceStep, pointName, containingSpace);
                    bool isAnotherSpace = containingSpace != null && containingSpace.Id != targetElement.Id;
                    TraceOtherSpatialLookupResult(traceStep, pointName, isAnotherSpace);
                    return isAnotherSpace;
                }

                return false;
            }
            catch (Exception ex)
            {
                traceStep?.Details.Add("apiException", ex.ToString());
                throw;
            }
        }

        private static void TraceOtherSpatialElement(TraceStep step, string pointName, Element containingElement)
        {
            if (step == null) return;
            step.Details[(pointName ?? "point") + "OtherSpatialElementId"] = containingElement?.Id.ToString() ?? string.Empty;
            step.Details[(pointName ?? "point") + "OtherSpatialElementUniqueId"] = containingElement?.UniqueId ?? string.Empty;
            step.Details[(pointName ?? "point") + "OtherSpatialLookupOutcome"] = "Evaluated";
        }

        private static void TraceOtherSpatialLookupResult(TraceStep step, string pointName, bool isAnother)
        {
            if (step == null) return;
            step.Details[(pointName ?? "point") + "IsAnotherSpatialElement"] = isAnother.ToString();
        }

        private static void MarkOtherSpatialLookupNotEvaluated(TraceStep step, string pointName)
        {
            if (step == null) return;
            step.Details[pointName + "OtherSpatialLookupOutcome"] = "NotEvaluated";
            step.Details[pointName + "OtherSpatialLookupReason"] = "EqualMembershipNoOutsidePoint";
        }

        private static Phase? GetElementPhase(Document hostDocument, Element element)
        {
            if (hostDocument == null || element == null)
                return null;

            ElementId phaseId = element.CreatedPhaseId;
            if (phaseId == ElementId.InvalidElementId)
                return null;

            return hostDocument.GetElement(phaseId) as Phase;
        }

        /// <summary>
        /// Универсальная проверка: является ли заполнитель витражной ячейки остеклением.
        /// Поддерживает стандартные Curtain Panels и случай, когда панель заменена на базовую стену.
        /// </summary>
        private bool IsCurtainGridFillGlazing(Element fill, SourceTrace sourceTrace = null)
        {
            TraceStep step = sourceTrace?.StartStep("GlazingClassification");
            if (fill == null)
            {
                step?.Complete("Skipped", "NoFill");
                return false;
            }

            AddTraceDetail(step, "elementKind", fill.GetType().Name);
            AddTraceDetail(step, "elementId", fill.Id);

            // В CurtainGrid почти всегда приходит Panel, даже если "подставили стену"
            if (fill is Panel panel)
            {
                // 1) Классика: тип конструкции на типе панели
                string constructionType = panel.Symbol?
                    .get_Parameter(BuiltInParameter.CURTAIN_WALL_PANELS_CONSTRUCTION_TYPE)?
                    .AsString();
                AddTraceDetail(step, "constructionType", constructionType ?? "null");

                if (IsGlazingMarker(constructionType))
                {
                    step?.Complete("Accepted", "ConstructionTypeGlazingMarker");
                    return true;
                }

                // 2) Доп.критерий: "Группа модели" на типе панели (если кто-то так настроил)
                string panelTypeModelGroup = panel.Symbol?
                    .get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?
                    .AsString();
                AddTraceDetail(step, "panelModelGroup", panelTypeModelGroup ?? "null");

                if (IsGlazingMarker(panelTypeModelGroup))
                {
                    step?.Complete("Accepted", "PanelModelGroupGlazingMarker");
                    return true;
                }

                // 3) Главное для вашего случая: хост-элемент (стена), вставленный в ячейку
                Element host = null;
                try
                {
                    ElementId hostId = panel.FindHostPanel();
                    if (hostId != null && hostId != ElementId.InvalidElementId)
                        host = panel.Document.GetElement(hostId);
                    AddTraceDetail(step, "hostPanelId", hostId);
                }
                catch (Exception ex)
                {
                    AddTraceDetail(step, "findHostPanelError", ex.ToString());
                }

                if (host != null && host.Id != panel.Id)
                {
                    string hostTypeModelGroup = GetTypeModelGroup(host);
                    AddTraceDetail(step, "hostModelGroup", hostTypeModelGroup ?? "null");
                    bool hostGlazingMarker = IsGlazingMarker(hostTypeModelGroup);
                    AddTraceDetail(step, "hostGlazingMarker", hostGlazingMarker);
                    if (hostGlazingMarker)
                    {
                        step?.Complete("Accepted", "HostModelGroupGlazingMarker");
                        return true;
                    }
                }
                else
                {
                    AddTraceDetail(step, "hostGlazingMarker", false);
                }

                step?.Complete("Skipped", "NotGlazingMarker");
                return false;
            }

            // Фолбэк: если вдруг реально пришла Wall (редко)
            if (fill is Wall wall)
            {
                string modelGroup = wall.WallType?
                    .get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?
                    .AsString();
                AddTraceDetail(step, "wallModelGroup", modelGroup ?? "null");
                bool accepted = IsGlazingMarker(modelGroup);
                step?.Complete(accepted ? "Accepted" : "Skipped", accepted ? "WallModelGroupGlazingMarker" : "NotGlazingMarker");
                return accepted;
            }

            step?.Complete("Skipped", "UnsupportedFillType");
            return false;
        }

        /// <summary>
        /// Возвращает "Группа модели" (ALL_MODEL_MODEL) с ТИПА элемента.
        /// </summary>
        private string GetTypeModelGroup(Element element)
        {
            if (element == null) return null;

            Document doc = element.Document;
            ElementId typeId = element.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return null;

            ElementType type = doc.GetElement(typeId) as ElementType;
            return type?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString();
        }

        /// <summary>
        /// "Остекление" или "Остекления" (без чувствительности к регистру/пробелам).
        /// </summary>
        private static bool IsGlazingMarker(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim();

            return string.Equals(v, CurtainPanelConstructionTypeGlazing, StringComparison.OrdinalIgnoreCase)
                || string.Equals(v, BasicWallModelGroupGlazing, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Витраж с «Группа модели» типа стены = «Наружный витраж» (как в исходной логике для помещений).
        /// </summary>
        private static bool IsOuterCurtainWallByModelGroup(Wall wall)
        {
            string? mg = wall.WallType?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString();
            return string.Equals(mg, "Наружный витраж", StringComparison.Ordinal);
        }

        /// <summary>
        /// Базовые стены без сетки витража с маркером остекления в типе (в т.ч. замена панели витража на Wall).
        /// </summary>
        private static List<Wall> CollectGlazingBasicWalls(Document? document)
        {
            if (document == null) return new List<Wall>();

            return new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.CurtainGrid == null)
                .Where(w => IsGlazingMarker(w.WallType?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString()))
                .ToList();
        }

        /// <summary>
        /// Получить "ориентацию" заполнителя (нормаль наружу).
        /// Для Panel — FacingOrientation, для Wall — Orientation.
        /// </summary>
        private bool TryGetFillFacingOrientation(Element fill, out XYZ facing)
        {
            facing = null;

            if (fill is Panel p)
            {
                facing = p.FacingOrientation;
                return facing != null;
            }

            if (fill is Wall w)
            {
                // Orientation — нормаль к оси стены в плоскости XY
                facing = w.Orientation;
                return facing != null;
            }

            return false;
        }

        /// <summary>
        /// Площадь заполнителя по HOST_AREA_COMPUTED (как у вас было для Panel).
        /// </summary>
        private double GetFillHostArea(Element fill)
        {
            if (fill == null) return 0.0;

            Parameter p = fill.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (p == null || p.StorageType != StorageType.Double) return 0.0;

            return p.AsDouble();
        }

        private static int CountCurtainWallsExcludedByOuterModelGroup(Document document)
        {
            if (document == null) return 0;
            return new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Count(w => w.CurtainGrid != null && !IsOuterCurtainWallByModelGroup(w));
        }

        // Только диагностика исключений фильтра «Наружный витраж» для режима помещений.
        private static void TraceExcludedOuterCurtainWalls(Document document, TargetTrace targetTrace, string sourcePass)
        {
            if (document == null || targetTrace == null) return;
            foreach (Wall wall in new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.CurtainGrid != null && !IsOuterCurtainWallByModelGroup(w)))
            {
                SourceTrace trace = StartFillTrace(targetTrace, sourcePass, "CurtainWallCandidate", wall);
                TraceStep step = trace?.StartStep("OuterCurtainWallFilter");
                AddTraceDetail(step, "hasCurtainGrid", true);
                AddTraceDetail(step, "modelGroup", wall.WallType?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString() ?? "null");
                trace?.Complete("Skipped", "NotOuterCurtainWall");
            }
        }

        // Только диагностика: список, используемый расчётом, не изменяется.
        private static void TraceExcludedBasicWallCandidates(Document document, TargetTrace targetTrace, string sourcePass)
        {
            if (document == null || targetTrace == null) return;

            foreach (Wall wall in new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>())
            {
                bool hasCurtainGrid = wall.CurtainGrid != null;
                string modelGroup = wall.WallType?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString();
                if (!hasCurtainGrid && IsGlazingMarker(modelGroup)) continue;

                SourceTrace trace = StartFillTrace(targetTrace, sourcePass, "GlazingWallCandidate", wall);
                TraceStep step = trace?.StartStep("Candidate");
                AddTraceDetail(step, "hasCurtainGrid", hasCurtainGrid);
                AddTraceDetail(step, "modelGroup", modelGroup ?? "null");
                trace?.Complete("Skipped", hasCurtainGrid ? "CurtainGridWall" : "NotGlazingMarker");
            }
        }

        private static SourceTrace StartFillTrace(TargetTrace targetTrace, string sourcePass, string sourceType, Element element)
        {
            SourceTrace trace = targetTrace?.StartSource(sourceType, element?.UniqueId);
            if (trace == null) return null;

            trace.SourcePass = sourcePass;
            trace.ElementId = element?.Id.ToString();
            trace.Document = element?.Document == null ? null : CreateDocumentTrace(element.Document);
            return trace;
        }

        private static void TraceFillGeometry(SourceTrace sourceTrace, BoundingBoxXYZ boundingBox, Transform transform, XYZ facingLocal)
        {
            TraceStep step = sourceTrace?.StartStep("Geometry");
            if (boundingBox == null)
            {
                step?.Complete("Skipped", "NoBoundingBox");
                return;
            }

            Transform tr = transform ?? Transform.Identity;
            XYZ center = tr.OfPoint((boundingBox.Min + boundingBox.Max) / 2);
            step?.Points.Add(CreateTracePoint("boundingBoxMin", tr.OfPoint(boundingBox.Min)));
            step?.Points.Add(CreateTracePoint("boundingBoxMax", tr.OfPoint(boundingBox.Max)));
            step?.Points.Add(CreateTracePoint("center", center));
            step?.Points.Add(CreateTracePoint("facing", tr.OfVector(facingLocal)));
            step?.Complete("Recorded", "GeometryAvailable");
        }

        private static bool TryGetExteriorDirectionFromBidirectionalSolidRays(
            XYZ center, XYZ facing, Solid targetSolid, SourceTrace sourceTrace, out XYZ exteriorDirection)
        {
            exteriorDirection = null;
            TraceStep step = sourceTrace?.StartStep("FallbackSolidRays");
            if (center == null || facing == null || targetSolid == null || facing.GetLength() <= 1e-9)
            {
                step?.Complete("Skipped", targetSolid == null ? "NoTargetSolid" : "NoOrientation");
                return false;
            }

            double rayLength = 700.0 / 304.8;
            XYZ forwardEnd = center + facing * rayLength;
            XYZ backwardEnd = center - facing * rayLength;
            step?.Points.Add(CreateTracePoint("forwardStart", center));
            step?.Points.Add(CreateTracePoint("forwardEnd", forwardEnd));
            step?.Points.Add(CreateTracePoint("backwardStart", center));
            step?.Points.Add(CreateTracePoint("backwardEnd", backwardEnd));

            try
            {
                SolidCurveIntersection forward = targetSolid.IntersectWithCurve(Line.CreateBound(center, forwardEnd), new SolidCurveIntersectionOptions());
                AddTraceDetail(step, "forwardSegmentCount", forward.SegmentCount);
                if (forward.SegmentCount > 0)
                {
                    exteriorDirection = facing.Negate();
                    step?.Points.Add(CreateTracePoint("selectedExteriorDirection", exteriorDirection));
                    AddTraceDetail(step, "backwardRay", "NotEvaluatedForwardAccepted");
                    step?.Complete("Accepted", "ForwardSolidRay");
                    return true;
                }

                SolidCurveIntersection backward = targetSolid.IntersectWithCurve(Line.CreateBound(center, backwardEnd), new SolidCurveIntersectionOptions());
                AddTraceDetail(step, "backwardSegmentCount", backward.SegmentCount);
                if (backward.SegmentCount > 0)
                {
                    exteriorDirection = facing;
                    step?.Points.Add(CreateTracePoint("selectedExteriorDirection", exteriorDirection));
                    step?.Complete("Accepted", "BackwardSolidRay");
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddTraceDetail(step, "apiException", ex.ToString());
                step?.Complete("Skipped", "ApiException");
                throw;
            }

            step?.Complete("Skipped", "NoSolidRayIntersection");
            return false;
        }

        /// <summary>
        /// Площадь базовых стен-остекления (без витражной сетки): та же геометрия луча, что и для заполнений витража.
        /// </summary>
        private void ProcessGlazingBasicWalls(
            IEnumerable<Wall>? walls,
            Transform sourceToHostTransform,
            Element targetElement,
            Solid elementSolid,
            TargetTrace targetTrace,
            string sourcePass,
            XYZ trueNorthBasisX,
            XYZ trueNorthBasisY,
            ref double windowsAreaNorth,
            ref double windowsAreaSouth,
            ref double windowsAreaWest,
            ref double windowsAreaEast,
            ref double windowsAreaNorthwest,
            ref double windowsAreaNortheast,
            ref double windowsAreaSouthwest,
            ref double windowsAreaSoutheast)
        {
            if (walls == null || elementSolid == null) return;

            Transform tr = sourceToHostTransform ?? Transform.Identity;

            foreach (Wall wall in walls)
            {
                SourceTrace sourceTrace = StartFillTrace(targetTrace, sourcePass, "GlazingWall", wall);
                TraceStep candidateStep = sourceTrace?.StartStep("Candidate");
                AddTraceDetail(candidateStep, "hasCurtainGrid", wall?.CurtainGrid != null);
                string modelGroup = wall?.WallType?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString();
                AddTraceDetail(candidateStep, "modelGroup", modelGroup ?? "null");
                if (wall == null || wall.CurtainGrid != null)
                {
                    sourceTrace?.Complete("Skipped", "CurtainGridWall");
                    continue;
                }
                if (!IsGlazingMarker(modelGroup))
                {
                    sourceTrace?.Complete("Skipped", "NotGlazingMarker");
                    continue;
                }

                if (!TryGetFillFacingOrientation(wall, out XYZ facingLocal))
                {
                    sourceTrace?.Complete("Skipped", "NoOrientation");
                    continue;
                }

                BoundingBoxXYZ bb;
                try
                {
                    bb = wall.get_BoundingBox(null);
                }
                catch (Exception ex)
                {
                    AddTraceDetail(candidateStep, "elementBoundingBoxError", ex.ToString());
                    sourceTrace?.Complete("Skipped", "ApiException");
                    throw;
                }
                AddTraceDetail(candidateStep, "boundingBoxRoute", "ElementBoundingBox");
                TraceFillGeometry(sourceTrace, bb, tr, facingLocal);
                if (bb == null)
                {
                    sourceTrace?.Complete("Skipped", "NoBoundingBox");
                    continue;
                }

                XYZ centerLocal = (bb.Max + bb.Min) / 2;
                XYZ center = tr.OfPoint(centerLocal);
                XYZ facing = tr.OfVector(facingLocal);

                double area = GetFillHostArea(wall);
                TraceStep areaStep = sourceTrace?.StartStep("Area");
                AddTraceDetail(areaStep, "hostAreaComputedRaw", area);
                AddTraceDetail(areaStep, "hostAreaComputedFinal", area);
                if (area <= 0)
                {
                    sourceTrace?.Complete("Skipped", "NoArea");
                    continue;
                }

                if (TryGetExteriorDirectionFromSpatialProbes(
                    center,
                    facing,
                    targetElement,
                    targetElement.Document,
                    sourceTrace,
                    out XYZ exteriorDirection,
                    out bool isInteriorOpening))
                {
                    UpdateWindowAreas(
                        ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                        ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                        area, exteriorDirection, trueNorthBasisX, trueNorthBasisY, sourceTrace);
                    sourceTrace?.Complete(sourceTrace.Direction?.Accepted == true ? "Counted" : "Skipped", sourceTrace.ReasonCode ?? "SpatialProbe");
                    continue;
                }

                if (isInteriorOpening)
                {
                    sourceTrace?.Complete("Skipped", "InteriorOpening");
                    continue;
                }

                if (TryGetExteriorDirectionFromBidirectionalSolidRays(center, facing, elementSolid, sourceTrace, out XYZ exteriorDirectionFromRay))
                {
                    sourceTrace.ReasonCode = "SolidRay";
                    UpdateWindowAreas(ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                        ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                        area, exteriorDirectionFromRay, trueNorthBasisX, trueNorthBasisY, sourceTrace);
                    sourceTrace?.Complete(sourceTrace.Direction?.Accepted == true ? "Counted" : "Skipped", sourceTrace.ReasonCode);
                    continue;
                }

                sourceTrace?.Complete("Skipped", "NoSpatialAssociation");
            }
        }

        /// <summary>
        /// Общая обработка заполнений витражных стен (в текущем документе или в связи).
        /// sourceToHostTransform:
        /// - Transform.Identity для текущего документа
        /// - RevitLinkInstance.GetTotalTransform() для связи
        /// </summary>
        
        private void ProcessCurtainWallFills(
            Document sourceDoc,
            IEnumerable<Wall> curtainWalls,
            Transform sourceToHostTransform,
            Element targetElement,
            Solid elementSolid,
            TargetTrace targetTrace,
            string sourcePass,
            XYZ trueNorthBasisX,
            XYZ trueNorthBasisY,
            ref double windowsAreaNorth,
            ref double windowsAreaSouth,
            ref double windowsAreaWest,
            ref double windowsAreaEast,
            ref double windowsAreaNorthwest,
            ref double windowsAreaNortheast,
            ref double windowsAreaSouthwest,
            ref double windowsAreaSoutheast)
        {
            if (sourceDoc == null) return;
            if (curtainWalls == null) return;
            if (elementSolid == null) return;

            Transform tr = sourceToHostTransform ?? Transform.Identity;

            foreach (Wall wall in curtainWalls)
            {
                CurtainGrid grid = wall?.CurtainGrid;
                if (grid == null)
                {
                    SourceTrace missingGridTrace = StartFillTrace(targetTrace, sourcePass, "CurtainWall", wall);
                    missingGridTrace?.StartStep("CurtainGrid")?.Complete("Skipped", "NoCurtainGrid");
                    missingGridTrace?.Complete("Skipped", "NoCurtainGrid");
                    continue;
                }

                SourceTrace curtainWallTrace = StartFillTrace(targetTrace, sourcePass, "CurtainWall", wall);
                TraceStep curtainWallStep = curtainWallTrace?.StartStep("CurtainGrid");
                AddTraceDetail(curtainWallStep, "hasCurtainGrid", true);
                AddTraceDetail(curtainWallStep, "modelGroup", wall.WallType?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString() ?? "null");
                curtainWallStep?.Complete("Accepted", "CurtainWallIncluded");
                curtainWallTrace?.Complete("Accepted", "CurtainWallIncluded");

                foreach (ElementId panelId in grid.GetPanelIds())
                {
                    Element fill = sourceDoc.GetElement(panelId);
                    SourceTrace sourceTrace = StartFillTrace(targetTrace, sourcePass, "CurtainGridFill", fill);
                    TraceStep panelStep = sourceTrace?.StartStep("CurtainGridFill");
                    AddTraceDetail(panelStep, "hostCurtainWallId", wall.Id);
                    AddTraceDetail(panelStep, "panelId", panelId);
                    AddTraceDetail(panelStep, "fillFound", fill != null);
                    AddTraceDetail(panelStep, "fillRuntimeType", fill?.GetType().Name ?? "null");
                    if (fill == null)
                    {
                        sourceTrace?.Complete("Skipped", "NoFill");
                        continue;
                    }

                    // Фильтрация остекления (Panel или Wall)
                    if (!IsCurtainGridFillGlazing(fill, sourceTrace))
                    {
                        sourceTrace?.Complete("Skipped", "NotGlazingMarker");
                        continue;
                    }

                    // Ориентация заполнителя
                    if (!TryGetFillFacingOrientation(fill, out XYZ facingLocal))
                    {
                        sourceTrace?.Complete("Skipped", "NoOrientation");
                        continue;
                    }

                    if (!TryGetFillBoundingBox(sourceDoc, fill, sourceTrace, out BoundingBoxXYZ bb))
                    {
                        sourceTrace?.Complete("Skipped", "NoBoundingBox");
                        continue;
                    }

                    TraceFillGeometry(sourceTrace, bb, tr, facingLocal);

                    XYZ centerLocal = (bb.Max + bb.Min) / 2;

                    // Приводим геометрию к координатам хоста (для связи) либо оставляем как есть (для текущего дока)
                    XYZ center = tr.OfPoint(centerLocal);
                    XYZ facing = tr.OfVector(facingLocal);

                    double area = GetFillHostArea(fill);
                    TraceStep areaStep = sourceTrace?.StartStep("Area");
                    AddTraceDetail(areaStep, "hostAreaComputedRaw", area);
                    AddTraceDetail(areaStep, "hostAreaComputedFinal", area);
                    if (area <= 0)
                    {
                        sourceTrace?.Complete("Skipped", "NoArea");
                        continue;
                    }

                    if (TryGetExteriorDirectionFromSpatialProbes(
                        center,
                        facing,
                    targetElement,
                    targetElement.Document,
                    sourceTrace,
                    out XYZ exteriorDirection,
                        out bool isInteriorOpening))
                    {
                        UpdateWindowAreas(
                            ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                            ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                            area, exteriorDirection, trueNorthBasisX, trueNorthBasisY, sourceTrace);
                        sourceTrace?.Complete(sourceTrace.Direction?.Accepted == true ? "Counted" : "Skipped", sourceTrace.ReasonCode ?? "SpatialProbe");
                        continue;
                    }

                    if (isInteriorOpening)
                    {
                        sourceTrace?.Complete("Skipped", "InteriorOpening");
                        continue;
                    }

                    if (TryGetExteriorDirectionFromBidirectionalSolidRays(center, facing, elementSolid, sourceTrace, out XYZ exteriorDirectionFromRay))
                    {
                        sourceTrace.ReasonCode = "SolidRay";
                        UpdateWindowAreas(ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                            ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                            area, exteriorDirectionFromRay, trueNorthBasisX, trueNorthBasisY, sourceTrace);
                        sourceTrace?.Complete(sourceTrace.Direction?.Accepted == true ? "Counted" : "Skipped", sourceTrace.ReasonCode);
                        continue;
                    }

                    sourceTrace?.Complete("Skipped", "NoSpatialAssociation");
                }
            }
        }

        private bool TryGetFillBoundingBox(Document sourceDoc, Element fill, SourceTrace sourceTrace, out BoundingBoxXYZ bb)
        {
            bb = null;
            TraceStep step = sourceTrace?.StartStep("BoundingBox");
            if (fill == null)
            {
                step?.Complete("Skipped", "NoFill");
                return false;
            }

            // 1) Пробуем BB самого элемента (быстро)
            try { bb = fill.get_BoundingBox(null); }
            catch (Exception ex)
            {
                AddTraceDetail(step, "elementBoundingBoxError", ex.ToString());
                step?.Complete("Skipped", "ApiException");
                throw;
            }
            if (bb != null)
            {
                step?.Complete("Accepted", "ElementBoundingBox");
                return true;
            }

            // 2) Частый кейс: fill = Panel, а реальная "начинка" сидит в host (например, стена)
            if (fill is Panel panel)
            {
                try
                {
                    ElementId hostId = panel.FindHostPanel(); // у вас это ElementId
                    AddTraceDetail(step, "hostPanelId", hostId);
                    if (hostId != null && hostId != ElementId.InvalidElementId)
                    {
                        Element host = sourceDoc.GetElement(hostId);
                        bb = host?.get_BoundingBox(null);
                        if (bb != null)
                        {
                            step?.Complete("Accepted", "HostBoundingBox");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddTraceDetail(step, "hostBoundingBoxError", ex.ToString());
                }
            }

            // 3) (Опционально) Фолбэк: пытаемся получить BB через геометрию (медленнее, но иногда спасает)
            Options opt = new Options
            {
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement ge;
            try
            {
                ge = fill.get_Geometry(opt);
            }
            catch (Exception ex)
            {
                AddTraceDetail(step, "geometryBoundingBoxError", ex.ToString());
                step?.Complete("Skipped", "ApiException");
                throw;
            }
            if (ge == null)
            {
                step?.Complete("Skipped", "NoBoundingBox");
                return false;
            }

            XYZ min = null, max = null;

            void Expand(BoundingBoxXYZ b)
            {
                if (b == null) return;

                if (min == null)
                {
                    min = b.Min;
                    max = b.Max;
                    return;
                }

                min = new XYZ(
                    Math.Min(min.X, b.Min.X),
                    Math.Min(min.Y, b.Min.Y),
                    Math.Min(min.Z, b.Min.Z));

                max = new XYZ(
                    Math.Max(max.X, b.Max.X),
                    Math.Max(max.Y, b.Max.Y),
                    Math.Max(max.Z, b.Max.Z));
            }

            try
            {
                foreach (GeometryObject obj in ge)
                {
                    if (obj is Solid s && s.Volume > 0)
                    {
                        Expand(s.GetBoundingBox());
                    }
                    else if (obj is GeometryInstance gi)
                    {
                        GeometryElement inst = gi.GetInstanceGeometry();
                        if (inst == null) continue;

                        foreach (GeometryObject io in inst)
                        {
                            if (io is Solid si && si.Volume > 0)
                                Expand(si.GetBoundingBox());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddTraceDetail(step, "geometryBoundingBoxError", ex.ToString());
                step?.Complete("Skipped", "ApiException");
                throw;
            }

            if (min == null)
            {
                step?.Complete("Skipped", "NoBoundingBox");
                return false;
            }

            bb = new BoundingBoxXYZ { Min = min, Max = max };
            step?.Complete("Accepted", "GeometryBoundingBox");
            return true;
        }

        private void UpdateWindowAreas(
            ref double north, ref double south, ref double west, ref double east,
            ref double northwest, ref double northeast, ref double southwest, ref double southeast,
            double area, XYZ orientation, XYZ trueNorthBasisX, XYZ trueNorthBasisY, SourceTrace sourceTrace = null)
        {
            DirectionTrace directionTrace = sourceTrace == null ? null : new DirectionTrace
            {
                ExteriorVector = CreateTraceVector(orientation),
                EastBasis = CreateTraceVector(trueNorthBasisX),
                NorthBasis = CreateTraceVector(trueNorthBasisY),
                Area = area
            };
            if (!CardinalDirectionClassifier.TryClassify(
                orientation.X,
                orientation.Y,
                trueNorthBasisX.X,
                trueNorthBasisX.Y,
                trueNorthBasisY.X,
                trueNorthBasisY.Y,
                out CardinalDirectionBucket bucket))
            {
                if (directionTrace != null)
                {
                    directionTrace.Accepted = false;
                    sourceTrace.Direction = directionTrace;
                }
                return;
            }

            double before;
            double after;

            switch (bucket)
            {
                case CardinalDirectionBucket.North:
                    before = north;
                    north += area;
                    after = north;
                    break;
                case CardinalDirectionBucket.South:
                    before = south;
                    south += area;
                    after = south;
                    break;
                case CardinalDirectionBucket.West:
                    before = west;
                    west += area;
                    after = west;
                    break;
                case CardinalDirectionBucket.East:
                    before = east;
                    east += area;
                    after = east;
                    break;
                case CardinalDirectionBucket.Northwest:
                    before = northwest;
                    northwest += area;
                    after = northwest;
                    break;
                case CardinalDirectionBucket.Northeast:
                    before = northeast;
                    northeast += area;
                    after = northeast;
                    break;
                case CardinalDirectionBucket.Southwest:
                    before = southwest;
                    southwest += area;
                    after = southwest;
                    break;
                case CardinalDirectionBucket.Southeast:
                    before = southeast;
                    southeast += area;
                    after = southeast;
                    break;
                default:
                    return;
            }

            if (directionTrace != null)
            {
                directionTrace.Accepted = true;
                directionTrace.Bucket = bucket.ToString();
                directionTrace.BucketValueBefore = before;
                directionTrace.BucketValueAfter = after;
                sourceTrace.Direction = directionTrace;
            }
        }

        private static List<Space> GetSpacesFromCurrentSelection(Document doc, Selection sel)
        {
            ICollection<ElementId> selectedIds = sel.GetElementIds();
            List<Space> tempSpacessList = new List<Space>();
            foreach (ElementId roomId in selectedIds)
            {
                if (doc.GetElement(roomId) is Space space)
                {
                    tempSpacessList.Add(space);
                }
            }
            return tempSpacessList;
        }

        private static List<Room> GetRoomsFromCurrentSelection(Document doc, Selection sel)
        {
            ICollection<ElementId> selectedIds = sel.GetElementIds();
            List<Room> tempRoomsList = new List<Room>();
            foreach (ElementId roomId in selectedIds)
            {
                if (doc.GetElement(roomId) is Room room)
                {
                    tempRoomsList.Add(room);
                }
            }
            return tempRoomsList;
        }

        private static async Task GetPluginStartInfo()
        {
            // Получаем сборку, в которой выполняется текущий код
            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            string assemblyName = "CardinalDirectionGlazing";
            string assemblyNameRus = "Остекление по сторонам";
            string assemblyFolderPath = Path.GetDirectoryName(thisAssembly.Location);

            int lastBackslashIndex = assemblyFolderPath.LastIndexOf("\\");
            string dllPath = assemblyFolderPath.Substring(0, lastBackslashIndex + 1) + "PluginInfoCollector\\PluginInfoCollector.dll";

            Assembly assembly = Assembly.LoadFrom(dllPath);
            Type type = assembly.GetType("PluginInfoCollector.InfoCollector");

            if (type != null)
            {
                // Создание экземпляра класса
                object instance = Activator.CreateInstance(type);

                // Получение метода CollectPluginUsageAsync
                var method = type.GetMethod("CollectPluginUsageAsync");

                if (method != null)
                {
                    // Вызов асинхронного метода через reflection
                    Task task = (Task)method.Invoke(instance, new object[] { assemblyName, assemblyNameRus });
                    await task;  // Ожидание завершения асинхронного метода
                }
            }
        }
    }
}
