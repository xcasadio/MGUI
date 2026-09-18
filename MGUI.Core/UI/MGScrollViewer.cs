using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Mouse;
using System.Diagnostics;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;

namespace MGUI.Core.UI;

public class MGScrollViewer : MGSingleContentHost
{
    public const int VSBWidth = 16;
    public const int HSBHeight = 16;

    private const int ScrollBarPadding = 2;

    internal static float GetVisibleOffset(float currentOffset, float viewportSize, float maxOffset, float elementStart, float elementEnd)
    {
        if (viewportSize <= 0)
        {
            return Math.Clamp(currentOffset, 0, maxOffset);
        }

        var newOffset = currentOffset;
        if (elementStart < currentOffset)
        {
            newOffset = elementStart;
        }
        else if (elementEnd > currentOffset + viewportSize)
        {
            newOffset = elementEnd - viewportSize;
        }

        return Math.Clamp(newOffset, 0, maxOffset);
    }

    internal static float GetVisibleOffset(float currentOffset, float viewportStart, float viewportSize, float maxOffset, float elementStart, float elementEnd)
    {
        if (viewportSize <= 0)
        {
            return Math.Clamp(currentOffset, 0, maxOffset);
        }

        var newOffset = currentOffset;
        var viewportEnd = viewportStart + viewportSize;
        if (elementStart < viewportStart)
        {
            newOffset -= viewportStart - elementStart;
        }
        else if (elementEnd > viewportEnd)
        {
            newOffset += elementEnd - viewportEnd;
        }

        return Math.Clamp(newOffset, 0, maxOffset);
    }

    private bool TryGetDescendantBoundsInContentSpace(MGElement target, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;

        if (target == null || !HasContent)
        {
            return false;
        }

        if (Content == target)
        {
            bounds = target.LayoutBounds;
            return true;
        }

        if (!Content.IsSelfOrAncestorOf(target))
        {
            return false;
        }

        var currentBounds = target.LayoutBounds;
        for (var current = target.Parent; current != null; current = current.Parent)
        {
            if (current == Content)
            {
                bounds = currentBounds;
                return true;
            }

            currentBounds = currentBounds.GetTranslated(current.LayoutBounds.Location);
        }

        return false;
    }

    public void EnsureElementVisible(MGElement target)
    {
        if (target == null || !HasContent || ContentViewport.Width <= 0 || ContentViewport.Height <= 0)
        {
            return;
        }

        if (!TryGetDescendantBoundsInContentSpace(target, out var bounds))
        {
            return;
        }

        var pendingVerticalOffset = PendingVerticalOffset;
        var pendingHorizontalOffset = PendingHorizontalOffset;
        var verticalViewportStart = Content.LayoutBounds.Top + pendingVerticalOffset;
        var horizontalViewportStart = Content.LayoutBounds.Left + pendingHorizontalOffset;
        var newVerticalOffset = GetVisibleOffset(pendingVerticalOffset, verticalViewportStart, ContentViewport.Height, MaxVerticalOffset, bounds.Top, bounds.Bottom);
        var newHorizontalOffset = GetVisibleOffset(pendingHorizontalOffset, horizontalViewportStart, ContentViewport.Width, MaxHorizontalOffset, bounds.Left, bounds.Right);

        if (Math.Abs(newVerticalOffset - pendingVerticalOffset) > 0.5f)
        {
            ScrollTo(null, newVerticalOffset, ScrollAnimationDuration, ScrollAnimationEasing);
        }

        if (Math.Abs(newHorizontalOffset - pendingHorizontalOffset) > 0.5f)
        {
            ScrollTo(newHorizontalOffset, null, ScrollAnimationDuration, ScrollAnimationEasing);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _AllowClickDragScrolling;
    /// <summary>If true, clicking and dragging anywhere within this <see cref="MGScrollViewer"/>'s viewport will scroll the content,
    /// as long as the drag did not start over the scrollbars.<para/>
    /// Default value: false</summary>
    public bool AllowClickDragScrolling
    {
        get => _AllowClickDragScrolling;
        set
        {
            if (_AllowClickDragScrolling != value)
            {
                _AllowClickDragScrolling = value;
                NotifyPropertyChanged(nameof(AllowClickDragScrolling));
            }
        }
    }

    private bool IsDraggingContent { get; set; }
    private float _ContentDragStartVerticalOffset;
    private float _ContentDragStartHorizontalOffset;

    /// <summary>Represents how much <see cref="VerticalOffset"/> will be changed when using the mouse scroll wheel.<para/>
    /// Recommended value: Anywhere from 20 to 80 (Scrolling on reddit.com seems to scroll by about 84px? Might be percentage-based)</summary>
    public const int VerticalScrollInterval = 40;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ScrollBarVisibility _VSBVisibility;
    public ScrollBarVisibility VSBVisibility
    {
        get => _VSBVisibility;
        set
        {
            if (_VSBVisibility != value)
            {
                _VSBVisibility = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(VSBVisibility));
                NotifyPropertyChanged(nameof(VerticalScrollBarVisibility));
            }
        }
    }

    /// <summary>This property is the same as <see cref="VSBVisibility"/></summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => VSBVisibility;
        set => VSBVisibility = value;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ScrollBarVisibility _HSBVisibility;
    public ScrollBarVisibility HSBVisibility
    {
        get => _HSBVisibility;
        set
        {
            if (_HSBVisibility != value)
            {
                _HSBVisibility = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(HSBVisibility));
                NotifyPropertyChanged(nameof(HorizontalScrollBarVisibility));
            }
        }
    }

