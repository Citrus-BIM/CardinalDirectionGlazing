namespace CardinalDirectionGlazing
{
    public enum CurtainPanelAreaParameterScope
    {
        Instance,
        Type
    }

    public sealed class CurtainPanelAreaParameterOption
    {
        public string Name { get; set; } = string.Empty;
        public CurtainPanelAreaParameterScope Scope { get; set; }
        public string SharedGuid { get; set; } = string.Empty;

        public string DisplayName => Name + (Scope == CurtainPanelAreaParameterScope.Instance
            ? " (экземпляр)"
            : " (тип)");
    }
}
