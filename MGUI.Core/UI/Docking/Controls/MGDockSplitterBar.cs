using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Visual splitter bar that can be dragged to resize the split container.
/// Used internally by MGDockSplitContainer.
/// </summary>
public class MGDockSplitterBar : MGElement, IActiveMouseDragCapture
{
    public const string SurfacePartName = "PART_Surface";
    public const string AccentPartName = "PART_Accent";
    public const string GripPartName = "PART_Grip";

    private MGBorder SurfaceElement { get; set; }
    private MGComponent<MGBorder> SurfaceComponent { get; set; }
    private MGBorder AccentElement { get; set; }
    private MGComponent<MGBorder> AccentComponent { get; set; }
    private MGGripDotsIcon GripElement { get; set; }
    private MGComponent<MGGripDotsIcon> GripComponent { get; set; }

    private bool _isDragging;
    /// <summary>
    /// True if the splitter is currently being dragged.
    /// </summary>
    public bool IsDragging
    {
        get => _isDragging;
        private set
        {
            if (_isDragging != value)
            {
                _isDragging = value;
                SyncVisualParts();
                NPC(nameof(IsDragging));
            }
        }
    }

    bool IActiveMouseDragCapture.IsActiveMouseDragCapture => IsDragging;

    private IFillBrush _normalBrush;
    /// <summary>
    /// Brush used when the splitter is in normal state.
    /// </summary>
    public IFillBrush NormalBrush
    {
        get => _normalBrush;
        set
        {
            if (_normalBrush != value)
            {
                _normalBrush = value;
                SyncVisualParts();
                NPC(nameof(NormalBrush));
            }
        }
    }

    private IFillBrush _hoverBrush;
    /// <summary>
    /// Brush used when the splitter is hovered.
    /// </summary>
    public IFillBrush HoverBrush
    {
        get => _hoverBrush;
        set
        {
            if (_hoverBrush != value)
            {
                _hoverBrush = value;
                SyncVisualParts();
                NPC(nameof(HoverBrush));
            }
        }
    }

    private IFillBrush _pressedBrush;
    /// <summary>
    /// Brush used when the splitter is being pressed/dragged.
    /// </summary>
    public IFillBrush PressedBrush
    {
        get => _pressedBrush;
        set
        {
            if (_pressedBrush != value)
            {
                _pressedBrush = value;
                SyncVisualParts();
                NPC(nameof(PressedBrush));
            }
        }
    }

    public Color HoverOverlayColor { get; set; } = new Color(150, 200, 255, 120);
    public Color PressedOverlayColor { get; set; } = new Color(100, 180, 255, 180);

    /// <summary>
    /// Gets the parent MGDockSplitContainer, if any.
    /// </summary>
    public MGDockSplitContainer ParentSplitContainer => Parent as MGDockSplitContainer;

    /// <summary>
    /// The initial split ratio when drag started.
    /// </summary>
    private float _dragStartRatio;

    /// <summary>
    /// The initial mouse position when drag started (in screen space).
    /// </summary>
    private Point _dragStartMousePosition;

    /// <summary>
    /// Event raised when the user drags the splitter.
    /// Provides the delta in split ratio.
    /// </summary>
    public event EventHandler<float> SplitRatioDragged;

    /// <summary>
    /// Creates a new MGDockSplitterBar.
    /// </summary>
    /// <param name="window">The parent window.</param>
    public MGDockSplitterBar(MGWindow window) : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            // Set default brushes with better visual feedback
            NormalBrush = new MGSolidFillBrush(new Color(64, 64, 64));        // Dark gray
            HoverBrush = new MGSolidFillBrush(new Color(100, 150, 200));      // Blue highlight
            PressedBrush = new MGSolidFillBrush(new Color(70, 130, 180));     // Darker blue when dragging
            // The surface, accent and grip parts come from the control template, see AttachControlTemplateStructure.
            DefaultControlTemplateName = MGControlTemplateCatalog.DockSplitterTemplateName;
            SyncVisualParts();

