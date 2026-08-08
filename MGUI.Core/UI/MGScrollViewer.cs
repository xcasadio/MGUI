using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Shared.Input.Mouse;
using System.Diagnostics;
using MGUI.Shared.Rendering.Clipping;

namespace MGUI.Core.UI
{
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

            float newOffset = currentOffset;
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

            float newOffset = currentOffset;
            float viewportEnd = viewportStart + viewportSize;
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

            Rectangle currentBounds = target.LayoutBounds;
            for (MGElement current = target.Parent; current != null; current = current.Parent)
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

            if (!TryGetDescendantBoundsInContentSpace(target, out Rectangle bounds))
            {
                return;
            }

            float verticalViewportStart = Content.LayoutBounds.Top + VerticalOffset;
            float horizontalViewportStart = Content.LayoutBounds.Left + HorizontalOffset;
            float newVerticalOffset = GetVisibleOffset(VerticalOffset, verticalViewportStart, ContentViewport.Height, MaxVerticalOffset, bounds.Top, bounds.Bottom);
            float newHorizontalOffset = GetVisibleOffset(HorizontalOffset, horizontalViewportStart, ContentViewport.Width, MaxHorizontalOffset, bounds.Left, bounds.Right);

            if (Math.Abs(newVerticalOffset - VerticalOffset) > 0.5f)
            {
                VerticalOffset = newVerticalOffset;
            }

            if (Math.Abs(newHorizontalOffset - HorizontalOffset) > 0.5f)
            {
                HorizontalOffset = newHorizontalOffset;
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
                    NPC(nameof(AllowClickDragScrolling));
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
                    NPC(nameof(VSBVisibility));
                    NPC(nameof(VerticalScrollBarVisibility));
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
                    NPC(nameof(HSBVisibility));
                    NPC(nameof(HorizontalScrollBarVisibility));
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
                    NPC(nameof(ContentViewport));
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
                    Rectangle? Previous = VSBBounds;
                    _VSBBounds = value;
                    NPC(nameof(VSBBounds));
                    NPC(nameof(PaddedVSBBounds));
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
                    Rectangle? Previous = HSBBounds;
                    _HSBBounds = value;
                    NPC(nameof(HSBBounds));
                    NPC(nameof(PaddedHSBBounds));
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
        /// <summary>See also: <see cref="MaxVerticalOffset"/></summary>
        public float VerticalOffset
        {
            get => _VerticalOffset;
            set
            {
                float ClampedValue = Math.Clamp(value, 0, MaxVerticalOffset);
                if (_VerticalOffset != ClampedValue)
                {
                    float Previous = VerticalOffset;
                    _VerticalOffset = ClampedValue;
                    ParentWindow.InvalidatePressedAndHoveredElements = true;
                    NPC(nameof(VerticalOffset));
                    VerticalOffsetChanged?.Invoke(this, new(Previous, VerticalOffset));
                    OffsetChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }


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
                    float Previous = MaxVerticalOffset;
                    _MaxVerticalOffset = value;
                    VerticalOffset = Math.Clamp(VerticalOffset, 0, MaxVerticalOffset);
                    NPC(nameof(MaxVerticalOffset));
                    MaxVerticalOffsetChanged?.Invoke(this, new(Previous, MaxVerticalOffset));
                }
            }
        }

