using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public sealed class MGPropertyGridCategoryModel
    {
        public string Name { get; init; }
        public IReadOnlyList<MGPropertyGridDescriptor> Descriptors { get; init; }
        public bool IsCollapsed { get; set; }
    }
}