using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    internal static class CurtainPanelAreaParameterCatalog
    {
        public static IReadOnlyList<CurtainPanelAreaParameterOption> Collect(
            Document currentDocument,
            IEnumerable<RevitLinkInstance> links)
        {
            if (currentDocument == null)
                return Array.Empty<CurtainPanelAreaParameterOption>();

            var documents = new List<Document> { currentDocument };
            foreach (RevitLinkInstance link in links ?? Enumerable.Empty<RevitLinkInstance>())
            {
                try
                {
                    Document linkedDocument = link?.GetLinkDocument();
                    if (linkedDocument != null
                        && !documents.Any(item => ReferenceEquals(item, linkedDocument)))
                    {
                        documents.Add(linkedDocument);
                    }
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                }
            }

            var observations = new List<CurtainPanelAreaParameterObservation>();
            foreach (Document document in documents)
                CollectDocument(document, observations);

            return CurtainPanelAreaParameterCatalogBuilder.Build(observations);
        }

        private static void CollectDocument(
            Document document,
            ICollection<CurtainPanelAreaParameterObservation> observations)
        {
            if (document == null)
                return;

            var processedPanels = new HashSet<ElementId>();
            var processedTypes = new HashSet<ElementId>();
            IEnumerable<Wall> curtainWalls;
            try
            {
                curtainWalls = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .OfClass(typeof(Wall))
                    .WhereElementIsNotElementType()
                    .Cast<Wall>()
                    .Where(wall => wall.CurtainGrid != null)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return;
            }

            foreach (Wall wall in curtainWalls)
            {
                CurtainGrid grid;
                ICollection<ElementId> panelIds;
                try
                {
                    grid = wall?.CurtainGrid;
                    panelIds = grid?.GetPanelIds();
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    continue;
                }

                if (grid == null || panelIds == null)
                    continue;

                foreach (ElementId panelId in panelIds)
                {
                    if (panelId == null || panelId == ElementId.InvalidElementId)
                        continue;

                    Element fill;
                    try
                    {
                        fill = document.GetElement(panelId);
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        continue;
                    }

                    if (!(fill is Panel panel)
                        || !CurtainGridFillGlazingClassifier.IsGlazing(panel)
                        || !processedPanels.Add(panel.Id))
                    {
                        continue;
                    }

                    string documentKey = GetDocumentKey(document);
                    AddParameters(
                        documentKey,
                        panel,
                        CurtainPanelAreaParameterScope.Instance,
                        observations);

                    FamilySymbol symbol = panel.Symbol;
                    if (symbol != null && processedTypes.Add(symbol.Id))
                    {
                        AddParameters(
                            documentKey,
                            symbol,
                            CurtainPanelAreaParameterScope.Type,
                            observations);
                    }
                }
            }
        }

        private static void AddParameters(
            string documentKey,
            Element element,
            CurtainPanelAreaParameterScope scope,
            ICollection<CurtainPanelAreaParameterObservation> observations)
        {
            if (element == null)
                return;

            try
            {
                foreach (Parameter parameter in element.Parameters)
                {
                    Definition definition = parameter?.Definition;
                    if (parameter == null
                        || definition == null
                        || parameter.StorageType != StorageType.Double
                        || !IsAreaDefinition(definition))
                    {
                        continue;
                    }

                    observations.Add(new CurtainPanelAreaParameterObservation
                    {
                        DocumentKey = documentKey,
                        Name = definition.Name,
                        Scope = scope,
                        SharedGuid = GetSharedGuid(parameter),
                        IsArea = true,
                        IsDouble = true
                    });
                }
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }
        }

        private static string GetSharedGuid(Parameter parameter)
        {
            if (parameter == null || !parameter.IsShared)
                return string.Empty;

            try
            {
                return parameter.GUID.ToString("D");
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
        }

        private static string GetDocumentKey(Document document)
        {
            try
            {
                return document.PathName + "|" + document.Title;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return string.Empty;
            }
        }

        private static bool IsAreaDefinition(Definition definition)
        {
#if REVIT_2019 || REVIT_2020 || REVIT_2021
            return definition.ParameterType == ParameterType.Area;
#else
            return definition.GetDataType() == SpecTypeId.Area;
#endif
        }
    }
}