        public event EventHandler<EventArgs<float>> MaxVerticalOffsetChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsScrollToBottomQueued;
        /// <summary>True if a <see cref="QueueScrollToBottom"/> request is waiting for the next layout pass.</summary>
        public bool IsScrollToBottomQueued => _IsScrollToBottomQueued;

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
                _IsScrollToBottomQueued = true;
            }
        }

        public void ScrollToTop() => VerticalOffset = 0;
        public void ScrollToBottom() => VerticalOffset = MaxVerticalOffset;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private float _HorizontalOffset;
        /// <summary>See also: <see cref="MaxHorizontalOffset"/></summary>
        public float HorizontalOffset
        {
            get => _HorizontalOffset;
            set
            {
                float ClampedValue = Math.Clamp(value, 0, MaxHorizontalOffset);
                if (_HorizontalOffset != ClampedValue)
                {
                    float Previous = HorizontalOffset;
                    _HorizontalOffset = ClampedValue;
                    ParentWindow.InvalidatePressedAndHoveredElements = true;
                    NPC(nameof(HorizontalOffset));
                    HorizontalOffsetChanged?.Invoke(this, new(Previous, HorizontalOffset));
                    OffsetChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

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
                    float Previous = MaxHorizontalOffset;
                    _MaxHorizontalOffset = value;
                    NPC(nameof(MaxHorizontalOffset));
                    HorizontalOffset = Math.Clamp(HorizontalOffset, 0, MaxHorizontalOffset);
                    MaxHorizontalOffsetChanged?.Invoke(this, new(Previous, MaxHorizontalOffset));
                }
            }
        }

        public event EventHandler<EventArgs<float>> MaxHorizontalOffsetChanged;
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
                    NPC(nameof(ScrollBarOuterBrush));
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
                    NPC(nameof(ScrollBarInnerBrush));
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

            for (int index = 0; index < _cachedRequestedContentCount; index++)
            {
                if (_cachedRequestedContentAvailableSizes[index] == actualAvailableSize)
                {
                    return _cachedRequestedContentSizes[index];
                }
            }

            Content.UpdateMeasurement(actualAvailableSize, out _, out Thickness requestedContentSize, out _, out _);
            Size measuredRequestedContentSize = ToRequestedContentSize(requestedContentSize);

            int insertionIndex = Math.Min(_cachedRequestedContentCount, RequestedContentMeasurementCacheSize - 1);
            for (int index = insertionIndex; index > 0; index--)
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
            int actualAvailableWidth = HSBVisibility == ScrollBarVisibility.Disabled ? availableSize.Width : int.MaxValue;
            int actualAvailableHeight = VSBVisibility == ScrollBarVisibility.Disabled ? availableSize.Height : int.MaxValue;
            return new(actualAvailableWidth, actualAvailableHeight);
        }

        private Size MeasureRequestedContentSize(Size availableSize)
        {
            Size actualAvailableSize = GetActualMeasurementAvailableSize(availableSize);
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
            int actualVSBWidth = GetActualVerticalScrollBarWidth(requestedContentSize);
            int actualHSBHeight = GetActualHorizontalScrollBarHeight(requestedContentSize);

            Size scrollBarsSize = new(actualVSBWidth, actualHSBHeight);
            Size viewportSize = LayoutBounds.Size.AsSize().Subtract(scrollBarsSize, 0, 0).Subtract(PaddingSize, 0, 0);
            ContentViewport = new(LayoutBounds.Left + Padding.Left, LayoutBounds.Top + Padding.Top, viewportSize.Width, viewportSize.Height);

            VSBBounds = actualVSBWidth == 0 ? null : new(LayoutBounds.Right - actualVSBWidth, LayoutBounds.Top, actualVSBWidth, LayoutBounds.Height - actualHSBHeight);
            HSBBounds = actualHSBHeight == 0 ? null : new(LayoutBounds.Left, LayoutBounds.Bottom - actualHSBHeight, LayoutBounds.Width - actualVSBWidth, actualHSBHeight);

            MaxVerticalOffset = Math.Max(0, contentSize.Height - ContentViewport.Height);
            MaxHorizontalOffset = Math.Max(0, contentSize.Width - ContentViewport.Width);

            //  Honor a deferred QueueScrollToBottom now that MaxVerticalOffset reflects the new content size.
            //  This is the deterministic completion point: it also fires when the new MaxVerticalOffset happens to
            //  be unchanged, which an approach based on MaxVerticalOffsetChanged would miss.
            if (_IsScrollToBottomQueued)
            {
                _IsScrollToBottomQueued = false;
                ScrollToBottom();
            }
        }

        public MGScrollViewer(MGWindow Window, ScrollBarVisibility VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ScrollBarVisibility HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled) 
            : base(Window, MGElementType.ScrollViewer)
        {
            using (BeginInitializing())
            {
                MGTheme Theme = GetTheme();

                VSBVisibility = VerticalScrollBarVisibility;
                HSBVisibility = HorizontalScrollBarVisibility;

                //Padding = new(0, 0, 5, 5);
                Padding = new(0);

                ScrollBarOuterBrush = Theme.ScrollBarOuterBrush.GetValue(true);
                ScrollBarInnerBrush = Theme.ScrollBarInnerBrush.GetValue(true);

                OnLayoutBoundsChanged += (sender, e) =>
                {
                    Size requestedContentSize = (VSBVisibility == ScrollBarVisibility.Auto || HSBVisibility == ScrollBarVisibility.Auto)
                        ? MeasureRequestedContentSize(AlignedContentBounds.Size.AsSize())
                        : Size.Empty;
                    Size contentSize = Content?.AllocatedBounds.Size.AsSize() ?? AlignedContentBounds.Size.AsSize();
                    UpdateScrollMetrics(requestedContentSize, contentSize);
                };

                MouseHandler.MovedInside += (sender, e) =>
                {
                    Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.CurrentPosition);
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
                        Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                        HandleScrollBarInput(Orientation.Vertical, e.Button, LayoutSpacePosition.Y);
                    }

                    if (IsHoveringHSB)
                    {
                        e.SetHandledBy(this, false);
                        Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
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
                        Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                        HandleScrollBarInput(Orientation.Vertical, e.Button, LayoutSpacePosition.Y);
                    }

                    if (IsDraggingHSB)
                    {
                        e.SetHandled(this, false);
                        Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                        HandleScrollBarInput(Orientation.Horizontal, e.Button, LayoutSpacePosition.X);
                    }

                    if (IsDraggingContent)
                    {
                        Point DragStart = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.StartPosition);
                        Point DragCurrent = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                        Point Delta = DragCurrent - DragStart;
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
                        foreach (DragStartCondition StartCondition in MouseHandler.DragStartConditions)
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
                    //  Attempt to scroll vertically
                    if (VSBBounds.HasValue)
                    {
                        if (e.ScrollWheelDelta > 0 && VerticalOffset > 0)
                        {
                            e.SetHandledBy(this, false);
                            VerticalOffset -= VerticalScrollInterval;
                        }
                        else if (e.ScrollWheelDelta < 0 && VerticalOffset < MaxVerticalOffset)
                        {
                            e.SetHandledBy(this, false);
                            VerticalOffset += VerticalScrollInterval;
                        }
                    }
                    //  Scroll horizontally if there is only a horizontal scrollbar but no vertical scrollbar
                    else if (HSBBounds.HasValue)
                    {
                        if (e.ScrollWheelDelta > 0 && HorizontalOffset > 0)
                        {
                            e.SetHandledBy(this, false);
                            HorizontalOffset -= VerticalScrollInterval;
                        }
                        else if (e.ScrollWheelDelta < 0 && HorizontalOffset < MaxHorizontalOffset)
                        {
                            e.SetHandledBy(this, false);
                            HorizontalOffset += VerticalScrollInterval;
                        }
                    }
                };
            }
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
            Point NewOffset = UA.Offset + ScrollOffset;
            base.UpdateContents(UA.ChangeOffset(NewOffset));
        }

        private void HandleScrollBarInput(Orientation ScrollBar, MouseButton Button, int CursorPosition)
        {
            if (Button == MouseButton.Left && HasContent)
            {
                Size ContentSize = Content.AllocatedBounds.Size.AsSize();

                if (ScrollBar == Orientation.Vertical && VSBVisibility != ScrollBarVisibility.Disabled)
                {
                    Rectangle ScrollBarBounds = PaddedVSBBounds.Value;

                    float PercentInCurrentViewport = ContentViewport.Height * 1.0f / ContentSize.Height;
                    float ScrollBarForegroundHeight = PercentInCurrentViewport * ScrollBarBounds.Height;

                    float MinValue = 0;
                    float MaxValue = ScrollBarBounds.Height - ScrollBarForegroundHeight;
                    float AdjustedPosition = Math.Clamp(CursorPosition - ScrollBarBounds.Top - ScrollBarForegroundHeight / 2, MinValue, MaxValue);

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
                    Rectangle ScrollBarBounds = PaddedHSBBounds.Value;

                    float PercentInCurrentViewport = ContentViewport.Width * 1.0f / ContentSize.Width;
                    float ScrollBarForegroundWidth = PercentInCurrentViewport * ScrollBarBounds.Width;

                    float MinValue = 0;
                    float MaxValue = ScrollBarBounds.Width - ScrollBarForegroundWidth;
                    float AdjustedPosition = Math.Clamp(CursorPosition - ScrollBarBounds.Left - ScrollBarForegroundWidth / 2, MinValue, MaxValue);

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

            Size requestedContentSize = MeasureRequestedContentSize(AvailableSize);
            return new Thickness(requestedContentSize.Width, requestedContentSize.Height, 0, 0);
        }

        protected override void UpdateContentLayout(Rectangle Bounds)
        {
            if (Content != null)
            {
                int ActualAvailableWidth = HSBVisibility == ScrollBarVisibility.Disabled ? Bounds.Width : int.MaxValue;
                int ActualAvailableHeight = VSBVisibility == ScrollBarVisibility.Disabled ? Bounds.Height : int.MaxValue;
                Size ActualAvailableSize = new(ActualAvailableWidth, ActualAvailableHeight);

                int ActualContentWidth = Bounds.Width;
                int ActualContentHeight = Bounds.Height;
                Size requestedContentSize = new(ActualContentWidth, ActualContentHeight);
                if (HSBVisibility != ScrollBarVisibility.Disabled || VSBVisibility != ScrollBarVisibility.Disabled)
                {
                    requestedContentSize = GetRequestedContentSizeForActualAvailableSize(ActualAvailableSize);
                    ActualContentWidth = Math.Max(ActualContentWidth, requestedContentSize.Width);
                    ActualContentHeight = Math.Max(ActualContentHeight, requestedContentSize.Height);
                }

                Rectangle previousContentViewport = ContentViewport;
                UpdateScrollMetrics(requestedContentSize, new Size(ActualContentWidth, ActualContentHeight));
                Rectangle ActualBounds = new(Bounds.Left, Bounds.Top, ActualContentWidth, ActualContentHeight);
                bool contentViewportChanged = ContentViewport != previousContentViewport;
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
                    int ActualAvailableWidth = HSBVisibility == ScrollBarVisibility.Disabled ? AvailableSize.Width - HorizontalPadding : int.MaxValue;
                    int ActualAvailableHeight = VSBVisibility == ScrollBarVisibility.Disabled ? AvailableSize.Height - VerticalPadding : int.MaxValue;
                    Size ActualAvailableSize = new(ActualAvailableWidth, ActualAvailableHeight);
                    Size requestedContentSize = GetRequestedContentSizeForActualAvailableSize(ActualAvailableSize);
                    RequestedContentSize = new Thickness(requestedContentSize.Width, requestedContentSize.Height, 0, 0);
                }
            }

            int ActualVSBWidth = VSBVisibility switch
            {
                ScrollBarVisibility.Disabled => 0,
                ScrollBarVisibility.Auto => RequestedContentSize.Height > AvailableSize.Height - VerticalPadding ? VSBWidth : 0,
                ScrollBarVisibility.Hidden => VSBWidth,
                ScrollBarVisibility.Visible => VSBWidth,
                ScrollBarVisibility.Collapsed => 0,
                _ => throw new NotImplementedException($"Unrecognized {nameof(ScrollBarVisibility)}: {VSBVisibility}"),
            };

            int ActualHSBHeight = HSBVisibility switch
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
                bool IsVSBRendered = VSBBounds.HasValue && VSBVisibility != ScrollBarVisibility.Hidden && VSBVisibility != ScrollBarVisibility.Collapsed;
                bool IsHSBRendered = HSBBounds.HasValue && HSBVisibility != ScrollBarVisibility.Hidden && HSBVisibility != ScrollBarVisibility.Collapsed;

                int MinSize = 8; // Minimum size of the inner rectangle of a ScrollBar
                Size ContentSize = Content.AllocatedBounds.Size.AsSize();

                if (IsVSBRendered)
                {
                    //  Calculate the coordinates of the inner rectangle of the scrollbar
                    Rectangle PaddedBounds = PaddedVSBBounds.Value;
                    float PercentInCurrentViewport = ContentViewport.Height * 1.0f / ContentSize.Height;
                    float PercentOutsideCurrentViewport = 1 - PercentInCurrentViewport;

                    float StartY;
                    if (MaxVerticalOffset.IsAlmostZero())
                    {
                        StartY = PaddedBounds.Top;
                    }
                    else
                    {
                        StartY = PaddedBounds.Top + PaddedBounds.Height * PercentOutsideCurrentViewport * VerticalOffset / MaxVerticalOffset;
                    }

                    float EndY = StartY + PercentInCurrentViewport * PaddedBounds.Height;

                    //  Validate that the inner rectangle is at least 8 pixels big
                    float Height = EndY - StartY + 1;
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

                    PrimaryVisualState PrimaryState = IsVSBFocused ? PrimaryVisualState.Selected : VisualState.Primary;
                    SecondaryVisualState SecondaryState = IsDraggingVSB ? SecondaryVisualState.Pressed : IsHoveringVSB ? SecondaryVisualState.Hovered : SecondaryVisualState.None;

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
                    Rectangle PaddedBounds = PaddedHSBBounds.Value;
                    float PercentInCurrentViewport = ContentViewport.Width * 1.0f / ContentSize.Width;
                    float PercentOutsideCurrentViewport = 1 - PercentInCurrentViewport;

                    float StartX;
                    if (MaxHorizontalOffset.IsAlmostZero())
                    {
                        StartX = PaddedBounds.Left;
                    }
                    else
                    {
                        StartX = PaddedBounds.Left + PaddedBounds.Width * PercentOutsideCurrentViewport * HorizontalOffset / MaxHorizontalOffset;
                    }

                    float EndX = StartX + PercentInCurrentViewport * PaddedBounds.Width;

                    //  Validate that the inner rectangle is at least 8 pixels big
                    float Width = EndX - StartX + 1;
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

                    PrimaryVisualState PrimaryState = IsHSBFocused ? PrimaryVisualState.Selected : VisualState.Primary;
                    SecondaryVisualState SecondaryState = IsDraggingHSB ? SecondaryVisualState.Pressed : IsHoveringHSB ? SecondaryVisualState.Hovered : SecondaryVisualState.None;

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
            Point NewOffset = DA.Offset - new Point((int)HorizontalOffset, (int)VerticalOffset);
            ElementDrawArgs adjustedDA = DA with { Offset = NewOffset };

            // CPU-side frustum culling (Task 15): skip direct content children whose ActualLayoutBounds
            // is empty (fully clipped), avoiding unnecessary Draw() call overhead for off-viewport elements.
            foreach (MGElement child in GetChildren())
            {
                if (!child.ActualLayoutBounds.IsEmpty)
                {
                    child.Draw(adjustedDA);
                }
            }
        }

        internal override ClipDefinition GetContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
        {
            Rectangle screenBounds = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Screen, ContentViewport.GetTranslated(DA.Offset));
            return CreateRectangleClipDefinition(screenBounds, $"{ElementType}.Viewport");
        }
    }
}
