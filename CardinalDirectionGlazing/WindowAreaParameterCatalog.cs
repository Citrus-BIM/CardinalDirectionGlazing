using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    internal static class WindowAreaParameterCatalog
    {
        public static IReadOnlyList<WindowAreaParameterOption> Collect(
            Document currentDocument,
            IEnumerable<RevitLinkInstance> links)
        {
            var documents = new List<Document> { currentDocument };
            foreach (RevitLinkInstance link in links ?? Enumerable.Empty<RevitLinkInstance>())
            {
                Document? linked = link?.GetLinkDocument();
                if (linked != null && !documents.Any(item => ReferenceEquals(item, linked)))
                    documents.Add(linked);
            }

            var observations = new List<WindowAreaParameterObservation>();
            foreach (Document document in documents)
            {
                IEnumerable<FamilyInstance> windows = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>();

                foreach (FamilyInstance window in windows)
                {
                    AddParameters(document.Title, window, WindowAreaParameterScope.Instance, observations);
                    if (window.Symbol != null)
                        AddParameters(document.Title, window.Symbol, WindowAreaParameterScope.Type, observations);
                }
            }

            return WindowAreaParameterCatalogBuilder.Build(observations);
        }

        private static void AddParameters(
            string documentKey,
            Element element,
            WindowAreaParameterScope scope,
            ICollection<WindowAreaParameterObservation> observations)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                Definition? definition = parameter?.Definition;
                if (parameter == null || definition == null)
                    continue;

                string sharedGuid = string.Empty;
                if (parameter.IsShared)
                {
                    try
                    {
                        sharedGuid = parameter.GUID.ToString("D");
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                observations.Add(new WindowAreaParameterObservation
                {
                    DocumentKey = documentKey,
                    Name = definition.Name,
                    Scope = scope,
                    SharedGuid = sharedGuid,
                    IsArea = IsAreaDefinition(definition),
                    IsDouble = parameter.StorageType == StorageType.Double
                });
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
