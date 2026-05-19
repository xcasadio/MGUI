using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public sealed class MGColorPalette
    {
        public string Name { get; set; }
        public List<MGColorSwatch> Swatches { get; }

        public MGColorPalette(string name)
        {
            Name = name ?? string.Empty;
            Swatches = new List<MGColorSwatch>();
        }

        public MGColorSwatch AddSwatch(string name, ColorValue value)
        {
            MGColorSwatch swatch = new(name, value);
            Swatches.Add(swatch);
            return swatch;
        }

        public bool RemoveSwatch(MGColorSwatch swatch)
            => swatch != null && Swatches.Remove(swatch);

        public bool MoveSwatch(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Swatches.Count || newIndex < 0 || newIndex >= Swatches.Count || oldIndex == newIndex)
            {
                return false;
            }

            MGColorSwatch swatch = Swatches[oldIndex];
            Swatches.RemoveAt(oldIndex);
            Swatches.Insert(newIndex, swatch);
            return true;
        }

        public MGColorSwatch AddOrMoveToFront(string name, ColorValue value, int maxCount)
        {
            int existingIndex = Swatches.FindIndex(x => x.Value == value);
            MGColorSwatch swatch;
            if (existingIndex >= 0)
            {
                swatch = Swatches[existingIndex];
                swatch.Name = name ?? swatch.Name;
                Swatches.RemoveAt(existingIndex);
            }
            else
            {
                swatch = new MGColorSwatch(name, value);
            }

            Swatches.Insert(0, swatch);
            int limit = Math.Max(0, maxCount);
            while (Swatches.Count > limit)
            {
                Swatches.RemoveAt(Swatches.Count - 1);
            }

            return swatch;
        }
    }
}