    /// <summary>This property is the same as <see cref="HSBVisibility"/></summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => HSBVisibility;
        set => HSBVisibility = value;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Rectangle _ContentViewport;
    /// <summary>The bounds that this <see cref="MGScrollViewer"/>'s Content can draw itself to, 
    /// after accounting for <see cref="MGElement.Padding"/> and the width/height that the scrollbars reserved, if any.</summary>
    public Rectangle ContentViewport
    {
        get => _ContentViewport;
        private set
        {
            if (_ContentViewport != value)
            {
                _ContentViewport = value;
                NotifyPropertyChanged(nameof(ContentViewport));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Rectangle? _VSBBounds;
    public Rectangle? VSBBounds
    {
        get => _VSBBounds;
        private set
        {
            if (_VSBBounds != value)
            {
                var Previous = VSBBounds;
                _VSBBounds = value;
                NotifyPropertyChanged(nameof(VSBBounds));
                NotifyPropertyChanged(nameof(PaddedVSBBounds));
                VerticalScrollBarBoundsChanged?.Invoke(Previous, VSBBounds);
            }
        }
    }
    public event EventHandler<Rectangle?> VerticalScrollBarBoundsChanged;

    /// <summary>The screen bounds that the vertical scrollbar will be rendered to, after applying the <see cref="ScrollBarPadding"/></summary>
    public Rectangle? PaddedVSBBounds => VSBBounds?.GetCompressed(ScrollBarPadding);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Rectangle? _HSBBounds;
    public Rectangle? HSBBounds
    {
        get => _HSBBounds;
        private set
        {
            if (_HSBBounds != value)
            {
                var Previous = HSBBounds;
                _HSBBounds = value;
                NotifyPropertyChanged(nameof(HSBBounds));
                NotifyPropertyChanged(nameof(PaddedHSBBounds));
                HorizontalScrollBarBoundsChanged?.Invoke(Previous, HSBBounds);
            }
        }
    }

    public event EventHandler<Rectangle?> HorizontalScrollBarBoundsChanged;

    /// <summary>The screen bounds that the horizontal scrollbar will be rendered to, after applying the <see cref="ScrollBarPadding"/></summary>
    public Rectangle? PaddedHSBBounds => HSBBounds?.GetCompressed(ScrollBarPadding);

    #region Offset
    /// <summary>Invoked when either <see cref="HorizontalOffset"/> or <see cref="VerticalOffset"/> changes.<para/>
    /// See also: <see cref="HorizontalOffsetChanged"/>, <see cref="VerticalOffsetChanged"/></summary>
    public event EventHandler<EventArgs> OffsetChanged;
    public event EventHandler<EventArgs<float>> VerticalOffsetChanged;
    public event EventHandler<EventArgs<float>> HorizontalOffsetChanged;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _VerticalOffset;
    /// <summary>See also: <see cref="MaxVerticalOffset"/><para/>
    /// An external write (this setter): cancels an explicit <see cref="ScrollTo"/> run on this axis (keeping the written value), but leaves a
    /// <see cref="UITransition{T}"/> attached to <see cref="UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset"/> running: it retargets
    /// itself from its current animated value, like any other observed property.</summary>
    public float VerticalOffset
    {
        get => _VerticalOffset;
        set
        {
            var ClampedValue = Math.Clamp(value, 0, MaxVerticalOffset);
            CancelExternalScrollWrite(UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset);
            SetVerticalOffsetCore(ClampedValue);
        }
    }

    /// <summary>Writes <see cref="VerticalOffset"/> and raises the same notifications and events as the public setter, but never cancels a
    /// running animation on the path: used by the animated apply path (<see cref="ApplyAnimatedVerticalOffset"/>) and by the
    /// <see cref="MaxVerticalOffset"/> re-clamp, so a shrinking content lets an active <see cref="ScrollTo"/> run finish at the new bound
    /// instead of being cancelled.</summary>
    private void SetVerticalOffsetCore(float clampedValue)
    {
        if (_VerticalOffset != clampedValue)
        {
            var Previous = VerticalOffset;
            _VerticalOffset = clampedValue;
            ParentWindow.InvalidatePressedAndHoveredElements = true;
            NotifyPropertyChanged(nameof(VerticalOffset));
            VerticalOffsetChanged?.Invoke(this, new(Previous, VerticalOffset));
            OffsetChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Writes the animated value of <see cref="UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset"/> (clamped to the current
    /// bounds): used by that target's <c>SetValue</c>/<c>RestoreBaseValue</c>. Never cancels a run.</summary>
    internal void ApplyAnimatedVerticalOffset(float value) => SetVerticalOffsetCore(Math.Clamp(value, 0, MaxVerticalOffset));

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _MaxVerticalOffset;
    /// <summary>The maximum value that <see cref="VerticalOffset"/> can be set to.<br/>
    /// Setting <see cref="VerticalOffset"/> to this value will scroll to the bottom of the scrollable content.</summary>
    public float MaxVerticalOffset
    {
        get => _MaxVerticalOffset;
        private set
        {
            if (_MaxVerticalOffset != value)
            {
                var Previous = MaxVerticalOffset;
                _MaxVerticalOffset = value;
                //  The re-clamp is not an external write: a running ScrollTo (or transition) is left alone, and finishes at the new bound.
                SetVerticalOffsetCore(Math.Clamp(VerticalOffset, 0, MaxVerticalOffset));
                NotifyPropertyChanged(nameof(MaxVerticalOffset));
                MaxVerticalOffsetChanged?.Invoke(this, new(Previous, MaxVerticalOffset));
            }
        }
    }

    public event EventHandler<EventArgs<float>> MaxVerticalOffsetChanged;

    /// <summary>True if a <see cref="QueueScrollToBottom"/> request is waiting for the next layout pass.</summary>
    public bool IsScrollToBottomQueued { get; private set; }

    /// <summary>Attempts to immediately invoke <see cref="ScrollToBottom"/> if <see cref="MGElement.IsLayoutValid"/> is true.<br/>
    /// Else flags the request, and <see cref="ScrollToBottom"/> is invoked during the next layout pass, right after
    /// <see cref="MaxVerticalOffset"/> has been recomputed from the new content size.<para/>
    /// The request is a latch, not a queue: calling this repeatedly before the next layout pass coalesces into a
    /// single deferred scroll, so appending to scrollable content in a tight loop stays O(1) per call.</summary>
    public void QueueScrollToBottom()
    {
        if (IsLayoutValid)
        {
            ScrollToBottom();
        }
        else
        {
            IsScrollToBottomQueued = true;
        }
    }

    public void ScrollToTop() => VerticalOffset = 0;
    public void ScrollToBottom() => VerticalOffset = MaxVerticalOffset;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _HorizontalOffset;
    /// <summary>See also: <see cref="MaxHorizontalOffset"/><para/>
    /// An external write (this setter): cancels an explicit <see cref="ScrollTo"/> run on this axis (keeping the written value), but leaves a
    /// <see cref="UITransition{T}"/> attached to <see cref="UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset"/> running: it retargets
    /// itself from its current animated value, like any other observed property.</summary>
    public float HorizontalOffset
    {
        get => _HorizontalOffset;
        set
        {
            var ClampedValue = Math.Clamp(value, 0, MaxHorizontalOffset);
            CancelExternalScrollWrite(UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset);
            SetHorizontalOffsetCore(ClampedValue);
        }
    }

    /// <summary>Writes <see cref="HorizontalOffset"/> and raises the same notifications and events as the public setter, but never cancels a
    /// running animation on the path: used by the animated apply path (<see cref="ApplyAnimatedHorizontalOffset"/>) and by the
    /// <see cref="MaxHorizontalOffset"/> re-clamp, so a shrinking content lets an active <see cref="ScrollTo"/> run finish at the new bound
    /// instead of being cancelled.</summary>
    private void SetHorizontalOffsetCore(float clampedValue)
    {
        if (_HorizontalOffset != clampedValue)
        {
            var Previous = HorizontalOffset;
            _HorizontalOffset = clampedValue;
            ParentWindow.InvalidatePressedAndHoveredElements = true;
            NotifyPropertyChanged(nameof(HorizontalOffset));
            HorizontalOffsetChanged?.Invoke(this, new(Previous, HorizontalOffset));
            OffsetChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Writes the animated value of <see cref="UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset"/> (clamped to the current
    /// bounds): used by that target's <c>SetValue</c>/<c>RestoreBaseValue</c>. Never cancels a run.</summary>
    internal void ApplyAnimatedHorizontalOffset(float value) => SetHorizontalOffsetCore(Math.Clamp(value, 0, MaxHorizontalOffset));

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _MaxHorizontalOffset;
    /// <summary>The maximum value that <see cref="HorizontalOffset"/> can be set to.<br/>
    /// Setting <see cref="HorizontalOffset"/> to this value will scroll to the right-most of the scrollable content.</summary>
    public float MaxHorizontalOffset
    {
        get => _MaxHorizontalOffset;
        private set
        {
            if (_MaxHorizontalOffset != value)
            {
                var Previous = MaxHorizontalOffset;
                _MaxHorizontalOffset = value;
                NotifyPropertyChanged(nameof(MaxHorizontalOffset));
                //  The re-clamp is not an external write: a running ScrollTo (or transition) is left alone, and finishes at the new bound.
                SetHorizontalOffsetCore(Math.Clamp(HorizontalOffset, 0, MaxHorizontalOffset));
                MaxHorizontalOffsetChanged?.Invoke(this, new(Previous, MaxHorizontalOffset));
            }
        }
    }

    public event EventHandler<EventArgs<float>> MaxHorizontalOffsetChanged;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private UIPropertyAnimation<float> _VerticalScrollAnimation;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private UIPropertyAnimation<float> _HorizontalScrollAnimation;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TimeSpan _ScrollAnimationDuration;
    /// <summary>The duration <see cref="ScrollTo"/> uses for the mouse wheel (<see cref="MouseHandler"/>'s <c>Scrolled</c> handler) and for the
    /// keyboard/programmatic sites that call it with this viewer's own settings (<see cref="EnsureElementVisible"/>, a virtualized
    /// <see cref="MGListBox{TItemType}"/>'s focused item, <see cref="MGTreeView.ScrollIntoView"/>).<para/>
    /// Default value: <see cref="TimeSpan.Zero"/> (every one of those sites then writes the offset directly, unchanged behaviour).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan ScrollAnimationDuration
    {
        get => _ScrollAnimationDuration;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(ScrollAnimationDuration)} cannot be negative.");
            }

            if (_ScrollAnimationDuration != value)
            {
                _ScrollAnimationDuration = value;
                NotifyPropertyChanged(nameof(ScrollAnimationDuration));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IUIEasingFunction _ScrollAnimationEasing;
    /// <summary>The easing <see cref="ScrollAnimationDuration"/>'s sites pass to <see cref="ScrollTo"/>. Null means linear.</summary>
    public IUIEasingFunction ScrollAnimationEasing
    {
        get => _ScrollAnimationEasing;
        set
        {
            if (_ScrollAnimationEasing != value)
            {
                _ScrollAnimationEasing = value;
                NotifyPropertyChanged(nameof(ScrollAnimationEasing));
            }
        }
    }

    /// <summary>The destination of this viewer's own <see cref="ScrollTo"/> run on <see cref="VerticalOffset"/> while it is active (clamped to
    /// the current <c>[0, MaxVerticalOffset]</c> at read time); <see cref="VerticalOffset"/> otherwise (including while an application
    /// animation or a <see cref="UITransition{T}"/> drives the path).</summary>
    public float PendingVerticalOffset
        => _VerticalScrollAnimation != null && _VerticalScrollAnimation.IsActive
            ? Math.Clamp(_VerticalScrollAnimation.To, 0, MaxVerticalOffset)
            : VerticalOffset;

    /// <summary>The destination of this viewer's own <see cref="ScrollTo"/> run on <see cref="HorizontalOffset"/> while it is active (clamped
    /// to the current <c>[0, MaxHorizontalOffset]</c> at read time); <see cref="HorizontalOffset"/> otherwise (including while an application
    /// animation or a <see cref="UITransition{T}"/> drives the path).</summary>
    public float PendingHorizontalOffset
        => _HorizontalScrollAnimation != null && _HorizontalScrollAnimation.IsActive
            ? Math.Clamp(_HorizontalScrollAnimation.To, 0, MaxHorizontalOffset)
            : HorizontalOffset;

    /// <summary>Scrolls smoothly to the given destination(s) (a null axis is left untouched; each destination is clamped to
    /// <c>[0, Max...Offset]</c> at this call) over <paramref name="duration"/>, with <paramref name="easing"/> (null means linear).<para/>
    /// <paramref name="duration"/> zero or less, or this viewer having no desktop yet (<see cref="MGElement.SelfOrParentWindow"/>'s
    /// <see cref="MGWindow.Desktop"/> is null): a direct write through the public setter, which cancels any run on that axis exactly like any
    /// other external write.<para/>
    /// Otherwise starts (or restarts) one <see cref="UIPropertyAnimation{T}"/> per axis, reused across calls (a repeated wheel notch restarts
    /// the same instance instead of allocating a new one): <c>From</c> unset (starts from the current value), <c>CancelBehavior</c>
    /// <see cref="UIAnimationCancelBehavior.KeepCurrent"/>, <c>FillBehavior</c> <see cref="UIAnimationFillBehavior.HoldEnd"/>,
    /// <c>InheritsBaseValue</c> false (a plain target: nothing to restore).</summary>
    public void ScrollTo(float? horizontalOffset, float? verticalOffset, TimeSpan duration, IUIEasingFunction easing = null)
    {
        if (verticalOffset.HasValue)
        {
            ScrollVerticalTo(verticalOffset.Value, duration, easing);
        }

        if (horizontalOffset.HasValue)
        {
            ScrollHorizontalTo(horizontalOffset.Value, duration, easing);
        }
    }

    private void ScrollVerticalTo(float destination, TimeSpan duration, IUIEasingFunction easing)
    {
        var clamped = Math.Clamp(destination, 0, MaxVerticalOffset);
        if (duration <= TimeSpan.Zero || SelfOrParentWindow?.Desktop == null)
        {
            VerticalOffset = clamped;
            return;
        }

        var animation = _VerticalScrollAnimation ??= new UIPropertyAnimation<float>(UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset)
        {
            CancelBehavior = UIAnimationCancelBehavior.KeepCurrent,
            FillBehavior = UIAnimationFillBehavior.HoldEnd,
            InheritsBaseValue = false,
            Name = "scroll:vertical",
        };
        animation.ClearFrom();
        animation.To = clamped;
        animation.Duration = duration;
        animation.Easing = easing;
        Animations.Start(animation);
    }

    private void ScrollHorizontalTo(float destination, TimeSpan duration, IUIEasingFunction easing)
    {
        var clamped = Math.Clamp(destination, 0, MaxHorizontalOffset);
        if (duration <= TimeSpan.Zero || SelfOrParentWindow?.Desktop == null)
        {
            HorizontalOffset = clamped;
            return;
        }

        var animation = _HorizontalScrollAnimation ??= new UIPropertyAnimation<float>(UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset)
        {
            CancelBehavior = UIAnimationCancelBehavior.KeepCurrent,
            FillBehavior = UIAnimationFillBehavior.HoldEnd,
            InheritsBaseValue = false,
            Name = "scroll:horizontal",
        };
        animation.ClearFrom();
        animation.To = clamped;
        animation.Duration = duration;
        animation.Easing = easing;
        Animations.Start(animation);
    }

    /// <summary>Cancels the explicit run active on <paramref name="path"/>, if any, unless it is the run of a <see cref="UITransition{T}"/>
    /// attached to the same path (identified by reference to its own <see cref="UITransition{T}.Animation"/>, never by name): a transition
    /// keeps retargeting itself instead, through its own subscription to the property notification this write is about to raise.<para/>
    /// Never allocates the animation slot (<see cref="MGElement.AnimationSlotOrNull"/>): a viewer that animates nothing pays one null test.</summary>
    private void CancelExternalScrollWrite(string path)
    {
        var manager = SelfOrParentWindow?.Desktop?.Animations;
        var animation = manager?.GetActive(this, path);
        if (animation == null)
        {
            return;
        }

        if (AnimationSlotOrNull?.Transitions[path] is UITransition<float> transition && ReferenceEquals(transition.Animation, animation))
        {
            return;
        }

        animation.CancelCore(UIAnimationCancelBehavior.KeepCurrent);
    }
    #endregion Offset

    private bool IsHoveringVSB { get; set; }
    private bool IsDraggingVSB { get; set; }
    private bool IsVSBFocused => !VisualState.IsDisabled && (IsHoveringVSB || IsDraggingVSB);

    private bool IsHoveringHSB { get; set; }
    private bool IsDraggingHSB { get; set; }
    private bool IsHSBFocused => !VisualState.IsDisabled && (IsHoveringHSB || IsDraggingHSB);

    /// <summary>Must always be true for <see cref="MGScrollViewer"/> to avoid content that is out of the <see cref="ContentViewport"/>'s bounds from being visible.</summary>
    public override bool ClipToBounds
    { 
        get => base.ClipToBounds;
        set
        {
            if (value != true)
            {
                //throw new InvalidOperationException($"{nameof(MGScrollViewer)}.{nameof(ClipToBounds)} must always be true for scrollable content.");
                return;
            }
            base.ClipToBounds = value;
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private VisualStateFillBrush _ScrollBarOuterBrush;
    /// <summary>The brush to use for the outer portion of the scrollbars.<br/>
    /// Default value: <see cref="MGTheme.ScrollBarOuterBrush"/><para/>
    /// See also:<br/><see cref="MGWindow.Theme"/><br/><see cref="MGDesktop.Theme"/></summary>
    public VisualStateFillBrush ScrollBarOuterBrush
    {
        get => _ScrollBarOuterBrush;
        set
        {
            if (_ScrollBarOuterBrush != value)
            {
                _ScrollBarOuterBrush = value;
                NotifyPropertyChanged(nameof(ScrollBarOuterBrush));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private VisualStateFillBrush _ScrollBarInnerBrush;
    /// <summary>The brush to use for the inner portion of the scrollbars.<br/>
    /// Default value: <see cref="MGTheme.ScrollBarInnerBrush"/><para/>
    /// See also:<br/><see cref="MGWindow.Theme"/><br/><see cref="MGDesktop.Theme"/></summary>
    public VisualStateFillBrush ScrollBarInnerBrush
    {
        get => _ScrollBarInnerBrush;
        set
        {
            if (_ScrollBarInnerBrush != value)
            {
                _ScrollBarInnerBrush = value;
                NotifyPropertyChanged(nameof(ScrollBarInnerBrush));
            }
        }
    }

    protected override bool CanCacheSelfMeasurement => false; // The self measurement depends on the measurement of the children, so it must be re-calculated each time it's requested

    private const int RequestedContentMeasurementCacheSize = 2;
    private readonly Size[] _cachedRequestedContentAvailableSizes = new Size[RequestedContentMeasurementCacheSize];
    private readonly Size[] _cachedRequestedContentSizes = new Size[RequestedContentMeasurementCacheSize];
    private int _cachedRequestedContentCount;

    private void InvalidateRequestedContentSizeCache()
    {
        _cachedRequestedContentCount = 0;
    }

    private static Size ToRequestedContentSize(Thickness requestedContentSize)
        => new(requestedContentSize.Width, requestedContentSize.Height);

    private Size GetRequestedContentSizeForActualAvailableSize(Size actualAvailableSize)
    {
        if (!HasContent)
        {
            return Size.Empty;
        }

        for (var index = 0; index < _cachedRequestedContentCount; index++)
        {
            if (_cachedRequestedContentAvailableSizes[index] == actualAvailableSize)
            {
                return _cachedRequestedContentSizes[index];
            }
        }

        Content.UpdateMeasurement(actualAvailableSize, out _, out var requestedContentSize, out _, out _);
        var measuredRequestedContentSize = ToRequestedContentSize(requestedContentSize);

        var insertionIndex = Math.Min(_cachedRequestedContentCount, RequestedContentMeasurementCacheSize - 1);
        for (var index = insertionIndex; index > 0; index--)
        {
            _cachedRequestedContentAvailableSizes[index] = _cachedRequestedContentAvailableSizes[index - 1];
            _cachedRequestedContentSizes[index] = _cachedRequestedContentSizes[index - 1];
        }

        _cachedRequestedContentAvailableSizes[0] = actualAvailableSize;
        _cachedRequestedContentSizes[0] = measuredRequestedContentSize;
        _cachedRequestedContentCount = Math.Min(RequestedContentMeasurementCacheSize, _cachedRequestedContentCount + 1);

        return measuredRequestedContentSize;
    }

    private Size GetActualMeasurementAvailableSize(Size availableSize)
    {
        var actualAvailableWidth = HSBVisibility == ScrollBarVisibility.Disabled ? availableSize.Width : int.MaxValue;
        var actualAvailableHeight = VSBVisibility == ScrollBarVisibility.Disabled ? availableSize.Height : int.MaxValue;
        return new(actualAvailableWidth, actualAvailableHeight);
    }

    private Size MeasureRequestedContentSize(Size availableSize)
    {
        var actualAvailableSize = GetActualMeasurementAvailableSize(availableSize);
        return GetRequestedContentSizeForActualAvailableSize(actualAvailableSize);
    }

    private int GetActualVerticalScrollBarWidth(Size requestedContentSize)
    {
        return VSBVisibility switch
        {
            ScrollBarVisibility.Disabled => 0,
            ScrollBarVisibility.Auto => requestedContentSize.Height > AlignedContentBounds.Height ? VSBWidth : 0,
            ScrollBarVisibility.Hidden => VSBWidth,
            ScrollBarVisibility.Visible => VSBWidth,
            ScrollBarVisibility.Collapsed => 0,
            _ => throw new NotImplementedException($"Unrecognized {nameof(ScrollBarVisibility)}: {VSBVisibility}"),
        };
    }

    private int GetActualHorizontalScrollBarHeight(Size requestedContentSize)
    {
        return HSBVisibility switch
        {
            ScrollBarVisibility.Disabled => 0,
            ScrollBarVisibility.Auto => requestedContentSize.Width > AlignedContentBounds.Width ? HSBHeight : 0,
            ScrollBarVisibility.Hidden => HSBHeight,
            ScrollBarVisibility.Visible => HSBHeight,
            ScrollBarVisibility.Collapsed => 0,
            _ => throw new NotImplementedException($"Unrecognized {nameof(ScrollBarVisibility)}: {HSBVisibility}"),
        };
    }

    private void UpdateScrollMetrics(Size requestedContentSize, Size contentSize)
    {
        var actualVSBWidth = GetActualVerticalScrollBarWidth(requestedContentSize);
        var actualHSBHeight = GetActualHorizontalScrollBarHeight(requestedContentSize);

        Size scrollBarsSize = new(actualVSBWidth, actualHSBHeight);
        var viewportSize = LayoutBounds.Size.AsSize().Subtract(scrollBarsSize, 0, 0).Subtract(PaddingSize, 0, 0);
        ContentViewport = new(LayoutBounds.Left + Padding.Left, LayoutBounds.Top + Padding.Top, viewportSize.Width, viewportSize.Height);

        VSBBounds = actualVSBWidth == 0 ? null : new(LayoutBounds.Right - actualVSBWidth, LayoutBounds.Top, actualVSBWidth, LayoutBounds.Height - actualHSBHeight);
        HSBBounds = actualHSBHeight == 0 ? null : new(LayoutBounds.Left, LayoutBounds.Bottom - actualHSBHeight, LayoutBounds.Width - actualVSBWidth, actualHSBHeight);

        MaxVerticalOffset = Math.Max(0, contentSize.Height - ContentViewport.Height);
        MaxHorizontalOffset = Math.Max(0, contentSize.Width - ContentViewport.Width);

        //  Honor a deferred QueueScrollToBottom now that MaxVerticalOffset reflects the new content size.
        //  This is the deterministic completion point: it also fires when the new MaxVerticalOffset happens to
        //  be unchanged, which an approach based on MaxVerticalOffsetChanged would miss.
        if (IsScrollToBottomQueued)
        {
            IsScrollToBottomQueued = false;
            ScrollToBottom();
        }
    }

    public MGScrollViewer(MGWindow Window, ScrollBarVisibility VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ScrollBarVisibility HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled) 
        : base(Window, MGElementType.ScrollViewer)
    {
        using (BeginInitializing())
        {
            var Theme = GetTheme();

            VSBVisibility = VerticalScrollBarVisibility;
            HSBVisibility = HorizontalScrollBarVisibility;

            //Padding = new(0, 0, 5, 5);
            SetPadding(new(0), UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

            ScrollBarOuterBrush = Theme.ScrollBarOuterBrush.GetValue(true);
            ScrollBarInnerBrush = Theme.ScrollBarInnerBrush.GetValue(true);

            OnLayoutBoundsChanged += (sender, e) =>
            {
                var requestedContentSize = (VSBVisibility == ScrollBarVisibility.Auto || HSBVisibility == ScrollBarVisibility.Auto)
                    ? MeasureRequestedContentSize(AlignedContentBounds.Size.AsSize())
                    : Size.Empty;
                var contentSize = Content?.AllocatedBounds.Size.AsSize() ?? AlignedContentBounds.Size.AsSize();
                UpdateScrollMetrics(requestedContentSize, contentSize);
            };

            MouseHandler.MovedInside += (sender, e) =>
            {
                var LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.CurrentPosition);
                IsHoveringVSB = VSBBounds.HasValue && VSBBounds.Value.ContainsInclusive(LayoutSpacePosition);
                IsHoveringHSB = HSBBounds.HasValue && HSBBounds.Value.ContainsInclusive(LayoutSpacePosition);
            };

            MouseHandler.MovedOutside += (sender, e) =>
            {
                IsHoveringVSB = false;
                IsHoveringHSB = false;
            };

            MouseHandler.LMBPressedInside += (sender, e) =>
            {
                if (IsHoveringVSB)
                {
                    e.SetHandledBy(this, false);
                    var LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                    HandleScrollBarInput(Orientation.Vertical, e.Button, LayoutSpacePosition.Y);
                }

                if (IsHoveringHSB)
                {
                    e.SetHandledBy(this, false);
                    var LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                    HandleScrollBarInput(Orientation.Horizontal, e.Button, LayoutSpacePosition.X);
                }
            };

            MouseHandler.DragStartCondition = DragStartCondition.MousePressed;

            MouseHandler.DragStart += (sender, e) =>
            {
                if (e.IsLMB)
                {
                    if (IsHoveringVSB)
                    {
                        e.SetHandledBy(this, false);
                        IsDraggingVSB = true;
                    }

                    if (IsHoveringHSB)
                    {
                        e.SetHandledBy(this, false);
                        IsDraggingHSB = true;
                    }

                    if (AllowClickDragScrolling && !IsHoveringVSB && !IsHoveringHSB)
                    {
                        e.SetHandledBy(this, false);
                        IsDraggingContent = true;
                        _ContentDragStartVerticalOffset = VerticalOffset;
                        _ContentDragStartHorizontalOffset = HorizontalOffset;
                    }
                }
            };

            MouseHandler.DragEnd += (sender, e) =>
            {
                IsDraggingVSB = false;
                IsDraggingHSB = false;
                IsDraggingContent = false;
            };

            MouseHandler.Dragged += (sender, e) =>
            {
                if (IsDraggingVSB)
                {
                    e.SetHandled(this, false);
                    var LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                    HandleScrollBarInput(Orientation.Vertical, e.Button, LayoutSpacePosition.Y);
                }

                if (IsDraggingHSB)
                {
                    e.SetHandled(this, false);
                    var LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                    HandleScrollBarInput(Orientation.Horizontal, e.Button, LayoutSpacePosition.X);
                }

                if (IsDraggingContent)
                {
                    var DragStart = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.StartPosition);
                    var DragCurrent = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                    var Delta = DragCurrent - DragStart;
                    VerticalOffset = _ContentDragStartVerticalOffset - Delta.Y;
                    HorizontalOffset = _ContentDragStartHorizontalOffset - Delta.X;
                }
            };

            //  Pre-emptively handle mouse events if user was dragging a scrollbar so that the child content doesn't also react to the mouse event
            OnBeginUpdateContents += (sender, e) =>
            {
                if (IsDraggingVSB || IsDraggingHSB || IsDraggingContent)
                {
                    MouseHandler.Tracker.CurrentButtonReleasedEvents[MouseButton.Left]?.SetHandledBy(this, false);
                    foreach (var StartCondition in MouseHandler.DragStartConditions)
                    {
                        MouseHandler.Tracker.CurrentDragStartEvents[StartCondition][MouseButton.Left]?.SetHandledBy(this, false);
                    }
                }

                if (IsHoveringVSB || IsHoveringHSB)
                {
                    MouseHandler.Tracker.CurrentButtonPressedEvents[MouseButton.Left]?.SetHandledBy(this, false);
                }
            };

            //  This probably isn't needed
            MouseHandler.ReleasedOutside += (sender, e) =>
            {
                if (e.IsLMB && (IsDraggingVSB || IsDraggingHSB || IsDraggingContent))
                {
                    e.SetHandledBy(this, false);
                }
            };

            MouseHandler.Scrolled += (sender, e) =>
            {
                //  Attempt to scroll vertically. The guards read PendingVerticalOffset (the current value when nothing of this viewer's own
                //  is running, so a zero ScrollAnimationDuration is byte-for-byte the same as before this decision existed) so that consumption
                //  follows the destination of an in-flight smooth scroll rather than its momentary animated position.
                if (VSBBounds.HasValue)
                {
                    if (e.ScrollWheelDelta > 0 && PendingVerticalOffset > 0)
                    {
                        e.SetHandledBy(this, false);
                        ScrollTo(null, PendingVerticalOffset - VerticalScrollInterval, ScrollAnimationDuration, ScrollAnimationEasing);
                    }
                    else if (e.ScrollWheelDelta < 0 && PendingVerticalOffset < MaxVerticalOffset)
                    {
                        e.SetHandledBy(this, false);
                        ScrollTo(null, PendingVerticalOffset + VerticalScrollInterval, ScrollAnimationDuration, ScrollAnimationEasing);
                    }
                }
                //  Scroll horizontally if there is only a horizontal scrollbar but no vertical scrollbar
                else if (HSBBounds.HasValue)
                {
                    if (e.ScrollWheelDelta > 0 && PendingHorizontalOffset > 0)
                    {
                        e.SetHandledBy(this, false);
                        ScrollTo(PendingHorizontalOffset - VerticalScrollInterval, null, ScrollAnimationDuration, ScrollAnimationEasing);
                    }
                    else if (e.ScrollWheelDelta < 0 && PendingHorizontalOffset < MaxHorizontalOffset)
                    {
                        e.SetHandledBy(this, false);
                        ScrollTo(PendingHorizontalOffset + VerticalScrollInterval, null, ScrollAnimationDuration, ScrollAnimationEasing);
                    }
                }
            };
        }
    }

    /// <inheritdoc/>
    protected override IEnumerable<VisualStateFillBrush> GetVisualStateFillBrushes()
    {
        foreach (var Brush in base.GetVisualStateFillBrushes())
        {
            yield return Brush;
        }

        yield return ScrollBarOuterBrush;
        yield return ScrollBarInnerBrush;
    }

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);

        if (CurrentTheme == null)
        {
            return;
        }

        ScrollBarOuterBrush = CurrentTheme.ScrollBarOuterBrush.GetValue(true);
        ScrollBarInnerBrush = CurrentTheme.ScrollBarInnerBrush.GetValue(true);
    }

    protected override void LayoutChanged(MGElement Source, bool NotifyParent)
    {
        InvalidateRequestedContentSizeCache();
        base.LayoutChanged(Source, NotifyParent);
    }

    protected override void UpdateContents(ElementUpdateArgs UA)
    {
        Point ScrollOffset = new((int)HorizontalOffset, (int)VerticalOffset);
        var NewOffset = UA.Offset + ScrollOffset;
        base.UpdateContents(UA.ChangeOffset(NewOffset));
    }

    private void HandleScrollBarInput(Orientation ScrollBar, MouseButton Button, int CursorPosition)
    {
        if (Button == MouseButton.Left && HasContent)
        {
            var ContentSize = Content.AllocatedBounds.Size.AsSize();

            if (ScrollBar == Orientation.Vertical && VSBVisibility != ScrollBarVisibility.Disabled)
            {
                var ScrollBarBounds = PaddedVSBBounds.Value;

                var PercentInCurrentViewport = ContentViewport.Height * 1.0f / ContentSize.Height;
                var ScrollBarForegroundHeight = PercentInCurrentViewport * ScrollBarBounds.Height;

                float MinValue = 0;
                var MaxValue = ScrollBarBounds.Height - ScrollBarForegroundHeight;
                var AdjustedPosition = Math.Clamp(CursorPosition - ScrollBarBounds.Top - ScrollBarForegroundHeight / 2, MinValue, MaxValue);

                if (MaxValue == MinValue)
                {
                    VerticalOffset = MaxValue;
                }
                else
                {
                    VerticalOffset = (AdjustedPosition - MinValue) / (MaxValue - MinValue) * MaxVerticalOffset;
                }
            }
            else if (ScrollBar == Orientation.Horizontal && HSBVisibility != ScrollBarVisibility.Disabled)
            {
                var ScrollBarBounds = PaddedHSBBounds.Value;

                var PercentInCurrentViewport = ContentViewport.Width * 1.0f / ContentSize.Width;
                var ScrollBarForegroundWidth = PercentInCurrentViewport * ScrollBarBounds.Width;

                float MinValue = 0;
                var MaxValue = ScrollBarBounds.Width - ScrollBarForegroundWidth;
                var AdjustedPosition = Math.Clamp(CursorPosition - ScrollBarBounds.Left - ScrollBarForegroundWidth / 2, MinValue, MaxValue);

                if (MaxValue == MinValue)
                {
                    VerticalOffset = MaxValue;
                }
                else
                {
                    HorizontalOffset = (AdjustedPosition - MinValue) / (MaxValue - MinValue) * MaxHorizontalOffset;
                }
            }
        }
    }

    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        if (!HasContent)
        {
            return base.UpdateContentMeasurement(AvailableSize);
        }

        var requestedContentSize = MeasureRequestedContentSize(AvailableSize);
        return new Thickness(requestedContentSize.Width, requestedContentSize.Height, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        if (Content != null)
        {
            var ActualAvailableWidth = HSBVisibility == ScrollBarVisibility.Disabled ? Bounds.Width : int.MaxValue;
            var ActualAvailableHeight = VSBVisibility == ScrollBarVisibility.Disabled ? Bounds.Height : int.MaxValue;
            Size ActualAvailableSize = new(ActualAvailableWidth, ActualAvailableHeight);

            var ActualContentWidth = Bounds.Width;
            var ActualContentHeight = Bounds.Height;
            Size requestedContentSize = new(ActualContentWidth, ActualContentHeight);
            if (HSBVisibility != ScrollBarVisibility.Disabled || VSBVisibility != ScrollBarVisibility.Disabled)
            {
                requestedContentSize = GetRequestedContentSizeForActualAvailableSize(ActualAvailableSize);
                ActualContentWidth = Math.Max(ActualContentWidth, requestedContentSize.Width);
                ActualContentHeight = Math.Max(ActualContentHeight, requestedContentSize.Height);
            }

            var previousContentViewport = ContentViewport;
            UpdateScrollMetrics(requestedContentSize, new Size(ActualContentWidth, ActualContentHeight));
            Rectangle ActualBounds = new(Bounds.Left, Bounds.Top, ActualContentWidth, ActualContentHeight);
            var contentViewportChanged = ContentViewport != previousContentViewport;
            if (!Content.IsLayoutValid || Content.AllocatedBounds != ActualBounds || contentViewportChanged)
            {
                Content.UpdateLayout(ActualBounds);
            }
        }
        else
        {
            UpdateScrollMetrics(Size.Empty, Bounds.Size.AsSize());
            base.UpdateContentLayout(Bounds);
        }
    }

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
    {
        //  Measure the content to see if we need to display scrollbars that are using ScrollBarVisibility.Auto
        Thickness RequestedContentSize = new(0);
        if (VSBVisibility == ScrollBarVisibility.Auto || HSBVisibility == ScrollBarVisibility.Auto)
        {
            if (HasContent)
            {
                var ActualAvailableWidth = HSBVisibility == ScrollBarVisibility.Disabled ? AvailableSize.Width - HorizontalPadding : int.MaxValue;
                var ActualAvailableHeight = VSBVisibility == ScrollBarVisibility.Disabled ? AvailableSize.Height - VerticalPadding : int.MaxValue;
                Size ActualAvailableSize = new(ActualAvailableWidth, ActualAvailableHeight);
                var requestedContentSize = GetRequestedContentSizeForActualAvailableSize(ActualAvailableSize);
                RequestedContentSize = new Thickness(requestedContentSize.Width, requestedContentSize.Height, 0, 0);
            }
        }

        var ActualVSBWidth = VSBVisibility switch
        {
            ScrollBarVisibility.Disabled => 0,
            ScrollBarVisibility.Auto => RequestedContentSize.Height > AvailableSize.Height - VerticalPadding ? VSBWidth : 0,
            ScrollBarVisibility.Hidden => VSBWidth,
            ScrollBarVisibility.Visible => VSBWidth,
            ScrollBarVisibility.Collapsed => 0,
            _ => throw new NotImplementedException($"Unrecognized {nameof(ScrollBarVisibility)}: {VSBVisibility}"),
        };

        var ActualHSBHeight = HSBVisibility switch
        {
            ScrollBarVisibility.Disabled => 0,
            ScrollBarVisibility.Auto => RequestedContentSize.Width > AvailableSize.Width - HorizontalPadding ? HSBHeight : 0,
            ScrollBarVisibility.Hidden => HSBHeight,
            ScrollBarVisibility.Visible => HSBHeight,
            ScrollBarVisibility.Collapsed => 0,
            _ => throw new NotImplementedException($"Unrecognized {nameof(ScrollBarVisibility)}: {HSBVisibility}"),
        };

        SharedSize = new(0);
        return new(0, 0, ActualVSBWidth, ActualHSBHeight);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        if (HasContent)
        {
            var IsVSBRendered = VSBBounds.HasValue && VSBVisibility != ScrollBarVisibility.Hidden && VSBVisibility != ScrollBarVisibility.Collapsed;
            var IsHSBRendered = HSBBounds.HasValue && HSBVisibility != ScrollBarVisibility.Hidden && HSBVisibility != ScrollBarVisibility.Collapsed;

            var MinSize = 8; // Minimum size of the inner rectangle of a ScrollBar
            var ContentSize = Content.AllocatedBounds.Size.AsSize();

            if (IsVSBRendered)
            {
                //  Calculate the coordinates of the inner rectangle of the scrollbar
                var PaddedBounds = PaddedVSBBounds.Value;
                var PercentInCurrentViewport = ContentViewport.Height * 1.0f / ContentSize.Height;
                var PercentOutsideCurrentViewport = 1 - PercentInCurrentViewport;

                float StartY;
                if (MaxVerticalOffset.IsAlmostZero())
                {
                    StartY = PaddedBounds.Top;
                }
                else
                {
                    StartY = PaddedBounds.Top + PaddedBounds.Height * PercentOutsideCurrentViewport * VerticalOffset / MaxVerticalOffset;
                }

                var EndY = StartY + PercentInCurrentViewport * PaddedBounds.Height;

                //  Validate that the inner rectangle is at least 8 pixels big
                var Height = EndY - StartY + 1;
                if (Height < MinSize)
                {
                    //  Expand both ends by half of the difference
                    StartY -= (MinSize - Height) / 2;
                    EndY += (MinSize - Height) / 2;
                    Height = MinSize;

                    //  Ensure the Start/End are still within the outer bounds
                    if (StartY < PaddedBounds.Top)
                    {
                        EndY += PaddedBounds.Top - StartY;
                        StartY = PaddedBounds.Top;
                    }
                    if (EndY > PaddedBounds.Bottom)
                    {
                        EndY = PaddedBounds.Bottom;
                    }
                }

                var PrimaryState = IsVSBFocused ? PrimaryVisualState.Selected : VisualState.Primary;
                var SecondaryState = IsDraggingVSB ? SecondaryVisualState.Pressed : IsHoveringVSB ? SecondaryVisualState.Hovered : SecondaryVisualState.None;

                //  Intentional rectangle-first exception: scrollbar underlays/overlays are painted over the
                //  logical rectangular track/thumb bounds; MGScrollViewer exposes no CornerRadius / MGBoxShape
                //  chrome for the scrollbar (VisualShape != ContentClipShape, see Docs/rendering-architecture.md)
                //  Draw the outer rectangle of the scrollbar
                ScrollBarOuterBrush.GetUnderlay(PrimaryState)?.Draw(DA, this, VSBBounds.Value);
                ScrollBarOuterBrush.GetFillOverlay(SecondaryState)?.Draw(DA, this, VSBBounds.Value);

                //  Draw the inner rectangle of the scrollbar
                Rectangle ScrollBarVisibleBounds = new(PaddedBounds.Left, (int)StartY, (PaddedBounds.Width), (int)(EndY - StartY + 1));
                ScrollBarInnerBrush.GetUnderlay(PrimaryState)?.Draw(DA, this, ScrollBarVisibleBounds);
                ScrollBarInnerBrush.GetFillOverlay(SecondaryState)?.Draw(DA, this, ScrollBarVisibleBounds);
            }

            if (IsHSBRendered)
            {
                //  Calculate the coordinates of the inner rectangle of the scrollbar
                var PaddedBounds = PaddedHSBBounds.Value;
                var PercentInCurrentViewport = ContentViewport.Width * 1.0f / ContentSize.Width;
                var PercentOutsideCurrentViewport = 1 - PercentInCurrentViewport;

                float StartX;
                if (MaxHorizontalOffset.IsAlmostZero())
                {
                    StartX = PaddedBounds.Left;
                }
                else
                {
                    StartX = PaddedBounds.Left + PaddedBounds.Width * PercentOutsideCurrentViewport * HorizontalOffset / MaxHorizontalOffset;
                }

                var EndX = StartX + PercentInCurrentViewport * PaddedBounds.Width;

                //  Validate that the inner rectangle is at least 8 pixels big
                var Width = EndX - StartX + 1;
                if (Width < MinSize)
                {
                    //  Expand both ends by half of the difference
                    StartX -= (MinSize - Width) / 2;
                    EndX += (MinSize - Width) / 2;
                    Width = MinSize;

                    //  Ensure the Start/End are still within the outer bounds
                    if (StartX < PaddedBounds.Left)
                    {
                        EndX += PaddedBounds.Left - StartX;
                        StartX = PaddedBounds.Left;
                    }
                    if (EndX > PaddedBounds.Right)
                    {
                        EndX = PaddedBounds.Right;
                    }
                }

                var PrimaryState = IsHSBFocused ? PrimaryVisualState.Selected : VisualState.Primary;
                var SecondaryState = IsDraggingHSB ? SecondaryVisualState.Pressed : IsHoveringHSB ? SecondaryVisualState.Hovered : SecondaryVisualState.None;

                //  Intentional rectangle-first exception: scrollbar underlays/overlays are painted over the
                //  logical rectangular track/thumb bounds; MGScrollViewer exposes no CornerRadius / MGBoxShape
                //  chrome for the scrollbar (VisualShape != ContentClipShape, see Docs/rendering-architecture.md)
                //  Draw the outer rectangle of the scrollbar
                ScrollBarOuterBrush.GetUnderlay(PrimaryState)?.Draw(DA, this, HSBBounds.Value);
                ScrollBarOuterBrush.GetFillOverlay(SecondaryState)?.Draw(DA, this, HSBBounds.Value);

                //  Draw the inner rectangle of the scrollbar
                Rectangle ScrollBarVisibleBounds = new((int)StartX, PaddedBounds.Top, (int)(EndX - StartX + 1), PaddedBounds.Height);
                ScrollBarInnerBrush.GetUnderlay(PrimaryState)?.Draw(DA, this, ScrollBarVisibleBounds);
                ScrollBarInnerBrush.GetFillOverlay(SecondaryState)?.Draw(DA, this, ScrollBarVisibleBounds);
            }
        }
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        var NewOffset = DA.Offset - new Point((int)HorizontalOffset, (int)VerticalOffset);
        var adjustedDA = DA with { Offset = NewOffset };

        // CPU-side frustum culling (Task 15): skip direct content children whose ActualLayoutBounds
        // is empty (fully clipped), avoiding unnecessary Draw() call overhead for off-viewport elements.
        foreach (var child in GetChildren())
        {
            if (!child.ActualLayoutBounds.IsEmpty)
            {
                child.Draw(adjustedDA);
            }
        }
    }

    internal override ClipDefinition GetContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
    {
        //  Y10: built from the ambient draw transform (the same way MGElement.Draw computes targetBounds), instead of a coordinate-space
        //  conversion that only knew about MGWindow.Scale. Without any transform pushed, or under a pure scale (MGWindow.Scale != 1),
        //  this produces the exact same rectangle: CreateTransformedBoundsF reduces to the old two-corner mapping for a non-rotated matrix,
        //  and the ambient transform already carries MGWindow.Scale, the same matrix the previous ConvertCoordinateSpace call applied.
        //  Under a transformed ancestor (a sliding window, a RenderTransform), the clip now follows the content to where it is actually drawn.
        var screenBounds = TransformClipBounds(DA, ContentViewport);
        return CreateRectangleClipDefinition(screenBounds, $"{ElementType}.Viewport");
    }
}