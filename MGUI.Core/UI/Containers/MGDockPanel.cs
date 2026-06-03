using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Core.UI.Containers
{
    public class MGDockPanel : MGMultiContentHost
    {
        private readonly record struct DockedChild(MGElement Item, Dock Position)
        {
            public bool IsLeftOrRight => Position == Dock.Left || Position == Dock.Right;
            public bool IsTopOrBottom => Position == Dock.Top || Position == Dock.Bottom;
        }

        private bool _LastChildFill;
        /// <summary>If true, the last child of this <see cref="MGDockPanel"/> will consume all remaining available space, regardless of it's <see cref="Dock"/> position.<para/>
        /// Default value: true</summary>
        public bool LastChildFill
        {
            get => _LastChildFill;
            set
            {
                if (_LastChildFill != value)
                {
                    _LastChildFill = value;
                    LayoutChanged(this, true);
                    NPC(nameof(LastChildFill));
                }
            }
        }

        private readonly List<DockedChild> DockedChildren = new();

        private readonly List<Size> CachedChildMeasurementAvailableSizes = new();
        private readonly List<Thickness> CachedChildMeasurementFullSizes = new();

        /// <summary>The last child will consume all remaining available space if <see cref="LastChildFill"/> is true, thus ignoring its <see cref="DockedChild.Position"/> value.</summary>
        private Dock? GetActualDockPosition(int index)
        {
            if (index < 0 || index >= DockedChildren.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return LastChildFill && index == DockedChildren.Count - 1
                ? null
                : DockedChildren[index].Position;
        }

        private void InvalidateChildMeasurementCache()
        {
            CachedChildMeasurementAvailableSizes.Clear();
            CachedChildMeasurementFullSizes.Clear();
        }

        private void EnsureChildMeasurementCacheCapacity()
        {
            while (CachedChildMeasurementAvailableSizes.Count < DockedChildren.Count)
            {
                CachedChildMeasurementAvailableSizes.Add(new Size(-1, -1));
                CachedChildMeasurementFullSizes.Add(default);
            }

            while (CachedChildMeasurementAvailableSizes.Count > DockedChildren.Count)
            {
                CachedChildMeasurementAvailableSizes.RemoveAt(CachedChildMeasurementAvailableSizes.Count - 1);
                CachedChildMeasurementFullSizes.RemoveAt(CachedChildMeasurementFullSizes.Count - 1);
            }
        }

        private static bool CanReuseCachedMeasurement(MGElement child, Size cachedAvailableSize, Thickness cachedFullSize, Size requestedAvailableSize)
        {
            if (!child.IsLayoutValid)
            {
                return false;
            }

            if (cachedAvailableSize == requestedAvailableSize)
            {
                return true;
            }

            return cachedAvailableSize.Width >= requestedAvailableSize.Width
                && cachedAvailableSize.Height >= requestedAvailableSize.Height
                && cachedFullSize.Width <= requestedAvailableSize.Width
                && cachedFullSize.Height <= requestedAvailableSize.Height;
        }

        private bool TryGetCachedChildMeasurement(int childIndex, MGElement child, Size availableSize, out Thickness fullSize)
        {
            if (childIndex >= 0
                && childIndex < CachedChildMeasurementAvailableSizes.Count
                && CanReuseCachedMeasurement(child, CachedChildMeasurementAvailableSizes[childIndex], CachedChildMeasurementFullSizes[childIndex], availableSize))
            {
                fullSize = CachedChildMeasurementFullSizes[childIndex];
                return true;
            }

            fullSize = default;
            return false;
        }

        private void CacheChildMeasurement(int childIndex, Size availableSize, Thickness fullSize)
        {
            CachedChildMeasurementAvailableSizes[childIndex] = availableSize;
            CachedChildMeasurementFullSizes[childIndex] = fullSize;
        }

        private static void UpdateChildLayoutIfNeeded(MGElement child, Rectangle childBounds)
        {
            if (!child.IsLayoutValid || child.AllocatedBounds != childBounds)
            {
                child.UpdateLayout(childBounds);
            }
        }

        public bool TryAddChild(MGElement Item, Dock Dock)
        {
            if (!CanChangeContent)
            {
                return false;
            }

            DockedChildren.Add(new(Item, Dock));
            _Children.Add(Item);
            InvalidateChildMeasurementCache();
            return true;
        }

        public bool TryRemoveChild(MGElement Item)
        {
            if (!CanChangeContent)
            {
                return false;
            }

            for (int i = 0; i < DockedChildren.Count; i++)
            {
                DockedChild ActualItem = DockedChildren[i];
                if (ActualItem.Item == Item)
                {
                    DockedChildren.RemoveAt(i);
                    _Children.Remove(Item);
                    InvalidateChildMeasurementCache();
                    return true;
                }
            }

            return false;
        }

        public MGDockPanel(MGWindow Window, bool LastChildFill = true)
            : base(Window, MGElementType.DockPanel)
        {
            using (BeginInitializing())
            {
                this.LastChildFill = LastChildFill;
            }
        }

        protected override void UpdateContentLayout(Rectangle Bounds)
        {
            if (!HasContent)
            {
                return;
            }

            EnsureChildMeasurementCacheCapacity();
            Size AvailableSize = new(Bounds.Width, Bounds.Height);

            //Referenced:
            //https://referencesource.microsoft.com/#PresentationFramework/src/Framework/System/windows/Controls/DockPanel.cs

            int AccumulatedLeft = 0;
            int AccumulatedTop = 0;
            int AccumulatedRight = 0;
            int AccumulatedBottom = 0;

            for (int childIndex = 0; childIndex < DockedChildren.Count; childIndex++)
            {
                DockedChild Child = DockedChildren[childIndex];
                Dock? actualDock = GetActualDockPosition(childIndex);
                int AccumulatedWidth = AccumulatedLeft + AccumulatedRight;
                int AccumulatedHeight = AccumulatedTop + AccumulatedBottom;

                Size RemainingSize = AvailableSize.Subtract(new Size(AccumulatedWidth, AccumulatedHeight), 0, 0);
                if (!TryGetCachedChildMeasurement(childIndex, Child.Item, RemainingSize, out Thickness FullSize))
                {
                    Child.Item.UpdateMeasurement(RemainingSize, out _, out FullSize, out _, out _);
                    CacheChildMeasurement(childIndex, RemainingSize, FullSize);
                }

                Rectangle ChildBounds;
                if (!actualDock.HasValue)
                {
                    ChildBounds = new(Bounds.Left + AccumulatedLeft, Bounds.Top + AccumulatedTop, Bounds.Width - AccumulatedWidth, Bounds.Height - AccumulatedHeight);
                }
                else
                {
                    switch (actualDock.Value)
                    {
                        case Dock.Left:
                            ChildBounds = new(Bounds.Left + AccumulatedLeft, Bounds.Top + AccumulatedTop, FullSize.Width, RemainingSize.Height);
                            AccumulatedLeft += FullSize.Width;
                            break;

                        case Dock.Right:
                            AccumulatedRight += FullSize.Width;
                            ChildBounds = new(Bounds.Right - AccumulatedRight, Bounds.Top + AccumulatedTop, FullSize.Width, RemainingSize.Height);
                            break;

                        case Dock.Top:
                            ChildBounds = new(Bounds.Left + AccumulatedLeft, Bounds.Top + AccumulatedTop, RemainingSize.Width, FullSize.Height);
                            AccumulatedTop += FullSize.Height;
                            break;

                        case Dock.Bottom:
                            AccumulatedBottom += FullSize.Height;
                            ChildBounds = new(Bounds.Left + AccumulatedLeft, Bounds.Bottom - AccumulatedBottom, RemainingSize.Width, FullSize.Height);
                            break;

                        default: throw new NotImplementedException($"Unrecognized {nameof(Dock)}: {actualDock.Value}");
                    }
                }

                UpdateChildLayoutIfNeeded(Child.Item, ChildBounds);
            }
        }

        protected override Thickness UpdateContentMeasurement(Size AvailableSize)
        {
            if (HasContent)
            {
                EnsureChildMeasurementCacheCapacity();
                //Referenced:
                //https://referencesource.microsoft.com/#PresentationFramework/src/Framework/System/windows/Controls/DockPanel.cs

                int TotalContentWidth = 0;
                int TotalContentHeight = 0;
                int AccumulatedWidth = 0;
                int AccumulatedHeight = 0;

                for (int childIndex = 0; childIndex < DockedChildren.Count; childIndex++)
                {
                    DockedChild Child = DockedChildren[childIndex];
                    Size RemainingSize = AvailableSize.Subtract(new Size(AccumulatedWidth, AccumulatedHeight), 0, 0);
                    if (!TryGetCachedChildMeasurement(childIndex, Child.Item, RemainingSize, out Thickness FullSize))
                    {
                        Child.Item.UpdateMeasurement(RemainingSize, out _, out FullSize, out _, out _);
                        CacheChildMeasurement(childIndex, RemainingSize, FullSize);
                    }

                    if (Child.IsLeftOrRight)
                    {
                        TotalContentHeight = Math.Max(TotalContentHeight, AccumulatedHeight + FullSize.Height);
                        AccumulatedWidth += FullSize.Width;
                    }
                    else if (Child.IsTopOrBottom)
                    {
                        TotalContentWidth = Math.Max(TotalContentWidth, AccumulatedWidth + FullSize.Width);
                        AccumulatedHeight += FullSize.Height;
                    }
                }

                TotalContentWidth = Math.Max(TotalContentWidth, AccumulatedWidth);
                TotalContentHeight = Math.Max(TotalContentHeight, AccumulatedHeight);

                return new Thickness(TotalContentWidth, TotalContentHeight, 0, 0);
            }
            else
            {
                return UpdateContentMeasurementBaseImplementation(AvailableSize);
            }
        }
    }
}
