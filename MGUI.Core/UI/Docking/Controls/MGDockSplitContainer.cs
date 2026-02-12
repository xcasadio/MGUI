using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Container that displays two child elements separated by a draggable splitter bar.
/// Supports horizontal (left/right) and vertical (top/bottom) orientations.
/// </summary>
public class MGDockSplitContainer : MGElement
{
    private Orientation _orientation;
    /// <summary>
    /// The orientation of the split. Horizontal = left/right, Vertical = top/bottom.
    /// </summary>
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation != value)
            {
                _orientation = value;
                LayoutChanged(this, true);
                NPC(nameof(Orientation));
            }
        }
    }

    private MGElement _firstChild;
    /// <summary>
    /// The first child element (left for horizontal, top for vertical).
    /// </summary>
    public MGElement FirstChild
    {
        get => _firstChild;
        set
        {
            if (_firstChild != value)
            {
                if (_firstChild != null)
                {
                    _firstChild.SetParent(null);
                }

                _firstChild = value;

                if (_firstChild != null)
                {
                    _firstChild.SetParent(this);
                }

                LayoutChanged(this, true);
                NPC(nameof(FirstChild));
            }
        }
    }

    private MGElement _secondChild;
    /// <summary>
    /// The second child element (right for horizontal, bottom for vertical).
    /// </summary>
    public MGElement SecondChild
    {
        get => _secondChild;
        set
        {
            if (_secondChild != value)
            {
                if (_secondChild != null)
                {
                    _secondChild.SetParent(null);
                }

                _secondChild = value;

                if (_secondChild != null)
                {
                    _secondChild.SetParent(this);
                }

                LayoutChanged(this, true);
                NPC(nameof(SecondChild));
            }
        }
    }

    private float _splitRatio = 0.5f;
    /// <summary>
    /// The split ratio (0.0 to 1.0) representing the proportion allocated to the first child.
    /// Default is 0.5 (50/50 split).
    /// </summary>
    public float SplitRatio
    {
        get => _splitRatio;
        set
        {
            float clamped = Math.Clamp(value, 0.0f, 1.0f);
            if (_splitRatio != clamped)
            {
                _splitRatio = clamped;
                LayoutChanged(this, true);
                NPC(nameof(SplitRatio));
            }
        }
    }

    private int _minFirstSize = 100;
    /// <summary>
    /// Minimum size (width for horizontal, height for vertical) for the first child.
    /// Default is 100 pixels.
    /// </summary>
    public int MinFirstSize
    {
        get => _minFirstSize;
        set
        {
            if (_minFirstSize != value)
            {
                _minFirstSize = value;
                LayoutChanged(this, true);
                NPC(nameof(MinFirstSize));
            }
        }
    }

    private int _minSecondSize = 100;
    /// <summary>
    /// Minimum size (width for horizontal, height for vertical) for the second child.
    /// Default is 100 pixels.
    /// </summary>
    public int MinSecondSize
    {
        get => _minSecondSize;
        set
        {
            if (_minSecondSize != value)
            {
                _minSecondSize = value;
                LayoutChanged(this, true);
                NPC(nameof(MinSecondSize));
            }
        }
    }

    private int _splitterThickness = 4;
    /// <summary>
    /// The thickness of the splitter bar in pixels.
    /// Default is 4 pixels.
    /// </summary>
    public int SplitterThickness
    {
        get => _splitterThickness;
        set
        {
            if (_splitterThickness != value)
            {
                _splitterThickness = value;
                LayoutChanged(this, true);
                NPC(nameof(SplitterThickness));
            }
        }
    }

    /// <summary>
    /// The splitter bar visual element.
    /// </summary>
    private readonly MGDockSplitterBar _splitterBar;

    /// <summary>
    /// Event raised when the split ratio changes.
    /// </summary>
    public event EventHandler<float> SplitRatioChanged;

    /// <summary>
    /// Creates a new MGDockSplitContainer.
    /// </summary>
    /// <param name="window">The parent window.</param>
    /// <param name="orientation">The split orientation.</param>
    public MGDockSplitContainer(MGWindow window, Orientation orientation = Orientation.Horizontal) 
        : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            _orientation = orientation;

            // Create splitter bar
            _splitterBar = new MGDockSplitterBar(window);
            _splitterBar.SetParent(this);

            // Default alignment
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    /// <summary>
    /// Sets the split ratio, optionally clamping it.
    /// </summary>
    /// <param name="newRatio">The new split ratio.</param>
    /// <param name="clamp">Whether to clamp the ratio to ensure min sizes are respected.</param>
    public void SetSplitRatio(float newRatio, bool clamp = true)
    {
        if (clamp)
        {
            newRatio = ClampRatioToMinSizes(newRatio);
        }

        if (_splitRatio != newRatio)
        {
            _splitRatio = newRatio;
            LayoutChanged(this, true);
            NPC(nameof(SplitRatio));
            SplitRatioChanged?.Invoke(this, newRatio);
        }
    }

    /// <summary>
    /// Sets the split ratio without triggering model synchronization.
    /// Used during drag operations to avoid rebuilding the visual tree.
    /// Call <see cref="SetSplitRatio"/> after drag ends to sync to model.
    /// </summary>
    public void SetSplitRatioWithoutSync(float newRatio, bool clamp = true)
    {
        if (clamp)
        {
            newRatio = ClampRatioToMinSizes(newRatio);
        }

        if (_splitRatio != newRatio)
        {
            _splitRatio = newRatio;
            LayoutChanged(this, true);
            // DO NOT trigger NPC or SplitRatioChanged - no model sync during drag
        }
    }

    /// <summary>
    /// Clamps the ratio to ensure minimum sizes are respected.
    /// </summary>
    private float ClampRatioToMinSizes(float ratio)
    {
        int availableSize = (Orientation == Orientation.Horizontal)
            ? LayoutBounds.Width - SplitterThickness
            : LayoutBounds.Height - SplitterThickness;

        if (availableSize <= 0)
        {
            return ratio;
        }

        float minRatio = (float)MinFirstSize / availableSize;
        float maxRatio = (float)(availableSize - MinSecondSize) / availableSize;

        return Math.Clamp(ratio, minRatio, maxRatio);
    }

    public override IEnumerable<MGElement> GetChildren()
    {
        if (FirstChild != null)
        {
            yield return FirstChild;
        }

        if (_splitterBar != null)
        {
            yield return _splitterBar;
        }

        if (SecondChild != null)
        {
            yield return SecondChild;
        }
    }

    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        System.Diagnostics.Debug.WriteLine($"[MGDockSplitContainer] UpdateContentMeasurement: AvailableSize={AvailableSize}, Orientation={Orientation}");
            
        if (FirstChild == null && SecondChild == null)
        {
            return new Thickness(0);
        }

        Thickness firstSize = new Thickness(0);
        Thickness secondSize = new Thickness(0);
        Thickness splitterSize = new Thickness(0);

        // Measure children
        if (FirstChild != null)
        {
            FirstChild.UpdateMeasurement(AvailableSize, out _, out firstSize, out _, out _);
        }

        if (_splitterBar != null)
        {
            _splitterBar.UpdateMeasurement(AvailableSize, out _, out splitterSize, out _, out _);
        }

        if (SecondChild != null)
        {
            SecondChild.UpdateMeasurement(AvailableSize, out _, out secondSize, out _, out _);
        }

        // Calculate total size based on orientation
        if (Orientation == Orientation.Horizontal)
        {
            // Horizontal: widths add up, height is max
            int totalWidth = firstSize.Width + splitterSize.Width + secondSize.Width;
            int maxHeight = Math.Max(firstSize.Height, Math.Max(splitterSize.Height, secondSize.Height));
            return new Thickness(totalWidth, maxHeight, 0, 0);
        }

        // Vertical: heights add up, width is max
        int maxWidth = Math.Max(firstSize.Width, Math.Max(splitterSize.Width, secondSize.Width));
        int totalHeight = firstSize.Height + splitterSize.Height + secondSize.Height;
        return new Thickness(maxWidth, totalHeight, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        if (FirstChild == null && SecondChild == null)
        {
            return;
        }

        Rectangle firstBounds, splitterBounds, secondBounds;

        if (Orientation == Orientation.Horizontal)
        {
            // Horizontal split: side by side
            int availableWidth = Bounds.Width - SplitterThickness;
                
            // Calculate sizes based on ratio
            int firstWidth = (int)(availableWidth * SplitRatio);
            int secondWidth = availableWidth - firstWidth;

            // Apply min size constraints
            if (firstWidth < MinFirstSize)
            {
                firstWidth = MinFirstSize;
                secondWidth = availableWidth - firstWidth;
            }
            if (secondWidth < MinSecondSize)
            {
                secondWidth = MinSecondSize;
                firstWidth = availableWidth - secondWidth;
            }

            // Ensure we don't go negative
            firstWidth = Math.Max(0, firstWidth);
            secondWidth = Math.Max(0, secondWidth);

            // Calculate bounds
            firstBounds = new Rectangle(Bounds.X, Bounds.Y, firstWidth, Bounds.Height);
            splitterBounds = new Rectangle(Bounds.X + firstWidth, Bounds.Y, SplitterThickness, Bounds.Height);
            secondBounds = new Rectangle(Bounds.X + firstWidth + SplitterThickness, Bounds.Y, secondWidth, Bounds.Height);
        }
        else
        {
            // Vertical split: top and bottom
            int availableHeight = Bounds.Height - SplitterThickness;
                
            // Calculate sizes based on ratio
            int firstHeight = (int)(availableHeight * SplitRatio);
            int secondHeight = availableHeight - firstHeight;

            // Apply min size constraints
            if (firstHeight < MinFirstSize)
            {
                firstHeight = MinFirstSize;
                secondHeight = availableHeight - firstHeight;
            }
            if (secondHeight < MinSecondSize)
            {
                secondHeight = MinSecondSize;
                firstHeight = availableHeight - secondHeight;
            }

            // Ensure we don't go negative
            firstHeight = Math.Max(0, firstHeight);
            secondHeight = Math.Max(0, secondHeight);

            // Calculate bounds
            firstBounds = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, firstHeight);
            splitterBounds = new Rectangle(Bounds.X, Bounds.Y + firstHeight, Bounds.Width, SplitterThickness);
            secondBounds = new Rectangle(Bounds.X, Bounds.Y + firstHeight + SplitterThickness, Bounds.Width, secondHeight);
        }

        // Update child layouts
        FirstChild?.UpdateLayout(firstBounds);
        _splitterBar?.UpdateLayout(splitterBounds);
        SecondChild?.UpdateLayout(secondBounds);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        // No self rendering needed - children will draw themselves
        DrawSelfBaseImplementation(DA, LayoutBounds);
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        // Draw all children
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }
    }
}