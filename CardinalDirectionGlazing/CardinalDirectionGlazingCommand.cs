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
            try
            {
                _ = GetPluginStartInfo();
            }
            catch { }

            Document doc = commandData.Application.ActiveUIDocument.Document;
            Selection sel = commandData.Application.ActiveUIDocument.Selection;

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
                return Result.Cancelled;
            }

            // Проверка выбранного связанного файла
            if (cardinalDirectionGlazingWPF.SpacesOrRoomsForProcessingButtonName == "radioButton_Spaces")
            {
                // Если выбрана обработка пространств, связанный файл обязателен
                if (cardinalDirectionGlazingWPF.SelectedRevitLinkInstance == null)
                {
                    TaskDialog.Show("Revit", "Связанный файл не выбран! Для обработки пространств необходим связанный файл.");
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
            }

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
                return Result.Cancelled;
            }

            // Проверка наличия параметров в первом элементе
            if (elementsList.Count != 0)
            {
                Element firstElement = elementsList.First();

                // Проверка каждого параметра по отдельности и вывод сообщения, если параметр отсутствует
                if (firstElement.get_Parameter(windowsAreaNorthGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_С\" не найден! Добавьте параметр.");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaSouthGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_Ю\" не найден! Добавьте параметр.");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaWestGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_З\" не найден! Добавьте параметр.");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaEastGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_В\" не найден! Добавьте параметр.");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaNorthwestGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_СЗ\" не найден! Добавьте параметр.");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaNortheastGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_СВ\" не найден! Добавьте параметр.");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaSouthwestGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_ЮЗ\" не найден! Добавьте параметр.");
                    return Result.Cancelled;
                }

                if (firstElement.get_Parameter(windowsAreaSoutheastGuid) == null)
                {
                    TaskDialog.Show("Revit", "Параметр \"ПлощадьОкон_ЮВ\" не найден! Добавьте параметр.");
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

            // Начинаем транзакцию для обновления данных в Revit
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Остекление по сторонам света");

                foreach (Element element in elementsList)
                {
                    double windowsAreaNorth = 0;
                    double windowsAreaSouth = 0;
                    double windowsAreaWest = 0;
                    double windowsAreaEast = 0;
                    double windowsAreaNorthwest = 0;
                    double windowsAreaNortheast = 0;
                    double windowsAreaSouthwest = 0;
                    double windowsAreaSoutheast = 0;

                    Solid elementSolid = GetSolidFromElement(element);
                    if (elementSolid == null) continue;

                    Transform tr = transform ?? Transform.Identity;

                    ProcessWindows(
                        windowsList,
                        Transform.Identity,
                        element,
                        elementSolid,
                        doc,
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

                    ProcessGlazingBasicWalls(
                        glazingBasicWallsList,
                        Transform.Identity,
                        element,
                        elementSolid,
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

                    ProcessGlazingBasicWalls(
                        linkedGlazingBasicWallsList,
                        tr,
                        element,
                        elementSolid,
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

                    // Установка значений параметров для каждого элемента
                    element.get_Parameter(windowsAreaNorthGuid)?.Set(windowsAreaNorth);
                    element.get_Parameter(windowsAreaSouthGuid)?.Set(windowsAreaSouth);
                    element.get_Parameter(windowsAreaWestGuid)?.Set(windowsAreaWest);
                    element.get_Parameter(windowsAreaEastGuid)?.Set(windowsAreaEast);
                    element.get_Parameter(windowsAreaNorthwestGuid)?.Set(windowsAreaNorthwest);
                    element.get_Parameter(windowsAreaNortheastGuid)?.Set(windowsAreaNortheast);
                    element.get_Parameter(windowsAreaSouthwestGuid)?.Set(windowsAreaSouthwest);
                    element.get_Parameter(windowsAreaSoutheastGuid)?.Set(windowsAreaSoutheast);
                }

                t.Commit();
            }

            return Result.Succeeded;
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

        private double GetWindowArea(FamilyInstance window)
        {
            if (window?.Symbol == null) return 0.0;

            double roughHeight = window.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM)?.AsDouble() ?? 0.0;
            double roughWidth = window.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM)?.AsDouble() ?? 0.0;
            double caseworkHeight = window.Symbol.get_Parameter(BuiltInParameter.CASEWORK_HEIGHT)?.AsDouble() ?? 0.0;
            double caseworkWidth = window.Symbol.get_Parameter(BuiltInParameter.CASEWORK_WIDTH)?.AsDouble() ?? 0.0;

            return Math.Max(roughHeight, caseworkHeight) * Math.Max(roughWidth, caseworkWidth);
        }

        private void ProcessWindows(
            IEnumerable<FamilyInstance>? windows,
            Transform sourceToHostTransform,
            Element targetElement,
            Solid targetSolid,
            Document hostDocument,
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

                double windowArea = GetWindowArea(window);
                if (windowArea <= 0)
                    continue;

                if (!TryGetWindowExteriorDirection(window, tr, targetElement, targetSolid, hostDocument, out XYZ exteriorDirection))
                    continue;

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
                    trueNorthBasisY);
            }
        }

        private bool TryGetWindowExteriorDirection(
            FamilyInstance window,
            Transform sourceToHostTransform,
            Element targetElement,
            Solid targetSolid,
            Document hostDocument,
            out XYZ exteriorDirection)
        {
            exteriorDirection = null;

            if (window == null || targetElement == null || hostDocument == null)
                return false;

            Transform tr = sourceToHostTransform ?? Transform.Identity;
            BoundingBoxXYZ windowBoundingBox = window.get_BoundingBox(null);
            XYZ facing = tr.OfVector(window.FacingOrientation);
            bool isInteriorOpening = false;

            if (windowBoundingBox != null
                && TryGetExteriorDirectionFromSpatialProbes(
                    tr.OfPoint((windowBoundingBox.Max + windowBoundingBox.Min) / 2),
                    facing,
                    targetElement,
                    hostDocument,
                    out exteriorDirection,
                    out isInteriorOpening))
            {
                return true;
            }

            if (isInteriorOpening)
                return false;

            if (TryGetWindowExteriorDirectionFromCalculationPoints(
                window,
                sourceToHostTransform,
                targetElement,
                hostDocument,
                out exteriorDirection,
                out isInteriorOpening))
            {
                return true;
            }

            if (isInteriorOpening)
                return false;

            return TryGetWindowExteriorDirectionFromFallbackRay(
                window,
                sourceToHostTransform,
                targetSolid,
                out exteriorDirection);
        }

        private bool TryGetWindowExteriorDirectionFromCalculationPoints(
            FamilyInstance window,
            Transform sourceToHostTransform,
            Element targetElement,
            Document hostDocument,
            out XYZ exteriorDirection,
            out bool isInteriorOpening)
        {
            exteriorDirection = null;
            isInteriorOpening = false;

            if (window == null || targetElement == null || hostDocument == null)
                return false;

            if (!window.HasSpatialElementFromToCalculationPoints)
                return false;

            IList<XYZ> points;
            try
            {
                points = window.GetSpatialElementFromToCalculationPoints();
            }
            catch
            {
                return false;
            }

            if (points == null || points.Count < 2)
                return false;

            Transform tr = sourceToHostTransform ?? Transform.Identity;

            XYZ pointA = tr.OfPoint(points[0]);
            XYZ pointB = tr.OfPoint(points[1]);

            bool pointAInside = IsPointInSpatialElement(targetElement, pointA);
            bool pointBInside = IsPointInSpatialElement(targetElement, pointB);

            if (pointAInside == pointBInside)
                return false;

            XYZ insidePoint = pointAInside ? pointA : pointB;
            XYZ outsidePoint = pointAInside ? pointB : pointA;

            if (IsPointInAnotherSpatialElement(hostDocument, targetElement, outsidePoint))
            {
                isInteriorOpening = true;
                return false;
            }

            XYZ direction = outsidePoint - insidePoint;
            if (direction.GetLength() <= 1e-9)
                return false;

            exteriorDirection = direction.Normalize();
            return true;
        }

        private bool TryGetExteriorDirectionFromSpatialProbes(
            XYZ center,
            XYZ facing,
            Element targetElement,
            Document hostDocument,
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
                bool frontInside = IsPointInSpatialElement(targetElement, front);
                bool backInside = IsPointInSpatialElement(targetElement, back);

                if (frontInside == backInside)
                    continue;

                XYZ outsidePoint = frontInside ? back : front;
                if (IsPointInAnotherSpatialElement(hostDocument, targetElement, outsidePoint))
                {
                    isInteriorOpening = true;
                    return false;
                }

                exteriorDirection = frontInside ? horizontalFacing.Negate() : horizontalFacing;
                return true;
            }

            return false;
        }

        private bool TryGetWindowExteriorDirectionFromFallbackRay(
            FamilyInstance window,
            Transform sourceToHostTransform,
            Solid targetSolid,
            out XYZ exteriorDirection)
        {
            exteriorDirection = null;

            if (window == null || targetSolid == null)
                return false;

            BoundingBoxXYZ windowBoundingBox = window.get_BoundingBox(null);
            if (windowBoundingBox == null)
                return false;

            Transform tr = sourceToHostTransform ?? Transform.Identity;
            XYZ facing = tr.OfVector(window.FacingOrientation);
            if (facing.GetLength() <= 1e-9)
                return false;

            XYZ windowCenter = tr.OfPoint((windowBoundingBox.Max + windowBoundingBox.Min) / 2);
            Curve windowCurve = Line.CreateBound(windowCenter, windowCenter + facing.Negate() * 700 / 304.8);

            SolidCurveIntersection intersection = targetSolid.IntersectWithCurve(windowCurve, new SolidCurveIntersectionOptions());
            if (intersection.SegmentCount == 0)
                return false;

            exteriorDirection = facing;
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

        private static bool IsPointInAnotherSpatialElement(Document hostDocument, Element targetElement, XYZ point)
        {
            Phase? phase = GetElementPhase(hostDocument, targetElement);

            if (targetElement is Room)
            {
                Room containingRoom = phase != null
                    ? hostDocument.GetRoomAtPoint(point, phase)
                    : hostDocument.GetRoomAtPoint(point);

                return containingRoom != null && containingRoom.Id != targetElement.Id;
            }

            if (targetElement is Space)
            {
                Space containingSpace = phase != null
                    ? hostDocument.GetSpaceAtPoint(point, phase)
                    : hostDocument.GetSpaceAtPoint(point);

                return containingSpace != null && containingSpace.Id != targetElement.Id;
            }

            return false;
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
        private bool IsCurtainGridFillGlazing(Element fill)
        {
            if (fill == null) return false;

            // В CurtainGrid почти всегда приходит Panel, даже если "подставили стену"
            if (fill is Panel panel)
            {
                // 1) Классика: тип конструкции на типе панели
                string constructionType = panel.Symbol?
                    .get_Parameter(BuiltInParameter.CURTAIN_WALL_PANELS_CONSTRUCTION_TYPE)?
                    .AsString();

                if (IsGlazingMarker(constructionType))
                    return true;

                // 2) Доп.критерий: "Группа модели" на типе панели (если кто-то так настроил)
                string panelTypeModelGroup = panel.Symbol?
                    .get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?
                    .AsString();

                if (IsGlazingMarker(panelTypeModelGroup))
                    return true;

                // 3) Главное для вашего случая: хост-элемент (стена), вставленный в ячейку
                Element host = null;
                try
                {
                    ElementId hostId = panel.FindHostPanel();
                    if (hostId != null && hostId != ElementId.InvalidElementId)
                        host = panel.Document.GetElement(hostId);
                }
                catch
                {
                    // на всякий случай: FindHostPanel может бросить исключение в редких случаях
                }

                if (host != null && host.Id != panel.Id)
                {
                    string hostTypeModelGroup = GetTypeModelGroup(host);
                    if (IsGlazingMarker(hostTypeModelGroup))
                        return true;
                }

                return false;
            }

            // Фолбэк: если вдруг реально пришла Wall (редко)
            if (fill is Wall wall)
            {
                string modelGroup = wall.WallType?
                    .get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?
                    .AsString();

                return IsGlazingMarker(modelGroup);
            }

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

        /// <summary>
        /// Площадь базовых стен-остекления (без витражной сетки): та же геометрия луча, что и для заполнений витража.
        /// </summary>
        private void ProcessGlazingBasicWalls(
            IEnumerable<Wall>? walls,
            Transform sourceToHostTransform,
            Element targetElement,
            Solid elementSolid,
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
                if (wall == null || wall.CurtainGrid != null) continue;

                if (!TryGetFillFacingOrientation(wall, out XYZ facingLocal)) continue;

                BoundingBoxXYZ bb = wall.get_BoundingBox(null);
                if (bb == null) continue;

                XYZ centerLocal = (bb.Max + bb.Min) / 2;
                XYZ center = tr.OfPoint(centerLocal);
                XYZ facing = tr.OfVector(facingLocal);

                double area = GetFillHostArea(wall);
                if (area <= 0) continue;

                if (TryGetExteriorDirectionFromSpatialProbes(
                    center,
                    facing,
                    targetElement,
                    targetElement.Document,
                    out XYZ exteriorDirection,
                    out bool isInteriorOpening))
                {
                    UpdateWindowAreas(
                        ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                        ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                        area, exteriorDirection, trueNorthBasisX, trueNorthBasisY);
                    continue;
                }

                if (isInteriorOpening) continue;

                double rayLen = 700 / 304.8;
                Curve curveForward = Line.CreateBound(center, center + facing * rayLen);
                Curve curveBackward = Line.CreateBound(center, center + facing.Negate() * rayLen);

                SolidCurveIntersectionOptions opt = new SolidCurveIntersectionOptions();

                SolidCurveIntersection interForward = elementSolid.IntersectWithCurve(curveForward, opt);
                bool intersected = false;

                if (interForward.SegmentCount > 0)
                {
                    UpdateWindowAreas(
                        ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                        ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                        area, facing.Negate(), trueNorthBasisX, trueNorthBasisY);
                    intersected = true;
                }

                if (!intersected)
                {
                    SolidCurveIntersection interBackward = elementSolid.IntersectWithCurve(curveBackward, opt);
                    if (interBackward.SegmentCount > 0)
                    {
                        UpdateWindowAreas(
                            ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                            ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                            area, facing, trueNorthBasisX, trueNorthBasisY);
                    }
                }
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
                if (grid == null) continue;

                foreach (ElementId panelId in grid.GetPanelIds())
                {
                    Element fill = sourceDoc.GetElement(panelId);
                    if (fill == null) continue;

                    // Фильтрация остекления (Panel или Wall)
                    if (!IsCurtainGridFillGlazing(fill)) continue;

                    // Ориентация заполнителя
                    if (!TryGetFillFacingOrientation(fill, out XYZ facingLocal)) continue;

                    if (!TryGetFillBoundingBox(sourceDoc, fill, out BoundingBoxXYZ bb))
                        continue;

                    XYZ centerLocal = (bb.Max + bb.Min) / 2;

                    // Приводим геометрию к координатам хоста (для связи) либо оставляем как есть (для текущего дока)
                    XYZ center = tr.OfPoint(centerLocal);
                    XYZ facing = tr.OfVector(facingLocal);

                    // Две линии: вперёд по facing и назад (как у вас)
                    double rayLen = 700 / 304.8;
                    Curve curveForward = Line.CreateBound(center, center + facing * rayLen);
                    Curve curveBackward = Line.CreateBound(center, center + facing.Negate() * rayLen);

                    SolidCurveIntersectionOptions opt = new SolidCurveIntersectionOptions();

                    SolidCurveIntersection interForward = elementSolid.IntersectWithCurve(curveForward, opt);
                    bool intersected = false;

                    double area = GetFillHostArea(fill);
                    if (area <= 0) continue;

                    if (TryGetExteriorDirectionFromSpatialProbes(
                        center,
                        facing,
                        targetElement,
                        targetElement.Document,
                        out XYZ exteriorDirection,
                        out bool isInteriorOpening))
                    {
                        UpdateWindowAreas(
                            ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                            ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                            area, exteriorDirection, trueNorthBasisX, trueNorthBasisY);
                        continue;
                    }

                    if (isInteriorOpening) continue;

                    if (interForward.SegmentCount > 0)
                    {
                        // ВАЖНО: сохраняем вашу логику со сменой знака orientation в forward-ветке
                        UpdateWindowAreas(
                            ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                            ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                            area, facing.Negate(), trueNorthBasisX, trueNorthBasisY);

                        intersected = true;
                    }

                    if (!intersected)
                    {
                        SolidCurveIntersection interBackward = elementSolid.IntersectWithCurve(curveBackward, opt);
                        if (interBackward.SegmentCount > 0)
                        {
                            UpdateWindowAreas(
                                ref windowsAreaNorth, ref windowsAreaSouth, ref windowsAreaWest, ref windowsAreaEast,
                                ref windowsAreaNorthwest, ref windowsAreaNortheast, ref windowsAreaSouthwest, ref windowsAreaSoutheast,
                                area, facing, trueNorthBasisX, trueNorthBasisY);
                        }
                    }
                }
            }
        }

        private bool TryGetFillBoundingBox(Document sourceDoc, Element fill, out BoundingBoxXYZ bb)
        {
            bb = null;
            if (fill == null) return false;

            // 1) Пробуем BB самого элемента (быстро)
            bb = fill.get_BoundingBox(null);
            if (bb != null) return true;

            // 2) Частый кейс: fill = Panel, а реальная "начинка" сидит в host (например, стена)
            if (fill is Panel panel)
            {
                try
                {
                    ElementId hostId = panel.FindHostPanel(); // у вас это ElementId
                    if (hostId != null && hostId != ElementId.InvalidElementId)
                    {
                        Element host = sourceDoc.GetElement(hostId);
                        bb = host?.get_BoundingBox(null);
                        if (bb != null) return true;
                    }
                }
                catch
                {
                    // игнорируем: FindHostPanel может быть недоступен/кинуть исключение в редких случаях
                }
            }

            // 3) (Опционально) Фолбэк: пытаемся получить BB через геометрию (медленнее, но иногда спасает)
            Options opt = new Options
            {
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement ge = fill.get_Geometry(opt);
            if (ge == null) return false;

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

            if (min == null) return false;

            bb = new BoundingBoxXYZ { Min = min, Max = max };
            return true;
        }

        private void UpdateWindowAreas(
            ref double north, ref double south, ref double west, ref double east,
            ref double northwest, ref double northeast, ref double southwest, ref double southeast,
            double area, XYZ orientation, XYZ trueNorthBasisX, XYZ trueNorthBasisY)
        {
            if (!CardinalDirectionClassifier.TryClassify(
                orientation.X,
                orientation.Y,
                trueNorthBasisX.X,
                trueNorthBasisX.Y,
                trueNorthBasisY.X,
                trueNorthBasisY.Y,
                out CardinalDirectionBucket bucket))
            {
                return;
            }

            switch (bucket)
            {
                case CardinalDirectionBucket.North:
                    north += area;
                    break;
                case CardinalDirectionBucket.South:
                    south += area;
                    break;
                case CardinalDirectionBucket.West:
                    west += area;
                    break;
                case CardinalDirectionBucket.East:
                    east += area;
                    break;
                case CardinalDirectionBucket.Northwest:
                    northwest += area;
                    break;
                case CardinalDirectionBucket.Northeast:
                    northeast += area;
                    break;
                case CardinalDirectionBucket.Southwest:
                    southwest += area;
                    break;
                case CardinalDirectionBucket.Southeast:
                    southeast += area;
                    break;
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
