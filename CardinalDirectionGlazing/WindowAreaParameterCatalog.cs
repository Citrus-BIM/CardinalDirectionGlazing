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

            var options = new Dictionary<string, WindowAreaParameterOption>(StringComparer.OrdinalIgnoreCase);
            foreach (Document document in documents)
            {
                IEnumerable<FamilyInstance> windows = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>();

                foreach (FamilyInstance window in windows)
                {
                    AddParameters(window, WindowAreaParameterScope.Instance, options);
                    if (window.Symbol != null)
                        AddParameters(window.Symbol, WindowAreaParameterScope.Type, options);
                }
            }

            return options.Values
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Scope)
                .ToList();
        }

        private static void AddParameters(
            Element element,
            WindowAreaParameterScope scope,
            IDictionary<string, WindowAreaParameterOption> options)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                Definition? definition = parameter?.Definition;
                if (parameter == null
                    || parameter.StorageType != StorageType.Double
                    || definition == null
                    || string.IsNullOrWhiteSpace(definition.Name)
                    || !IsAreaDefinition(definition))
                    continue;

                string key = scope + "|" + definition.Name;
                if (!options.TryGetValue(key, out WindowAreaParameterOption? option))
                {
                    option = new WindowAreaParameterOption
                    {
                        Name = definition.Name,
                        Scope = scope
                    };
                    options.Add(key, option);
                }

                if (option.SharedGuid.Length == 0 && parameter.IsShared)
                {
                    try
                    {
                        option.SharedGuid = parameter.GUID.ToString("D");
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
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
