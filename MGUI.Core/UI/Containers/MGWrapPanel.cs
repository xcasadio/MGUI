using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using System.Collections.Generic;

namespace MGUI.Core.UI.Containers
{
    public class MGWrapPanel : MGMultiContentHost
    {
        private readonly List<WrapPanelChildMeasurement> _ChildMeasurements = new();
        private readonly List<Rectangle> _ArrangedChildBounds = new();

        #region Border
        public MGComponent<MGBorder> BorderComponent { get; }
        private MGBorder BorderElement { get; }
        public override MGBorder GetBorder() => BorderElement;

        public IBorderBrush BorderBrush
        {
            get => BorderElement.BorderBrush;
            set => BorderElement.BorderBrush = value;
        }

        public Thickness BorderThickness
        {
            get => BorderElement.BorderThickness;
            set => BorderElement.BorderThickness = value;
        }

        public MGCornerRadius CornerRadius
        {
            get => BorderElement.CornerRadius;
            set => BorderElement.CornerRadius = value;
        }
        #endregion Border

        private Orientation _Orientation;
        public Orientation Orientation
        {
            get => _Orientation;
            set
            {
                if (_Orientation != value)
                {
                    _Orientation = value;
                    LayoutChanged(this, true);
                    NPC(nameof(Orientation));
                }
            }
        }

        private int _Spacing;
        public int Spacing
        {
            get => _Spacing;
            set
            {
                if (_Spacing != value)
                {
                    _Spacing = value;
                    LayoutChanged(this, true);
                    NPC(nameof(Spacing));
                }
            }
        }

        /// <summary>The effective spacing used during measure/arrange, after applying <see cref="MGElement.ResponsiveSpacingScaleFactor"/> to <see cref="Spacing"/>.<br/>
        /// Equals <see cref="Spacing"/> when responsive spacing scaling doesn't apply (e.g. outside a responsive subtree, or at scale factor 1.0).</summary>
        internal int ResolvedSpacing => ResolveOwnedSpacing(_Spacing);

        public bool TryAddChild(MGElement item)
        {
            if (!CanChangeContent)
            {
                return false;
            }

            _Children.Add(item);
            return true;
        }

        public bool TryInsertChild(int index, MGElement item)
        {
            if (!CanChangeContent || index < 0 || index > _Children.Count)
            {
                return false;
            }

            if (index == _Children.Count)
            {
                return TryAddChild(item);
            }

            _Children.Insert(index, item);
            return true;
        }

        public bool TryRemoveChild(MGElement item)
        {
            if (!CanChangeContent)
            {
                return false;
            }

            return _Children.Remove(item);
        }

        public bool TryReplaceChild(MGElement oldItem, MGElement newItem)
        {
            if (!CanChangeContent)
            {
                return false;
            }

            int index = _Children.IndexOf(oldItem);
            if (index < 0)
            {
                return false;
            }

            _Children[index] = newItem;
            return true;
        }

        public bool TryRemoveAll()
        {
            if (!CanChangeContent)
            {
                return false;
            }

            _Children.ClearOneByOne();
            return true;
        }

        public MGWrapPanel(MGWindow window, Orientation orientation = Orientation.Horizontal)
            : base(window, MGElementType.WrapPanel)
        {
            using (BeginInitializing())
            {
                BorderElement = new(window, 0, null as IFillBrush);
                BorderComponent = MGComponentBase.Create(BorderElement);
                AddComponent(BorderComponent);
                BorderElement.OnBorderBrushChanged += (_, _) => NPC(nameof(BorderBrush));
                BorderElement.OnBorderThicknessChanged += (_, _) => NPC(nameof(BorderThickness));
                BorderElement.OnCornerRadiusChanged += (_, _) => NPC(nameof(CornerRadius));

                Orientation = orientation;
                Spacing = 0;
            }
        }

        protected override void UpdateContentLayout(Rectangle bounds)
        {
            if (!HasContent)
            {
                return;
            }

            int resolvedSpacing = ResolvedSpacing;
            List<WrapPanelChildMeasurement> childMeasurements = MeasureChildren(bounds.Size);
            MGWrapPanelLayoutEngine.ArrangeInto(childMeasurements, bounds, Orientation, resolvedSpacing, _ArrangedChildBounds);
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].UpdateLayout(_ArrangedChildBounds[i]);
            }
        }

        protected override Thickness UpdateContentMeasurement(Size availableSize)
        {
            if (!HasContent)
            {
                return UpdateContentMeasurementBaseImplementation(availableSize);
            }

            int resolvedSpacing = ResolvedSpacing;
            List<WrapPanelChildMeasurement> childMeasurements = MeasureChildren(availableSize);
            Size desiredSize = MGWrapPanelLayoutEngine.Measure(childMeasurements, availableSize, Orientation, resolvedSpacing);
            return new Thickness(desiredSize.Width, desiredSize.Height, 0, 0);
        }

        private List<WrapPanelChildMeasurement> MeasureChildren(Size availableSize)
        {
            _ChildMeasurements.Clear();
            foreach (MGElement child in Children)
            {
                child.UpdateMeasurement(availableSize, out _, out Thickness fullSize, out _, out _);
                _ChildMeasurements.Add(new WrapPanelChildMeasurement(fullSize.Width, fullSize.Height, child.IsVisibilityCollapsed));
            }

            return _ChildMeasurements;
        }
    }
}