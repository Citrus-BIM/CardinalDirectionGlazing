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
                catch (Exception)
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
                    .ToList();
            }
            catch (Exception)
            {
                return;
            }

            foreach (Wall wall in curtainWalls)
            {
                if (!TryGetCurtainGrid(wall, out ICollection<ElementId> panelIds))
                    continue;

                List<ElementId> stablePanelIds;
                try
                {
                    stablePanelIds = panelIds.ToList();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (ElementId panelId in stablePanelIds)
                {
                    if (!TryGetGlazingPanel(document, panelId, out Panel panel))
                    {
                        continue;
                    }

                    try
                    {
                        if (!processedPanels.Add(panel.Id))
                            continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    string documentKey = GetDocumentKey(document);
                    AddParameters(
                        documentKey,
                        panel,
                        CurtainPanelAreaParameterScope.Instance,
                        observations);

                    FamilySymbol symbol;
                    try
                    {
                        symbol = panel.Symbol;
                        if (symbol != null && processedTypes.Add(symbol.Id))
                        {
                            AddParameters(
                                documentKey,
                                symbol,
                                CurtainPanelAreaParameterScope.Type,
                                observations);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static bool TryGetCurtainGrid(
            Wall wall,
            out ICollection<ElementId> panelIds)
        {
            panelIds = null;
            try
            {
                CurtainGrid grid = wall?.CurtainGrid;
                if (grid == null)
                    return false;

                panelIds = grid.GetPanelIds();
                return panelIds != null;
            }
            catch (Exception)
            {
                panelIds = null;
                return false;
            }
        }

        private static bool TryGetGlazingPanel(
            Document document,
            ElementId panelId,
            out Panel result)
        {
            result = null;
            try
            {
                if (document == null
                    || panelId == null
                    || panelId == ElementId.InvalidElementId)
                {
                    return false;
                }

                Element fill = document.GetElement(panelId);
                if (!(fill is Panel panel))
                    return false;

                if (!CurtainGridFillGlazingClassifier.IsGlazing(panel))
                    return false;

                result = panel;
                return true;
            }
            catch (Exception)
            {
                result = null;
                return false;
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

            ParameterSet parameters;
            try
            {
                parameters = element.Parameters;
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                foreach (Parameter parameter in parameters)
                {
                    TryAddParameter(documentKey, parameter, scope, observations);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void TryAddParameter(
            string documentKey,
            Parameter parameter,
            CurtainPanelAreaParameterScope scope,
            ICollection<CurtainPanelAreaParameterObservation> observations)
        {
            try
            {
                Definition definition = parameter?.Definition;
                if (parameter == null
                    || definition == null
                    || parameter.StorageType != StorageType.Double
                    || !IsAreaDefinition(definition))
                {
                    return;
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
            catch (Exception)
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
            catch (Exception)
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
            catch (Exception)
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