            // Subscribe to mouse press event to start dragging
            MouseHandler.LMBPressedInside += OnLMBPressed;
        }
    }

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(SurfacePartName, typeof(MGBorder));
        yield return new(AccentPartName, typeof(MGBorder));
        yield return new(GripPartName, typeof(MGGripDotsIcon));
    }

    /// <summary>Binds the parts created by the control template (<c>Dock.Splitter.Default</c>) as components laid out on this bar. The brushes and
    /// overlay colors stay this bar's own properties and are pushed to the parts by <see cref="SyncVisualParts"/>; a structure replaced by another
    /// template releases the components of its parts.</summary>
    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
    {
        SurfaceElement = (MGBorder)Structure.Parts[SurfacePartName];
        AccentElement = (MGBorder)Structure.Parts[AccentPartName];
        GripElement = (MGGripDotsIcon)Structure.Parts[GripPartName];

        SurfaceElement.ManagedParent = this;
        SurfaceElement.IsHitTestVisible = false;
        AccentElement.ManagedParent = this;
        AccentElement.IsHitTestVisible = false;
        GripElement.ManagedParent = this;

        EnsureComponentBinding(() => SurfaceComponent, value => SurfaceComponent = value, SurfaceElement,
            element => new(element, false, false, false, false, false, false, false, (availableBounds, componentSize) => LayoutBounds));
        EnsureComponentBinding(() => AccentComponent, value => AccentComponent = value, AccentElement,
            element => new(element, false, false, false, false, false, false, false, (availableBounds, componentSize) => GetAccentBounds()));
        EnsureComponentBinding(() => GripComponent, value => GripComponent = value, GripElement,
            element => new(element, false, false, false, false, false, false, false, (availableBounds, componentSize) => LayoutBounds));

        SyncVisualParts();
    }

    private Rectangle GetAccentBounds()
    {
        Rectangle accentBounds = LayoutBounds;
        if (ParentSplitContainer?.Orientation == Orientation.Horizontal)
        {
            accentBounds.X -= 1;
            accentBounds.Width += 2;
        }
        else
        {
            accentBounds.Y -= 1;
            accentBounds.Height += 2;
        }

        return accentBounds;
    }

    private void SyncVisualParts()
    {
        if (SurfaceElement == null || AccentElement == null || GripElement == null)
        {
            return;
        }

        SurfaceElement.SetBackgroundSlot(UIValueSlot.Normal, IsDragging
            ? PressedBrush
            : IsHovered
                ? HoverBrush
                : NormalBrush, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        bool showAccent = IsHovered || IsDragging;
        AccentElement.Visibility = showAccent ? Visibility.Visible : Visibility.Collapsed;
        AccentElement.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(IsDragging ? PressedOverlayColor : HoverOverlayColor), UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        GripElement.IsVertical = ParentSplitContainer?.Orientation == Orientation.Horizontal;
        GripElement.DotColor = (IsHovered || IsDragging)
            ? Color.White * 0.8f
            : Color.White * 0.5f;
    }

    private void OnLMBPressed(object sender, BaseMousePressedEventArgs e)
    {
        if (ParentSplitContainer == null)
        {
            return;
        }

        IsDragging = true;
        _dragStartRatio = ParentSplitContainer.SplitRatio;
        _dragStartMousePosition = e.Position;
            
        e.SetHandledBy(this, false);
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);
        SyncVisualParts();

        if (IsDragging)
        {
            // Check if left mouse button is still pressed
            bool isStillPressed = ParentWindow.Desktop.InputTracker.Mouse.CurrentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
                
            if (!isStillPressed)
            {
                // Mouse button released, end drag
                IsDragging = false;

                // Commit the final ratio to the model.
                // We must use CommitRatioToModel() instead of SetSplitRatio() because
                // SetSplitRatioWithoutSync() already wrote the current value into _splitRatio
                // during the drag, so SetSplitRatio()'s "!= check" would silently no-op
                // and the model (DockSplitNode.SplitRatio) would remain stale — causing a
                // full RebuildVisualTree() triggered by any subsequent property change (e.g.
                // a tab click) to reset the split to its pre-drag ratio.
                ParentSplitContainer?.CommitRatioToModel();
                return;
            }

            if (ParentSplitContainer == null)
            {
                return;
            }

            // Get current mouse position
            Point currentMousePosition = ParentWindow.Desktop.InputTracker.Mouse.CurrentPosition;

            // Calculate delta in pixels
            Point delta = new Point(
                currentMousePosition.X - _dragStartMousePosition.X,
                currentMousePosition.Y - _dragStartMousePosition.Y
            );

            // Get container bounds
            Rectangle containerBounds = ParentSplitContainer.LayoutBounds;
                
            // Calculate new ratio based on orientation
            float newRatio = _dragStartRatio;
                
            if (ParentSplitContainer.Orientation == Orientation.Horizontal)
            {
                // Horizontal split: drag left/right
                int availableWidth = containerBounds.Width - ParentSplitContainer.SplitterThickness;
                if (availableWidth > 0)
                {
                    float deltaRatio = (float)delta.X / availableWidth;
                    newRatio = _dragStartRatio + deltaRatio;
                }
            }
            else
            {
                // Vertical split: drag up/down
                int availableHeight = containerBounds.Height - ParentSplitContainer.SplitterThickness;
                if (availableHeight > 0)
                {
                    float deltaRatio = (float)delta.Y / availableHeight;
                    newRatio = _dragStartRatio + deltaRatio;
                }
            }

            // Clamp to ensure min sizes are respected
            newRatio = ClampRatioToMinSizes(newRatio);

            // DURING DRAG: Update view only, no model sync
            SplitRatioDragged?.Invoke(this, newRatio);
            ParentSplitContainer?.SetSplitRatioWithoutSync(newRatio, clamp: false);
        }
    }

    /// <summary>
    /// Clamps the ratio to ensure min sizes are respected.
    /// </summary>
    private float ClampRatioToMinSizes(float ratio)
    {
        if (ParentSplitContainer == null)
        {
            return ratio;
        }

        Rectangle bounds = ParentSplitContainer.LayoutBounds;
        int splitterThickness = ParentSplitContainer.SplitterThickness;
        int minFirstSize = ParentSplitContainer.MinFirstSize;
        int minSecondSize = ParentSplitContainer.MinSecondSize;

        int availableSize = (ParentSplitContainer.Orientation == Orientation.Horizontal)
            ? bounds.Width - splitterThickness
            : bounds.Height - splitterThickness;

        if (availableSize <= 0)
        {
            return ratio;
        }

        return DockSplitSizing.ClampRatioToMinSizes(ratio, availableSize, minFirstSize, minSecondSize);
    }

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
    {
        SharedSize = new Thickness(0);
            
        // Splitter bar size depends on parent container orientation
        if (ParentSplitContainer != null)
        {
            int thickness = ParentSplitContainer.SplitterThickness;
            if (ParentSplitContainer.Orientation == Orientation.Horizontal)
            {
                // Vertical bar for horizontal split
                return new Thickness(thickness, AvailableSize.Height, 0, 0);
            }

            // Horizontal bar for vertical split
            return new Thickness(AvailableSize.Width, thickness, 0, 0);
        }
            
        // Default size if no parent
        return new Thickness(4, 4, 0, 0);
    }
        
}