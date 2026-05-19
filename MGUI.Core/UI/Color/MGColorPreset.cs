namespace MGUI.Core.UI
{
    public enum MGColorPresetCategory
    {
        Material,
        Emissive,
        Light,
        Fog,
        Sky,
        UITheme,
        DebugGizmo,
        Temperature,
    }

    public sealed class MGColorPreset
    {
        public MGColorPresetCategory Category { get; }
        public string Name { get; }
        public ColorValue Value { get; }

        public MGColorPreset(MGColorPresetCategory category, string name, ColorValue value)
        {
            Category = category;
            Name = name ?? string.Empty;
            Value = value;
        }
    }
}