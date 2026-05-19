using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public sealed class MGColorSwatch
    {
        public string Name { get; set; }
        public ColorValue Value { get; set; }
        public Dictionary<string, string> Metadata { get; }

        public MGColorSwatch(string name, ColorValue value)
        {
            Name = name ?? string.Empty;
            Value = value;
            Metadata = new Dictionary<string, string>();
        }
    }
}