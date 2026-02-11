using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls
{
    /// <summary>
    /// Visual splitter bar that can be dragged to resize the split container.
    /// Used internally by MGDockSplitContainer.
    /// </summary>
    public class MGDockSplitterBar : MGElement
    {
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
                    NPC(nameof(IsDragging));
                }
            }
        }

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
                    NPC(nameof(PressedBrush));
                }
            }
        }

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
                // Set default brushes
                NormalBrush = new MGSolidFillBrush(new Color(64, 64, 64));
                HoverBrush = new MGSolidFillBrush(new Color(80, 80, 80));
                PressedBrush = new MGSolidFillBrush(new Color(100, 100, 100));

                // Subscribe to mouse press event to start dragging
                MouseHandler.LMBPressedInside += OnLMBPressed;
            }
        }

        private void OnLMBPressed(object sender, BaseMousePressedEventArgs e)
        {
            if (ParentSplitContainer == null)
                return;

            IsDragging = true;
            _dragStartRatio = ParentSplitContainer.SplitRatio;
            _dragStartMousePosition = e.Position;
            
            e.SetHandledBy(this, false);
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            base.UpdateSelf(UA);

            if (IsDragging)
            {
                // Check if left mouse button is still pressed
                bool isStillPressed = ParentWindow.Desktop.InputTracker.Mouse.CurrentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
                
                if (!isStillPressed)
                {
                    // Mouse button released, end drag
                    IsDragging = false;
                    
                    // NOW sync the final ratio to the model
                    if (ParentSplitContainer != null)
                    {
                        float finalRatio = ParentSplitContainer.SplitRatio;
                        ParentSplitContainer.SetSplitRatio(finalRatio, clamp: false);
                    }
                    return;
                }

                if (ParentSplitContainer == null)
                    return;

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
                return ratio;

            Rectangle bounds = ParentSplitContainer.LayoutBounds;
            int splitterThickness = ParentSplitContainer.SplitterThickness;
            int minFirstSize = ParentSplitContainer.MinFirstSize;
            int minSecondSize = ParentSplitContainer.MinSecondSize;

            int availableSize = (ParentSplitContainer.Orientation == Orientation.Horizontal)
                ? bounds.Width - splitterThickness
                : bounds.Height - splitterThickness;

            if (availableSize <= 0)
                return ratio;

            // Calculate min/max ratios based on min sizes
            float minRatio = (float)minFirstSize / availableSize;
            float maxRatio = (float)(availableSize - minSecondSize) / availableSize;

            return Math.Clamp(ratio, minRatio, maxRatio);
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
                else
                {
                    // Horizontal bar for vertical split
                    return new Thickness(AvailableSize.Width, thickness, 0, 0);
                }
            }
            
            // Default size if no parent
            return new Thickness(4, 4, 0, 0);
        }
        
        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            // Choose brush based on state
            IFillBrush brush;
            if (IsDragging)
                brush = PressedBrush;
            else if (IsHovered)
                brush = HoverBrush;
            else
                brush = NormalBrush;

            // Draw background
            brush?.Draw(DA, this, LayoutBounds);

            // Draw grip dots in the center (optional visual enhancement)
            DrawGripDots(DA, LayoutBounds);

            DrawSelfBaseImplementation(DA, LayoutBounds);
        }

        /// <summary>
        /// Draws small grip dots in the center of the splitter bar.
        /// </summary>
        private void DrawGripDots(ElementDrawArgs DA, Rectangle bounds)
        {
            const int dotSize = 2;
            const int spacing = 4;
            const int dotCount = 5;

            Color dotColor = Color.White * 0.5f; // Semi-transparent white

            if (ParentSplitContainer?.Orientation == Orientation.Horizontal)
            {
                // Vertical dots for horizontal splitter
                int centerX = bounds.X + bounds.Width / 2;
                int centerY = bounds.Y + bounds.Height / 2;
                int startY = centerY - (dotCount * (dotSize + spacing)) / 2;

                for (int i = 0; i < dotCount; i++)
                {
                    int dotY = startY + i * (dotSize + spacing);
                    Rectangle dotRect = new Rectangle(centerX - dotSize / 2, dotY, dotSize, dotSize);
                    DA.DT.FillRectangle(Vector2.Zero, new RectangleF(dotRect.X, dotRect.Y, dotRect.Width, dotRect.Height), dotColor);
                }
            }
            else
            {
                // Horizontal dots for vertical splitter
                int centerX = bounds.X + bounds.Width / 2;
                int centerY = bounds.Y + bounds.Height / 2;
                int startX = centerX - (dotCount * (dotSize + spacing)) / 2;

                for (int i = 0; i < dotCount; i++)
                {
                    int dotX = startX + i * (dotSize + spacing);
                    Rectangle dotRect = new Rectangle(dotX, centerY - dotSize / 2, dotSize, dotSize);
                    DA.DT.FillRectangle(Vector2.Zero, new RectangleF(dotRect.X, dotRect.Y, dotRect.Width, dotRect.Height), dotColor);
                }
            }
        }
    }
}
