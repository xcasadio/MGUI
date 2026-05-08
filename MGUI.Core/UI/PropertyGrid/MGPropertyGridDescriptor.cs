using System;

namespace MGUI.Core.UI
{
    public sealed class MGPropertyGridDescriptor
    {
        public string Name { get; init; }
        public string DisplayName { get; init; }
        public string Category { get; init; }
        public Type PropertyType { get; init; }
        public MGPropertyGridEditorKind EditorKind { get; init; }
        public Func<object, object> Getter { get; init; }
        public Action<object, object> Setter { get; init; }
        public bool IsReadOnly { get; init; }
    }
}