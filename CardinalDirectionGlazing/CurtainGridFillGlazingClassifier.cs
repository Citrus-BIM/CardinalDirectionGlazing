using Autodesk.Revit.DB;
using System;
using System.Globalization;

namespace CardinalDirectionGlazing
{
    internal static class CurtainGridFillGlazingClassifier
    {
        private const string CurtainPanelConstructionTypeGlazing = "Остекление";
        private const string BasicWallModelGroupGlazing = "Остекления";

        public static bool IsGlazing(Element fill, SourceTrace sourceTrace = null)
        {
            TraceStep step = sourceTrace?.StartStep("GlazingClassification");
            if (fill == null)
            {
                step?.Complete("Skipped", "NoFill");
                return false;
            }

            AddTraceDetail(step, "elementKind", fill.GetType().Name);
            AddTraceDetail(step, "elementId", fill.Id);

            if (fill is Panel panel)
            {
                string constructionType = panel.Symbol?
                    .get_Parameter(BuiltInParameter.CURTAIN_WALL_PANELS_CONSTRUCTION_TYPE)?
                    .AsString();
                AddTraceDetail(step, "constructionType", constructionType ?? "null");
                if (IsGlazingMarker(constructionType))
                {
                    step?.Complete("Accepted", "ConstructionTypeGlazingMarker");
                    return true;
                }

                string panelTypeModelGroup = panel.Symbol?
                    .get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?
                    .AsString();
                AddTraceDetail(step, "panelModelGroup", panelTypeModelGroup ?? "null");
                if (IsGlazingMarker(panelTypeModelGroup))
                {
                    step?.Complete("Accepted", "PanelModelGroupGlazingMarker");
                    return true;
                }

                Element host = null;
                try
                {
                    ElementId hostId = panel.FindHostPanel();
                    if (hostId != null && hostId != ElementId.InvalidElementId)
                        host = panel.Document?.GetElement(hostId);
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

            if (fill is Wall wall)
            {
                string modelGroup = wall.WallType?
                    .get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?
                    .AsString();
                AddTraceDetail(step, "wallModelGroup", modelGroup ?? "null");
                bool accepted = IsGlazingMarker(modelGroup);
                step?.Complete(
                    accepted ? "Accepted" : "Skipped",
                    accepted ? "WallModelGroupGlazingMarker" : "NotGlazingMarker");
                return accepted;
            }

            step?.Complete("Skipped", "UnsupportedFillType");
            return false;
        }

        private static string GetTypeModelGroup(Element element)
        {
            if (element == null)
                return null;

            Document document = element.Document;
            ElementId typeId = element.GetTypeId();
            if (document == null || typeId == null || typeId == ElementId.InvalidElementId)
                return null;

            ElementType type = document.GetElement(typeId) as ElementType;
            return type?.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString();
        }

        private static bool IsGlazingMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim();
            return string.Equals(
                    normalized,
                    CurtainPanelConstructionTypeGlazing,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    normalized,
                    BasicWallModelGroupGlazing,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void AddTraceDetail(TraceStep step, string key, object value)
        {
            if (step != null)
                step.Details[key] = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
