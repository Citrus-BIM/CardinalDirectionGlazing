namespace CardinalDirectionGlazing
{
    public enum WindowAreaParameterScope
    {
        Instance,
        Type
    }

    public sealed class WindowAreaParameterOption
    {
        public string Name { get; set; } = string.Empty;
        public WindowAreaParameterScope Scope { get; set; }
        public string SharedGuid { get; set; } = string.Empty;

        public string DisplayName => Name + (Scope == WindowAreaParameterScope.Instance
            ? " (экземпляр)"
            : " (тип)");
    }
}
