using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Helpers;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Rendering;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Shapes;
using MGUI.Core.UI.Styling;
using MGUI.Core.Tooling;

namespace MGUI.Core.UI
{
    public class CancelEventArgs<T> : CancelEventArgs
    {
        public T Data { get; }

        public CancelEventArgs(T Data)
            : base(false)
        {
            this.Data = Data;
        }
    }

    public class MGWindow : MGSingleContentHost
    {
        public const string BorderPartName = "PART_Border";
        public const string TitleBarPartName = "PART_TitleBar";
        public const string TitleBarTextPartName = "PART_TitleBarText";
        public const string CloseButtonPartName = "PART_CloseButton";
        public const string ResizeGripPartName = "PART_ResizeGrip";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(BorderPartName, typeof(MGBorder));
            yield return new(TitleBarPartName, typeof(MGDockPanel));
            yield return new(TitleBarTextPartName, typeof(MGTextBlock));
            yield return new(CloseButtonPartName, typeof(MGButton));
            yield return new(ResizeGripPartName, typeof(MGResizeGrip), false);
        }

        protected internal override void ApplyControlTemplate(bool IsThemeRefresh)
        {
            base.ApplyControlTemplate(IsThemeRefresh);

            HashSet<string> requiredParts = GetRequiredControlTemplateParts()
                .Select(x => x.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (!requiredParts.Contains(TitleBarPartName))
            {
                if (TitleBarComponent != null)
                {
                    RemoveComponent(TitleBarComponent);
                    TitleBarComponent = null;
                }

                TitleBarElement = null;
                TitleBarTextBlockElement = null;
                CloseButtonElement = null;
            }

            if (!requiredParts.Contains(ResizeGripPartName))
            {
                if (ResizeGripComponent != null)
                {
                    RemoveComponent(ResizeGripComponent);
                    ResizeGripComponent = null;
                }

                ResizeGripElement = null;
            }
        }

        public MGDesktop Desktop { get; }
        public MGElement DefaultFocusElement { get; set; }

        #region Position / Size
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Point TopLeft
        {
            get => new(Left, Top);
            set
            {
                Left = value.X;
                Top = value.Y;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _Left;
        public int Left
        {
            get => _Left;
            set
            {
                if (_Left != value)
                {
                    _Left = value;
                    NPC(nameof(Left));
                    NPC(nameof(TopLeft));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _Top;
        public int Top
        {
            get => _Top;
            set
            {
                if (_Top != value)
                {
                    _Top = value;
                    NPC(nameof(Top));
                    NPC(nameof(TopLeft));
                }
            }
        }

        /// <summary>Note: This event is not invoked immediately after <see cref="Left"/> or <see cref="Top"/> changes.<br/>
        /// It is invoked during the Update tick to improve performance by only allowing it to notify once per tick.</summary>
        public event EventHandler<EventArgs<(int Left, int Top)>> OnWindowPositionChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _WindowWidth;
        public int WindowWidth
        {
            get => _WindowWidth;
            set
            {
                int ActualValue = Math.Clamp(value, MinWidth ?? 0, MaxWidth ?? int.MaxValue);
                if (_WindowWidth != ActualValue)
                {
                    _WindowWidth = ActualValue;
                    LayoutChanged(this, true);
                    UpdateScaleTransforms();
                    RecentSizeToContentSettings = null;
                    NPC(nameof(WindowWidth));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _WindowHeight;
        public int WindowHeight
        {
            get => _WindowHeight;
            set
            {
                int ActualValue = Math.Clamp(value, MinHeight ?? 0, MaxHeight ?? int.MaxValue);
                if (_WindowHeight != ActualValue)
                {
                    _WindowHeight = ActualValue;
                    LayoutChanged(this, true);
                    UpdateScaleTransforms();
                    RecentSizeToContentSettings = null;
                    NPC(nameof(WindowHeight));
                }
            }
        }

        /// <summary>Note: This event is not invoked immediately after <see cref="WindowWidth"/> or <see cref="WindowHeight"/> changes.<br/>
        /// It is invoked during the Update tick to improve performance by only allowing it to notify once per tick.</summary>
        public event EventHandler<EventArgs<(int Width, int Height)>> OnWindowSizeChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int PreviousLeft;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int PreviousTop;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int PreviousWidth;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int PreviousHeight;

        protected void InvokeWindowPositionChanged(Point PreviousPosition, Point NewPosition)
            => OnWindowPositionChanged?.Invoke(this, new((PreviousPosition.X, PreviousPosition.Y), (NewPosition.X, NewPosition.Y)));

        /// <summary>Fires the <see cref="OnWindowPositionChanged"/> and/or <see cref="OnWindowSizeChanged"/> events if necessary.<para/>
        /// This method is automatically invoked at the beginning of <see cref="MGElement.Update(ElementUpdateArgs)"/>,<br/>
        /// but in rare cases you may want to manually invoke this after changing <see cref="Left"/>, <see cref="Top"/>, <see cref="WindowWidth"/>, or <see cref="WindowHeight"/> to make changes take effect immediately.</summary>
        public void ValidateWindowSizeAndPosition()
        {
            if (PreviousLeft != Left || PreviousTop != Top)
            {
                try
                {
                    UpdateScaleTransforms();
                    InvokeWindowPositionChanged(new(PreviousLeft, PreviousTop), new(Left, Top));
                }
                finally
                {
                    PreviousLeft = Left;
                    PreviousTop = Top;
                }
            }

            if (PreviousWidth != WindowWidth || PreviousHeight != WindowHeight)
            {
                try
                {
                    UpdateScaleTransforms();
#if NEVER
                    UpdateRenderTarget();
#endif
                    OnWindowSizeChanged?.Invoke(this, new((PreviousWidth, PreviousHeight), (WindowWidth, WindowHeight)));
                }
                finally
                {
                    PreviousWidth = WindowWidth;
                    PreviousHeight = WindowHeight;
                }
            }
        }

        internal static (MonoGame.Extended.Size MinSize, MonoGame.Extended.Size MaxSize) GetEffectiveSizeConstraints(int MinWidth, int MinHeight, int MaxWidth, int MaxHeight)
        {
            int ActualMaxWidth = Math.Max(0, MaxWidth);
            int ActualMaxHeight = Math.Max(0, MaxHeight);
            int ActualMinWidth = Math.Clamp(MinWidth, 0, ActualMaxWidth);
            int ActualMinHeight = Math.Clamp(MinHeight, 0, ActualMaxHeight);
            return (new(ActualMinWidth, ActualMinHeight), new(ActualMaxWidth, ActualMaxHeight));
        }

        public Size ComputeContentSize(int MinWidth = 100, int MinHeight = 100, int MaxWidth = 1920, int MaxHeight = 1080)
        {
            var (MinSize, MaxSize) = GetEffectiveSizeConstraints(
                MinWidth,
                MinHeight,
                Math.Min(GetDesktop().ValidScreenBounds.Width, MaxWidth),
                Math.Min(GetDesktop().ValidScreenBounds.Height, MaxHeight));
            UpdateMeasurement(MaxSize, out _, out Thickness FullSize, out _, out _);
            Size Size = FullSize.Size.Clamp(MinSize, MaxSize);
            return Size;
        }

        private readonly record struct SizeToContentSettings(SizeToContent Type, int MinWidth, int MinHeight, int? MaxWidth, int? MaxHeight);
        private SizeToContentSettings? RecentSizeToContentSettings = null;

        /// <summary>Resizes this <see cref="MGWindow"/> to satisfy the given constraints.</summary>
        /// <param name="UpdateLayoutImmediately">If true, the layout of child content is refreshed immediately rather than waiting until the next update tick.</param>
        /// <returns>The computed size that this <see cref="MGWindow"/> will be changed to.</returns>
        public Size ApplySizeToContent(SizeToContent Value, int MinWidth = 50, int MinHeight = 50, int? MaxWidth = 1920, int? MaxHeight = 1080, bool UpdateLayoutImmediately = true)
        {
            var (MinSize, MaxSize) = GetEffectiveSizeConstraints(
                MinWidth,
                MinHeight,
                Math.Min(GetDesktop().ValidScreenBounds.Width - Left, MaxWidth ?? int.MaxValue),
                Math.Min(GetDesktop().ValidScreenBounds.Height - Top, MaxHeight ?? int.MaxValue));
            Size AvailableSize = GetActualAvailableSize(new Size(WindowWidth, WindowHeight), Value).Clamp(MinSize, MaxSize);
            UpdateMeasurement(AvailableSize, out _, out Thickness FullSize, out _, out _);
            Size Size = FullSize.Size.Clamp(MinSize, MaxSize);
            WindowWidth = Size.Width;
            WindowHeight = Size.Height;
            LayoutChanged(this, true);

            if (UpdateLayoutImmediately)
            {
                ValidateWindowSizeAndPosition();
                UpdateLayout(new Rectangle(Left, Top, WindowWidth, WindowHeight));
            }

            RecentSizeToContentSettings = new(Value, MinWidth, MinHeight, MaxWidth, MaxHeight);

            return Size;
        }

        protected static Size GetActualAvailableSize(Size Size, SizeToContent SizeToContent)
        {
            int ActualAvailableWidth = SizeToContent switch
            {
                SizeToContent.Manual => Size.Width,
                SizeToContent.Width => int.MaxValue,
                SizeToContent.Height => Size.Width,
                SizeToContent.WidthAndHeight => int.MaxValue,
                _ => throw new NotImplementedException($"Unrecognized {nameof(SizeToContent)}: {SizeToContent}")
            };

            int ActualAvailableHeight = SizeToContent switch
            {
                SizeToContent.Manual => Size.Height,
                SizeToContent.Width => Size.Height,
                SizeToContent.Height => int.MaxValue,
                SizeToContent.WidthAndHeight => int.MaxValue,
                _ => throw new NotImplementedException($"Unrecognized {nameof(SizeToContent)}: {SizeToContent}")
            };

            Size ActualAvailableSize = new(ActualAvailableWidth, ActualAvailableHeight);

            return ActualAvailableSize;
        }

        #region Scale
        private float _Scale = 1.0f;
        /// <summary>Scales this <see cref="MGWindow"/> from the window's center point.<para/>
        /// Default value: 1.0f</summary>
        public float Scale
        {
            get => _Scale;
            set
            {
                if (_Scale != value)
                {
                    float Previous = Scale;
                    _Scale = value;
                    UpdateScaleTransforms();
#if NEVER
                    UpdateRenderTarget();
#endif
                    NPC(nameof(Scale));
                    NPC(nameof(IsWindowScaled));
                    ScaleChanged?.Invoke(this, new(Previous, Scale));
                }
            }
        }

        /// <summary>Invoked when <see cref="Scale"/> changes.</summary>
        public event EventHandler<EventArgs<float>> ScaleChanged;

        public bool IsWindowScaled => !Scale.IsAlmostEqual(1.0f);

        private Matrix _UnscaledScreenSpaceToScaledScreenSpace;
        /// <summary>A <see cref="Matrix"/> that converts coordinates that haven't accounted for <see cref="Scale"/> to coordinates that have.<para/>
        /// If <see cref="Scale"/> is 1.0f, this value is <see cref="Matrix.Identity"/><para/>
        /// See also: <see cref="ScaledScreenSpaceToUnscaledScreenSpace"/></summary>
        protected internal Matrix UnscaledScreenSpaceToScaledScreenSpace { get => _UnscaledScreenSpaceToScaledScreenSpace; }

        private Matrix _ScaledScreenSpaceToUnscaledScreenSpace;
        /// <summary>A <see cref="Matrix"/> that converts coordinates in screen space to coordinates that haven't accounted for <see cref="Scale"/>.<para/>
        /// If <see cref="Scale"/> is 1.0f, this value is <see cref="Matrix.Identity"/><para/>
        /// See also: <see cref="UnscaledScreenSpaceToScaledScreenSpace"/></summary>
        protected internal Matrix ScaledScreenSpaceToUnscaledScreenSpace { get => _ScaledScreenSpaceToUnscaledScreenSpace; }

        private void UpdateScaleTransforms()
        {
            if (IsWindowScaled)
            {
                Vector2 ScaleOrigin = TopLeft.ToVector2(); //TopLeft.ToVector2() + new Vector2(WindowWidth / 2, WindowHeight / 2); // Center of window
                _UnscaledScreenSpaceToScaledScreenSpace =
                    Matrix.CreateTranslation(new Vector3(-ScaleOrigin, 0)) *
                    Matrix.CreateScale(Scale) *
                    Matrix.CreateTranslation(new Vector3(ScaleOrigin, 0));
                _ScaledScreenSpaceToUnscaledScreenSpace = Matrix.Invert(UnscaledScreenSpaceToScaledScreenSpace);
            }
            else
            {
                _UnscaledScreenSpaceToScaledScreenSpace = Matrix.Identity;
                _ScaledScreenSpaceToUnscaledScreenSpace = Matrix.Identity;
            }
        }

        #endregion Scale

        #region Resizing
        /// <summary>Provides direct access to the resizer grip that appears in the bottom-right corner of this textbox when <see cref="IsUserResizable"/> is true.</summary>
        public MGComponent<MGResizeGrip> ResizeGripComponent { get; private set; }
        private MGResizeGrip ResizeGripElement { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsUserResizable;
        /// <summary>If true, a <see cref="MGResizeGrip"/> will be visible in the bottom-right corner of the window, allowing the user to click+drag it to adjust this <see cref="MGWindow"/>'s <see cref="WindowWidth"/>/<see cref="WindowHeight"/><para/>
        /// Default value: true (except in special-cases such as <see cref="MGComboBox{TItemType}.Dropdown"/> window or for <see cref="MGToolTip"/>s)</summary>
        public bool IsUserResizable
        {
            get => _IsUserResizable;
            set
            {
                if (_IsUserResizable != value)
                {
                    _IsUserResizable = value;
                    if (ResizeGripElement != null)
                    {
                        ResizeGripElement.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                    }
                    NPC(nameof(IsUserResizable));
                }
                else
                {
                    if (ResizeGripElement != null)
                    {
                        ResizeGripElement.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }
        #endregion Resizing
        #endregion Position / Size

        #region Border
        /// <summary>Provides direct access to this element's border.</summary>
        public MGComponent<MGBorder> BorderComponent { get; private set; }
        private MGBorder BorderElement { get; set; }
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

        #region Nested Windows
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly List<MGWindow> _ModalWindows;
        public IReadOnlyList<MGWindow> ModalWindows => _ModalWindows;

        /// <summary>A child <see cref="MGWindow"/> of this <see cref="MGWindow"/>, which blocks all input handling on this <see cref="MGWindow"/></summary>
        public MGWindow ModalWindow
        {
            get => _ModalWindows.LastOrDefault();
            set => SetModalWindow(value);
        }

        private void SetModalWindow(MGWindow value)
        {
            if (_ModalWindows.Count == 1 && ReferenceEquals(_ModalWindows[0], value))
            {
                return;
            }

            MGWindow previousTopModal = ModalWindow;
            List<MGWindow> previousModalWindows = _ModalWindows.ToList();

            _ModalWindows.Clear();
            if (value != null)
            {
                _ModalWindows.Add(value);
            }

            SynchronizeModalState(previousModalWindows, previousTopModal);
        }

        public void PushModalWindow(MGWindow modalWindow)
        {
            if (modalWindow == null)
            {
                throw new ArgumentNullException(nameof(modalWindow));
            }

            if (_ModalWindows.Contains(modalWindow))
            {
                throw new ArgumentException("Cannot add the same modal window multiple times.", nameof(modalWindow));
            }

            if (modalWindow == this)
            {
                throw new ArgumentException("Cannot add a window as a modal child of itself.", nameof(modalWindow));
            }

            MGWindow previousTopModal = ModalWindow;
            List<MGWindow> previousModalWindows = _ModalWindows.ToList();
            _ModalWindows.Add(modalWindow);
            SynchronizeModalState(previousModalWindows, previousTopModal);
        }

        public bool RemoveModalWindow(MGWindow modalWindow)
        {
            if (modalWindow == null || !_ModalWindows.Contains(modalWindow))
            {
                return false;
            }

            MGWindow previousTopModal = ModalWindow;
            List<MGWindow> previousModalWindows = _ModalWindows.ToList();
            _ModalWindows.Remove(modalWindow);
            SynchronizeModalState(previousModalWindows, previousTopModal);
            return true;
        }

        private void SynchronizeModalState(IReadOnlyList<MGWindow> previousModalWindows, MGWindow previousTopModal)
        {
            IReadOnlyList<MGWindow> currentModalWindows = _ModalWindows.ToList();
            MGWindow currentTopModal = ModalWindow;

            foreach (MGWindow removedModal in previousModalWindows.Where(x => !currentModalWindows.Contains(x)))
            {
                Desktop.NotifyWindowClosed(removedModal);
                removedModal.NPC(nameof(IsModalWindow));
            }

            foreach (MGWindow addedModal in currentModalWindows.Where(x => !previousModalWindows.Contains(x)))
            {
                Desktop.NotifyWindowOpened(addedModal);
                addedModal.NPC(nameof(IsModalWindow));
            }

            Desktop.SyncModalStack(this, currentModalWindows);

            NPC(nameof(ModalWindows));
            NPC(nameof(ModalWindow));
            NPC(nameof(HasModalWindow));

            if (!ReferenceEquals(previousTopModal, currentTopModal))
            {
                previousTopModal?.NPC(nameof(IsModalWindow));
                currentTopModal?.NPC(nameof(IsModalWindow));
            }
        }
        /// <summary>True if a modal window is being displayed overtop of this window.</summary>
        public bool HasModalWindow => _ModalWindows.Count > 0;
        /// <summary>True if this window instance is the modal window of its parent window.</summary>
        public bool IsModalWindow => ParentWindow?.ModalWindow == this;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly List<MGWindow> _NestedWindows;
        /// <summary>The last element represents the <see cref="MGWindow"/> that will be 
        /// drawn last (I.E., rendered overtop of everything else), and updated first (I.E., has the first chance to handle inputs)<br/>
        /// except in cases where a Topmost window is prioritized (See: <see cref="IsTopmost"/>)<para/>
        /// This list does not include <see cref="ModalWindow"/>, which is always prioritized over all <see cref="NestedWindows"/></summary>
        public IReadOnlyList<MGWindow> NestedWindows => _NestedWindows;
        public void AddNestedWindow(MGWindow NestedWindow)
        {
            if (NestedWindow == null)
            {
                throw new ArgumentNullException(nameof(NestedWindow));
            }

            if (_NestedWindows.Contains(NestedWindow))
            {
                throw new ArgumentException("Cannot add the same nested window to a parent window multiple times.");
            }

            if (NestedWindow == this)
            {
                throw new ArgumentException("Cannot add a window as a nested window to itself as this would create an infinite recursive dependency.");
            }

            _NestedWindows.Add(NestedWindow);
            Desktop.NotifyWindowOpened(NestedWindow);
        }
        public bool RemoveNestedWindow(MGWindow NestedWindow) => _NestedWindows.Remove(NestedWindow);

        /// <summary>Moves the given <paramref name="NestedWindow"/> to the end of <see cref="NestedWindows"/> list.<br/>
        /// It will typically be rendered overtop of all other <see cref="NestedWindows"/>, unless another nested window is Topmost (See: <see cref="IsTopmost"/>).<para/>
        /// If there is a <see cref="ModalWindow"/>, then the <see cref="ModalWindow"/> will be rendered overtop of the front-most <paramref name="NestedWindow"/></summary>
        /// <param name="NestedWindow"></param>
        /// <returns>True if the <paramref name="NestedWindow"/> was brought to the front. False if it was not a valid element in <see cref="NestedWindows"/>.</returns>
        public bool BringToFront(MGWindow NestedWindow)
        {
            if (!_NestedWindows.Contains(NestedWindow))
            {
                return false;
            }
            else
            {
                if (_NestedWindows.IndexOf(NestedWindow) != _NestedWindows.Count - 1)
                {
                    _NestedWindows.Remove(NestedWindow);
                    _NestedWindows.Add(NestedWindow);
                }
                return true;
            }
        }

        /// <summary>Moves the given <paramref name="NestedWindow"/> to the start of <see cref="NestedWindows"/> list.<br/>
        /// It will typically be rendered underneath of all other <see cref="NestedWindows"/>, unless it is Topmost (See: <see cref="IsTopmost"/>)</summary>
        /// <param name="NestedWindow"></param>
        /// <returns>True if the <paramref name="NestedWindow"/> was moved to the back. False if it was not a valid element in <see cref="NestedWindows"/>.</returns>
        public bool BringToBack(MGWindow NestedWindow)
        {
            if (!_NestedWindows.Contains(NestedWindow))
            {
                return false;
            }
            else
            {
                if (_NestedWindows.IndexOf(NestedWindow) != 0)
                {
                    _NestedWindows.Remove(NestedWindow);
                    _NestedWindows.Insert(0, NestedWindow);
                }
                return true;
            }
        }

        public IEnumerable<MGWindow> RecurseNestedWindows(bool IncludeSelf, TreeTraversalMode TraversalMode = TreeTraversalMode.Postorder)
        {
            if (IncludeSelf && TraversalMode == TreeTraversalMode.Preorder)
            {
                yield return this;
            }

            foreach (MGWindow Nested in NestedWindows)
            {
                foreach (MGWindow Item in Nested.RecurseNestedWindows(true, TraversalMode))
                {
                    yield return Item;
                }
            }

            if (IncludeSelf && TraversalMode == TreeTraversalMode.Postorder)
            {
                yield return this;
            }
        }
        #endregion Nested Windows

        #region Title Bar
        /// <summary>Provides direct access to the dockpanel component that displays this window's title-bar content.<para/>
        /// See also: <see cref="IsTitleBarVisible"/>, <see cref="TitleBarTextBlockElement"/></summary>
        public MGComponent<MGDockPanel> TitleBarComponent { get; private set; }
        private MGDockPanel TitleBarElement { get; set; }

        /// <summary>The textblock element that contains this window's <see cref="TitleText"/> in the title-bar.</summary>
        public MGTextBlock TitleBarTextBlockElement { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _TitleText;

        /// <summary>This property is functionally equivalent to <see cref="TitleBarTextBlockElement"/>'s <see cref="MGTextBlock.Text"/> property.<para/>
        /// See also: <see cref="IsTitleBarVisible"/></summary>
        public string TitleText
        {
            get => TitleBarTextBlockElement?.Text ?? _TitleText;
            set
            {
                if (TitleText != value)
                {
                    _TitleText = value;
                    if (TitleBarTextBlockElement != null)
                    {
                        TitleBarTextBlockElement.Text = value;
                    }
                    NPC(nameof(TitleText));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsTitleBarVisible;

        /// <summary>True if the title bar should be visible at the top of this <see cref="MGWindow"/><para/>
        /// Default value: true for most types of <see cref="MGWindow"/>, false for <see cref="MGToolTip"/></summary>
        public bool IsTitleBarVisible
        {
            get => TitleBarElement?.Visibility == Visibility.Visible || (TitleBarElement == null && _IsTitleBarVisible);
            set
            {
                Visibility ActualValue = value ? Visibility.Visible : Visibility.Collapsed;
                if (_IsTitleBarVisible != value)
                {
                    _IsTitleBarVisible = value;
                    if (TitleBarElement != null)
                    {
                        TitleBarElement.Visibility = ActualValue;
                    }
                    RefreshTitleBarLayoutParticipation();
                    NPC(nameof(IsTitleBarVisible));
                }
                else
                {
                    if (TitleBarElement != null)
                    {
                        TitleBarElement.Visibility = ActualValue;
                    }

                    RefreshTitleBarLayoutParticipation();
                }
            }
        }

        private void RefreshTitleBarLayoutParticipation()
        {
            if (TitleBarComponent != null)
            {
                TitleBarComponent.ConsumesTopSpace = _IsTitleBarVisible;
            }
        }

        #region Close
        public MGButton CloseButtonElement { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsCloseButtonVisible;

        public bool IsCloseButtonVisible
        {
            get => CloseButtonElement?.Visibility == Visibility.Visible || (CloseButtonElement == null && _IsCloseButtonVisible);
            set
            {
                Visibility ActualValue = value ? Visibility.Visible : Visibility.Collapsed;
                if (_IsCloseButtonVisible != value)
                {
                    _IsCloseButtonVisible = value;
                    if (CloseButtonElement != null)
                    {
                        CloseButtonElement.Visibility = ActualValue;
                    }
                    NPC(nameof(IsCloseButtonVisible));
                }
                else
                {
                    if (CloseButtonElement != null)
                    {
                        CloseButtonElement.Visibility = ActualValue;
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _CanCloseWindow = true;
        public bool CanCloseWindow
        {
            get => _CanCloseWindow;
            set
            {
                if (_CanCloseWindow != value)
                {
                    _CanCloseWindow = value;
                    NPC(nameof(CanCloseWindow));
                }
            }
        }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
        {
            BorderElement = Structure.Parts[BorderPartName] as MGBorder;
            TitleBarElement = Structure.Parts[TitleBarPartName] as MGDockPanel;
            TitleBarTextBlockElement = Structure.Parts[TitleBarTextPartName] as MGTextBlock;
            CloseButtonElement = Structure.Parts[CloseButtonPartName] as MGButton;
            ResizeGripElement = Structure.Parts.TryGetValue(ResizeGripPartName, out MGElement resizeGrip) ? resizeGrip as MGResizeGrip : null;

            bool needsBorderNotifications = BorderComponent == null || !ReferenceEquals(BorderComponent.Element, BorderElement);
            EnsureComponentBinding(() => BorderComponent, value => BorderComponent = value, BorderElement, MGComponentBase.Create);
            if (needsBorderNotifications)
            {
                BorderElement.OnBorderBrushChanged += (sender, e) => { NPC(nameof(BorderBrush)); };
                BorderElement.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(BorderThickness)); };
                BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };
            }

            EnsureComponentBinding(() => TitleBarComponent, value => TitleBarComponent = value, TitleBarElement,
                element => new(element, true, false, true, true, false, false, false,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Stretch, VerticalAlignment.Top, ComponentSize.Size)));

            EnsureComponentBinding(() => ResizeGripComponent, value => ResizeGripComponent = value, ResizeGripElement, MGComponentBase.Create);

            TitleBarElement.DrawBackgroundEnabled = false;
            TitleBarElement.CanChangeContent = false;
            TitleText = _TitleText;
            IsTitleBarVisible = _IsTitleBarVisible;
            RefreshTitleBarLayoutParticipation();
            IsCloseButtonVisible = _IsCloseButtonVisible;
            IsUserResizable = _IsUserResizable;
        }

        public bool TryCloseWindow()
        {
            if (!CanCloseWindow)
            {
                return false;
            }

            if ((ParentWindow != null && (ParentWindow.NestedWindows.Contains(this) || ParentWindow.ModalWindows.Contains(this))) 
                || (ParentWindow == null && Desktop.Windows.Contains(this)))
            {
                if (WindowClosing != null)
                {
                    CancelEventArgs ClosingArgs = new();
                    WindowClosing.Invoke(this, ClosingArgs);
                    if (ClosingArgs.Cancel)
                    {
                        return false;
                    }
                }

                bool IsClosed = false;
                if (ParentWindow != null && ParentWindow.ModalWindows.Contains(this))
                {
                    IsClosed = ParentWindow.RemoveModalWindow(this);
                }    
                if (ParentWindow != null && ParentWindow.NestedWindows.Contains(this))
                {
                    IsClosed = ParentWindow.RemoveNestedWindow(this);
                }
                else if (ParentWindow == null && Desktop.Windows.Contains(this))
                {
                    IsClosed = Desktop.Windows.Remove(this);
                }

                if (IsClosed)
                {
                    Desktop.NotifyWindowClosed(this);
                    WindowClosed?.Invoke(this, EventArgs.Empty);
                }
                return IsClosed;
            }

            return false;
        }

        public event EventHandler<CancelEventArgs> WindowClosing;
        public event EventHandler<EventArgs> WindowClosed;
        #endregion Close
        #endregion Title Bar

        #region RadioButton Groups
        private Dictionary<string, MGRadioButtonGroup> RadioButtonGroups { get; }
        public bool HasRadioButtonGroup(string Name) => RadioButtonGroups.ContainsKey(Name);
        public MGRadioButtonGroup GetOrCreateRadioButtonGroup(string Name)
        {
            if (RadioButtonGroups.TryGetValue(Name, out MGRadioButtonGroup ExistingGroup))
            {
                return ExistingGroup;
            }
            else
            {
                MGRadioButtonGroup NewGroup = new(this, Name);
                RadioButtonGroups.Add(Name, NewGroup);
                return NewGroup;
            }
        }
        #endregion RadioButton Groups

        #region Named ToolTips
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Dictionary<string, MGToolTip> _NamedToolTips { get; }
        /// <summary>This dictionary is commonly used by <see cref="MGTextBlock"/> to reference <see cref="MGToolTip"/>s by a string key value.<para/>
        /// See also:<br/><see cref="AddNamedToolTip(string, MGToolTip)"/><br/><see cref="RemoveNamedToolTip(string)"/><para/>
        /// EX: If you create an <see cref="MGTextBlock"/> and set its text to:
        /// <code>[ToolTip=ABC]This text has a ToolTip[/ToolTip] but this text doesn't</code>
        /// then the ToolTip with the name "ABC" will be shown when hovering over the substring "This text has a ToolTip"</summary>
        [Obsolete("Use Desktop.Resources.NamedToolTips (or Desktop.Resources.AddNamedToolTip) to register shared ToolTips at the desktop level.")]
        public IReadOnlyDictionary<string, MGToolTip> NamedToolTips => _NamedToolTips;

        [Obsolete("Use Desktop.Resources.AddNamedToolTip(name, tooltip) instead.")]
        public void AddNamedToolTip(string Name, MGToolTip ToolTip) => _NamedToolTips.Add(Name, ToolTip);
        [Obsolete("Use Desktop.Resources.RemoveNamedToolTip(name) instead.")]
        public void RemoveNamedToolTip(string Name) => _NamedToolTips.Remove(Name);

        /// <summary>Searches both the window-local <see cref="NamedToolTips"/> and the desktop <see cref="MGResources.NamedToolTips"/> for a tooltip with the given name.</summary>
        internal bool TryGetNamedToolTip(string Name, out MGToolTip ToolTip)
        {
            if (Name != null && _NamedToolTips.TryGetValue(Name, out ToolTip))
            {
                return true;
            }

            return GetResources().TryGetNamedToolTip(Name, out ToolTip);
        }
        #endregion Named ToolTips

        /// <summary>A <see cref="MouseHandler"/> that is updated just before <see cref="MGElement.MouseHandler"/> is updated.<para/>
        /// This allows subscribing to mouse events that can get handled just before the <see cref="MGWindow"/>'s input handling can occur.</summary>
        public MouseHandler WindowMouseHandler { get; }
        /// <summary>A <see cref="KeyboardHandler"/> owned by this <see cref="MGWindow"/> instance. Since <see cref="MGElement"/> only
        /// reports <see cref="IKeyboardHandlerHost.HasKeyboardFocus"/> as true when the desktop's
        /// <see cref="MGDesktop.FocusedKeyboardHandler"/> is this exact element, and a window itself never becomes the focused
        /// keyboard handler, this handler never receives <see cref="KeyboardHandler.Pressed"/>/<see cref="KeyboardHandler.Released"/>/
        /// <see cref="KeyboardHandler.KeyRepeat"/> - it is effectively inert.</summary>
        [Obsolete("WindowKeyboardHandler never receives events because an MGWindow never holds keyboard focus itself " +
            "(see MGElement's explicit IKeyboardHandlerHost.HasKeyboardFocus implementation). Use PreviewKeyboardHandler instead: it is " +
            "pumped every tick before this window's content children are updated, seeing the tick's keys whether keyboard focus is on the " +
            "window or on any descendant. Kept for source compatibility since external code may already subscribe to it.")]
        public KeyboardHandler WindowKeyboardHandler { get; }
        /// <summary>A window-scoped "preview" <see cref="KeyboardHandler"/>, pumped every Update tick
        /// BEFORE this window's content children are updated - the window-scoped equivalent of <see cref="MGDesktop.HighPriorityKeyboardHandler"/>.<para/>
        /// Because it is pumped before the children, a subscriber sees the tick's keys regardless of whether keyboard focus is on this
        /// window or on any descendant of it (e.g. a focused <see cref="MGTextBox"/>). This is a preview, not a fallback for keys left
        /// unconsumed by descendants - it never bubbles keys back up after the children have had a chance to handle them.<para/>
        /// A subscriber that must not steal text-entry keys should check <see cref="MGDesktop.FocusedKeyboardHandler"/> against
        /// <see cref="ITextEntryHost"/> (e.g. return early while it is an <see cref="ITextEntryHost"/>) before acting on a key.<para/>
        /// Replaces <see cref="WindowKeyboardHandler"/>, which never receives events.</summary>
        public KeyboardHandler PreviewKeyboardHandler { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _AllowsClickThrough = false;
        /// <summary>If true, mouse clicks overtop of this <see cref="MGWindow"/> that weren't handled by any child elements will remain unhandled,<br/>
        /// allowing content underneath this <see cref="MGWindow"/> to handle the mouse event.<para/>
        /// Default value: false<para/>
        /// This property is ignored if <see cref="IsModalWindow"/> is true.</summary>
        public bool AllowsClickThrough
        {
            get => _AllowsClickThrough;
            set
            {
                if (_AllowsClickThrough != value)
                {
                    _AllowsClickThrough = value;
                    NPC(nameof(AllowsClickThrough));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ActivatesOnClick = true;
        /// <summary>If true, pressing the left mouse button inside this <see cref="MGWindow"/> brings it to the front
        /// (<see cref="MGDesktop.BringToFront(MGWindow)"/> / <see cref="MGWindow.BringToFront(MGWindow)"/>) and moves keyboard
        /// focus into it, resolved the same way as an existing focus resumption (<see cref="MGDesktop.ResolveAutoFocusTarget(MGElement, bool)"/>
        /// with <c>preferWindowDefault: false</c>): the last-focused element in this window, then <see cref="DefaultFocusElement"/>,
        /// then the first focusable element. If the click itself already focused an element (e.g. clicking a focusable control),
        /// that focus takes precedence. If resolution finds no valid target, the current focus is left unchanged.<para/>
        /// Has no effect while blocked by an active modal window or modal overlay (<see cref="MGDesktop.IsBlockedByModalOrOverlay(MGElement)"/>).<para/>
        /// Default value: true<para/>
        /// Popup-like <see cref="MGWindow"/> subtypes that own nested windows of their own (<see cref="MGContextMenu"/>, <see cref="MGToolTip"/>,
        /// the <see cref="MGComboBox{TItemType}"/> dropdown, <see cref="MGColorPickerPopup"/>) set this to false so that clicking
        /// inside them does not reorder their own nested windows.</summary>
        public bool ActivatesOnClick
        {
            get => _ActivatesOnClick;
            set
            {
                if (_ActivatesOnClick != value)
                {
                    _ActivatesOnClick = value;
                    NPC(nameof(ActivatesOnClick));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsDraggable = true;
        /// <summary>True if this <see cref="MGWindow"/> can be moved by dragging the title bar.<para/>
        /// Warning: You may need to set <see cref="IsTitleBarVisible"/> to true to utilize this feature.</summary>
        public bool IsDraggable
        {
            get => _IsDraggable;
            set
            {
                if (_IsDraggable != value)
                {
                    _IsDraggable = value;
                    NPC(nameof(IsDraggable));
                }
            }
        }

        #region Collapse
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _CollapseOnTitleBarDoubleClick;
        /// <summary>True if double-clicking the title bar (outside the close button) toggles <see cref="IsCollapsed"/>.<para/>
        /// Default value: false</summary>
        public bool CollapseOnTitleBarDoubleClick
        {
            get => _CollapseOnTitleBarDoubleClick;
            set
            {
                if (_CollapseOnTitleBarDoubleClick != value)
                {
                    _CollapseOnTitleBarDoubleClick = value;
                    NPC(nameof(CollapseOnTitleBarDoubleClick));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsCollapsed;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsCollapsePending;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _ExpandedWindowHeight;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ExpandedIsUserResizable;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Visibility _ExpandedContentVisibility = Visibility.Visible;

        /// <summary>True while the window is shaded down to its title bar: the content is hidden, resizing is disabled and
        /// <see cref="WindowHeight"/> is reduced to the title bar height. Setting it back to false restores the previous height,
        /// content visibility and <see cref="IsUserResizable"/>.<para/>
        /// Requires <see cref="IsTitleBarVisible"/>. When the title bar has not been laid out yet, the collapse is applied on the next update.<para/>
        /// See also: <see cref="CollapseOnTitleBarDoubleClick"/></summary>
        public bool IsCollapsed
        {
            get => _IsCollapsed;
            set
            {
                if (_IsCollapsed == value)
                {
                    return;
                }

                if (value)
                {
                    if (!IsTitleBarVisible)
                    {
                        return;
                    }

                    _ExpandedWindowHeight = WindowHeight;
                    _ExpandedIsUserResizable = IsUserResizable;
                    _ExpandedContentVisibility = Content?.Visibility ?? Visibility.Visible;
                    _IsCollapsed = true;
                    if (Content != null)
                    {
                        Content.Visibility = Visibility.Collapsed;
                    }
                    IsUserResizable = false;
                    _IsCollapsePending = !TryApplyCollapsedHeight();
                }
                else
                {
                    _IsCollapsed = false;
                    _IsCollapsePending = false;
                    if (Content != null)
                    {
                        Content.Visibility = _ExpandedContentVisibility;
                    }
                    IsUserResizable = _ExpandedIsUserResizable;
                    WindowHeight = _ExpandedWindowHeight;
                }

                NPC(nameof(IsCollapsed));
            }
        }

        /// <summary>Shrinks the window to its title bar. Returns false when the title bar has no layout yet (retried on the next update).</summary>
        private bool TryApplyCollapsedHeight()
        {
            if (TitleBarElement == null || TitleBarElement.LayoutBounds.Height <= 0)
            {
                return false;
            }

            int collapsedHeight = TitleBarElement.LayoutBounds.Bottom - LayoutBounds.Top + BorderThickness.Bottom;
            if (collapsedHeight <= 0)
            {
                return false;
            }

            //Bypass the MinHeight clamp of the WindowHeight setter: a collapsed window is legitimately smaller than its MinHeight.
            if (_WindowHeight != collapsedHeight)
            {
                _WindowHeight = collapsedHeight;
                LayoutChanged(this, true);
                UpdateScaleTransforms();
                RecentSizeToContentSettings = null;
                NPC(nameof(WindowHeight));
            }

            return true;
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            base.UpdateSelf(UA);
            if (_IsCollapsePending && _IsCollapsed)
            {
                _IsCollapsePending = !TryApplyCollapsedHeight();
            }
        }

        private void MakeCollapsible()
        {
            MouseHandler.LMBDoubleClickedInside += (sender, e) =>
            {
                if (!CollapseOnTitleBarDoubleClick || !IsTitleBarVisible || TitleBarElement == null)
                {
                    return;
                }

                Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                if (TitleBarElement.LayoutBounds.ContainsInclusive(LayoutSpacePosition)
                    && (CloseButtonElement == null || !CloseButtonElement.LayoutBounds.ContainsInclusive(LayoutSpacePosition)))
                {
                    IsCollapsed = !IsCollapsed;
                    e.SetHandledBy(this, false);
                }
            };
        }
        #endregion Collapse

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGTheme _Theme;
        /// <summary>If null, uses <see cref="MGDesktop.Theme"/> instead.<para/>
        /// Default value: null<para/>
        /// See also:<br/><see cref="MGElement.GetTheme()"/><br/><see cref="MGDesktop.Theme"/></summary>
        public MGTheme Theme
        {
            get => _Theme;
            set
            {
                if (_Theme != value)
                {
                    _Theme = value;
                    MGResources Resources = EnsureResourceScope(UIResourceScope.Window);
                    if (value == null)
                    {
                        Resources.ClearDefaultThemeOverride();
                    }
                    else
                    {
                        Resources.DefaultTheme = value;
                    }
                    NPC(nameof(Theme));
                }
            }
        }

        private bool _IsTopmost;
        /// <summary>If true, this <see cref="MGWindow"/> will always be updated first (so that it has first-chance to receive and handle inputs)
        /// and drawn last (so that it appears overtop of all other <see cref="MGWindow"/>s).<para/>
        /// This property is only respected within the window's scope.<br/>
        /// If this window is topmost, but is also a nested window of a non-topmost window, it will only be on top of it's siblings (the other nested windows of its parent)<para/>
        /// If multiple windows are topmost, their draw/update priority depends on their index in <see cref="MGDesktop.Windows"/> and <see cref="NestedWindows"/><para/>
        /// <see cref="ModalWindow"/>s will still take priority even over topmost windows.</summary>
        public bool IsTopmost
        {
            get => _IsTopmost;
            set
            {
                if (_IsTopmost != value)
                {
                    _IsTopmost = value;
                    NPC(nameof(IsTopmost));
                }
            }
        }

        private MGElement PressedElementAtBeginUpdate { get; set; }
        private MGElement HoveredElementAtBeginUpdate { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _PressedElement;
        /// <summary>The inner-most element of the visual tree that the mouse was hovering at the moment that the left mouse button was pressed.<br/>
        /// Null if the left mouse button is currently released or the mouse wasn't hovering an element when the button was pressed down.<para/>
        /// See also: <see cref="HoveredElement"/>, <see cref="PressedElementChanged"/></summary>
        public MGElement PressedElement
        {
            get => _PressedElement;
            private set
            {
                if (_PressedElement != value)
                {
                    _PressedElement = value;
                    NPC(nameof(PressedElement));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _HoveredElement;
        /// <summary>The inner-most element of the visual tree that the mouse is currently hovering, if any.<para/>
        /// If the mouse is hovering several sibling elements (such as children of an <see cref="MGOverlayPanel"/>, or elements placed inside the same cell of an <see cref="MGGrid"/>)<br/>
        /// then this property prioritizes the topmost element.<para/>
        /// See also: <see cref="PressedElement"/>, <see cref="HoveredElementChanged"/></summary>
        public MGElement HoveredElement
        {
            get => _HoveredElement;
            private set
            {
                if (_HoveredElement != value)
                {
                    _HoveredElement = value;
                    NPC(nameof(HoveredElement));
                }
            }
        }

        /// <summary>Invoked after <see cref="PressedElement"/> changes, but is intentionally deferred until the end of the current update tick so that <see cref="MGElement.VisualState"/> 
        /// values are properly synced with the <see cref="PressedElement"/></summary>
        public event EventHandler<EventArgs<MGElement>> PressedElementChanged;
        /// <summary>Invoked after <see cref="HoveredElement"/> changes, but is intentionally deferred until the end of the current update tick so that <see cref="MGElement.VisualState"/> 
        /// values are properly synced with the <see cref="HoveredElement"/></summary>
        public event EventHandler<EventArgs<MGElement>> HoveredElementChanged;

        internal bool InvalidatePressedAndHoveredElements { get; set; } = false;

        /// <summary>Set by <see cref="MGDesktop.Update"/> (or a nested window's parent) before each <see cref="Update(ElementUpdateArgs)"/> call.<para/>
        /// True if a higher z-order window is currently visually occluding this window at the current mouse position (mirrors the desktop's
        /// existing tooltip-occlusion check, generalized to <see cref="HoveredElement"/>/<see cref="PressedElement"/> per the 2026-09-02 cross-window
        /// hover occlusion decision - option (b), documented in Docs/input-architecture.md). While true, this window must not light up <see cref="HoveredElement"/>/
        /// <see cref="PressedElement"/> for the occluded position, EXCEPT while this window already owns an active mouse-drag capture, which must
        /// keep following its owner regardless of occlusion.</summary>
        internal bool IsOccludedAtMousePos { get; set; }

        //  Occlusion transitions: HoveredElement is only recomputed on mouse movement / layout changes, so when an occluder (higher desktop
        //  window or nested window) stops covering the mouse position without the mouse moving (e.g. a dropdown closed by clicking an item),
        //  the null forced during the occlusion must be refreshed exactly once.
        private bool _WasOccludedAtMousePos;
        private bool _WasOccludedByNestedWindowAtMousePos;
        private bool _RefreshHoveredElementAfterNestedOcclusion;

        /// <summary>Z-order-aware occlusion test used by <see cref="MGElement"/>'s mouse hit-test (<c>IMouseViewport.IsInside</c>): true if the given
        /// unscaled screen position is covered by a window drawn over this window's content - its own <see cref="ModalWindow"/> or
        /// <see cref="NestedWindows"/>, sibling nested windows drawn above it, higher desktop windows, or the active context menu.
        /// Click-through and hidden windows never occlude. The test is position-dependent (not merely "the mouse is over an occluder")
        /// so that Entered/Exited transitions are detected at the occluder's edge.</summary>
        internal bool IsUnscaledPositionOccluded(Vector2 UnscaledScreenPosition)
        {
            if (ModalWindow != null && ModalWindow.OccludesUnscaledPosition(UnscaledScreenPosition))
            {
                return true;
            }

            for (int i = 0; i < _NestedWindows.Count; i++)
            {
                if (_NestedWindows[i].OccludesUnscaledPosition(UnscaledScreenPosition))
                {
                    return true;
                }
            }

            return IsUnscaledPositionOccludedFromAbove(UnscaledScreenPosition);
        }

        /// <summary>True if this window itself covers the position: visible, not click-through, and its bounds contain the position.</summary>
        internal bool OccludesUnscaledPosition(Vector2 UnscaledScreenPosition)
            => Visibility == Visibility.Visible && !AllowsClickThrough && ActualLayoutBounds.ContainsInclusive(UnscaledScreenPosition);

        /// <summary>Occlusion by windows outside this window's own popup stack: for a nested window, the parent's modal window and the sibling
        /// nested windows drawn above it, then recursively the parent's own occluders; for a root window, the desktop windows drawn above it.
        /// Auxiliary windows that are not part of their parent's <see cref="NestedWindows"/> (modal window, tooltip, context menu) are drawn
        /// above that stack, so only the parent's external occluders apply to them.</summary>
        private bool IsUnscaledPositionOccludedFromAbove(Vector2 UnscaledScreenPosition)
            => IsUnscaledPositionOccludedFromAbove(UnscaledScreenPosition, this);

        /// <param name="Origin">The window whose content is being hit-tested: this window, or an auxiliary descendant (context menu, submenu,
        /// tooltip) that delegated to its parent chain. Carried up the recursion so that the desktop never treats the active context menu as an
        /// occluder of its own content.</param>
        private bool IsUnscaledPositionOccludedFromAbove(Vector2 UnscaledScreenPosition, MGWindow Origin)
        {
            if (ParentWindow == null)
            {
                return Desktop.IsUnscaledPositionOccludedAbove(this, Origin, UnscaledScreenPosition);
            }

            IReadOnlyList<MGWindow> Siblings = ParentWindow.NestedWindows;
            int MyIndex = -1;
            for (int i = 0; i < Siblings.Count; i++)
            {
                if (Siblings[i] == this)
                {
                    MyIndex = i;
                    break;
                }
            }

            if (MyIndex >= 0)
            {
                if (ParentWindow.ModalWindow != null && ParentWindow.ModalWindow.OccludesUnscaledPosition(UnscaledScreenPosition))
                {
                    return true;
                }

                for (int i = 0; i < Siblings.Count; i++)
                {
                    MGWindow Sibling = Siblings[i];
                    if (Sibling != this && IsDrawnAbove(Sibling, i, this, MyIndex) && Sibling.OccludesUnscaledPosition(UnscaledScreenPosition))
                    {
                        return true;
                    }
                }
            }

            return ParentWindow.IsUnscaledPositionOccludedFromAbove(UnscaledScreenPosition, Origin);
        }

        /// <summary>Draw order within one window list (desktop windows or one parent's nested windows): topmost windows are drawn over
        /// non-topmost ones; within the same group, later list entries are drawn later, i.e. above.</summary>
        internal static bool IsDrawnAbove(MGWindow Candidate, int CandidateIndex, MGWindow Reference, int ReferenceIndex)
            => Candidate.IsTopmost != Reference.IsTopmost ? Candidate.IsTopmost : CandidateIndex > ReferenceIndex;

        /// <summary>If true, this <see cref="MGWindow"/>'s layout will be recomputed at the start of the next update tick.</summary>
        public bool QueueLayoutRefresh { get; set; }

        internal MGElement GetActiveMouseDragCaptureOwner()
        {
            if (MouseHandler?.Tracker.CurrentState.LeftButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                return null;
            }

            MGElement current = PressedElement;
            while (current != null)
            {
                if (current is IActiveMouseDragCapture capture && capture.IsActiveMouseDragCapture)
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        private bool HasActiveMouseDragCapture() => GetActiveMouseDragCaptureOwner() != null;

        #region Data Context
        private object _WindowDataContext;
        /// <summary>The default <see cref="MGElement.DataContext"/> for all elements that do not explicitly define a <see cref="MGElement.DataContextOverride"/>.<para/>
        /// If not specified, this value is automatically inherited from the <see cref="MGElement.ParentWindow"/> if there is one.</summary>
        public object WindowDataContext
        {
            get => _WindowDataContext ?? ParentWindow?.WindowDataContext;
            set
            {
                if (_WindowDataContext != value)
                {
                    _WindowDataContext = value;
                    NPC(nameof(WindowDataContext));
                    NPC(nameof(DataContextOverride));
                    NPC(nameof(DataContext));
                    WindowDataContextChanged?.Invoke(this, WindowDataContext);
                    InvokeDataContextChanged();
                    //  Changing the DataContext may have changed the sizes of elements on this window, so attempt to resize the window to its contents
                    //RevalidateSizeToContent(false);
                }
            }
        }

        public override object DataContextOverride
        { 
            get => WindowDataContext;
            set => WindowDataContext = value;
        }

        public event EventHandler<object> WindowDataContextChanged;
        /// <summary>Named handler for <see cref="MGWindow.WindowDataContextChanged"/> on <see cref="MGElement.ParentWindow"/>.
        /// Stored as a field so it can be unsubscribed in <see cref="CleanUpParentDataContextListener"/>.</summary>
        private EventHandler<object> _onParentWindowDataContextChanged;
        private void CleanUpParentDataContextListener()
        {
            if (ParentWindow != null && _onParentWindowDataContextChanged != null)
            {
                ParentWindow.WindowDataContextChanged -= _onParentWindowDataContextChanged;
                Debug.WriteLine($"[Dispose] {nameof(MGWindow)} '{TitleText}' unsubscribed 1 event handler from ParentWindow");
                _onParentWindowDataContextChanged = null;
            }
        }

        private void RevalidateSizeToContent(bool UpdateImmediately)
        {
            if (RecentSizeToContentSettings.HasValue)
            {
                ApplySizeToContent(RecentSizeToContentSettings.Value.Type, RecentSizeToContentSettings.Value.MinWidth, RecentSizeToContentSettings.Value.MinHeight,
                    RecentSizeToContentSettings.Value.MaxWidth, RecentSizeToContentSettings.Value.MaxHeight, UpdateImmediately);
            }
        }
        #endregion Data Context

        #region Constructors
        /// <summary>Initializes a root-level window.</summary>
        public MGWindow(MGDesktop Desktop, int Left, int Top, int Width, int Height, MGTheme Theme = null)
            : this(Desktop, Theme, null, MGElementType.Window, Left, Top, Width, Height)
        {

        }

        /// <summary>Initializes a nested window (such as a popup). You should still call <see cref="AddNestedWindow(MGWindow)"/> (or set <see cref="ModalWindow"/>) afterwards.</summary>
        public MGWindow(MGWindow Window, int Left, int Top, int Width, int Height, MGTheme Theme = null)
            : this(Window.Desktop, Theme, Window, MGElementType.Window, Left, Top, Width, Height)
        {
            if (Window == null)
            {
                throw new ArgumentNullException(nameof(Window));
            }
        }

        /// <exception cref="InvalidOperationException">Thrown if you attempt to change <see cref="MGElement.HorizontalAlignment"/> or <see cref="MGElement.VerticalAlignment"/> on this <see cref="MGWindow"/></exception>
        protected MGWindow(MGDesktop Desktop, MGTheme WindowTheme, MGWindow ParentWindow, MGElementType ElementType, int Left, int Top, int Width, int Height)
            : base(Desktop, WindowTheme, ParentWindow, ElementType)
        {
            if (ParentWindow == null && !WindowElementTypes.Contains(ElementType))
            {
                throw new InvalidOperationException($"All {nameof(MGElement)}s must either belong to an {nameof(MGWindow)} or be a root-level {nameof(MGWindow)} instance.");
            }

            using (BeginInitializing())
            {
                this.Desktop = Desktop ?? throw new ArgumentNullException(nameof(Desktop));
                _ = EnsureResourceScope(UIResourceScope.Window);
                Theme = WindowTheme;

                MGTheme ActualTheme = GetTheme();

                WindowMouseHandler = InputTracker.Mouse.CreateHandler(this, null);
#pragma warning disable CS0618 // WindowKeyboardHandler is [Obsolete]; MGWindow itself must still create/pump it unchanged for source compatibility with existing subscribers.
                WindowKeyboardHandler = InputTracker.Keyboard.CreateHandler(this, null);
#pragma warning restore CS0618
                //  Owned by a dedicated host (not `this`) that keeps IKeyboardHandlerHost.HasKeyboardFocus's default (always true) -
                //  see WindowPreviewKeyboardHandlerHost - so this handler sees every key of the tick regardless of desktop focus.
                PreviewKeyboardHandler = InputTracker.Keyboard.CreateHandler(new WindowPreviewKeyboardHandlerHost(), null);
                MouseHandler.DragStartCondition = DragStartCondition.Both;

                RadioButtonGroups = new();
                _ModalWindows = new();
                _NestedWindows = new();
                _NamedToolTips = new();

                this.Left = Left;
                PreviousLeft = Left;
                this.Top = Top;
                PreviousTop = Top;

                WindowWidth = Width;
                PreviousWidth = Width;
                WindowHeight = Height;
                PreviousHeight = Height;

                MinWidth = 50;
                SetMinHeight(50, UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                MaxWidth = 4000;
                MaxHeight = 2000;

                _IsUserResizable = true;
                _IsTitleBarVisible = true;
                _IsCloseButtonVisible = true;
                DefaultControlTemplateName = MGControlTemplateCatalog.WindowTemplateName;
                TitleText = null;
                IsTitleBarVisible = true;
                IsCloseButtonVisible = true;
                IsUserResizable = true;

                HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalAlignment = VerticalAlignment.Stretch;

                OnHorizontalAlignmentChanged += (sender, e) =>
                {
                    if (ElementType == MGElementType.Window && e.NewValue != HorizontalAlignment.Stretch)
                    {
                        string Error = $"The {nameof(HorizontalAlignment)} of a root-level window element must always be set to: " +
                            $"{nameof(HorizontalAlignment)}.{nameof(HorizontalAlignment.Stretch)}";
                        throw new InvalidOperationException(Error);
                    }
                };
                OnVerticalAlignmentChanged += (sender, e) =>
                {
                    if (ElementType == MGElementType.Window && e.NewValue != VerticalAlignment.Stretch)
                    {
                        string Error = $"The {nameof(VerticalAlignment)} of a root-level window element must always be set to: " +
                            $"{nameof(VerticalAlignment)}.{nameof(VerticalAlignment.Stretch)}";
                        throw new InvalidOperationException(Error);
                    }
                };

                ElementsByName = new();
                OnDirectOrNestedContentAdded += Element_Added;
                OnDirectOrNestedContentRemoved += Element_Removed;

                OnBeginUpdate += (sender, e) =>
                {
                    PressedElementAtBeginUpdate = PressedElement;
                    HoveredElementAtBeginUpdate = HoveredElement;

                    bool hasActiveDragCapture = HasActiveMouseDragCapture();
                    bool suppressHoveredElementUpdate = !MouseHandler.Tracker.MouseLeftButtonReleasedRecently && hasActiveDragCapture;
                    bool occlusionEnded = (_WasOccludedAtMousePos && !IsOccludedAtMousePos) || _RefreshHoveredElementAfterNestedOcclusion;
                    _WasOccludedAtMousePos = IsOccludedAtMousePos;
                    _RefreshHoveredElementAfterNestedOcclusion = false;
                    bool shouldUpdateHoveredElement = !suppressHoveredElementUpdate
                        && (MouseHandler.Tracker.MouseMovedRecently || !IsLayoutValid || QueueLayoutRefresh || InvalidatePressedAndHoveredElements || occlusionEnded);

                    using (UIPerformanceProbe.BeginDesktopPhase("Window.ValidateAndLayout"))
                    {
                        ValidateWindowSizeAndPosition();

                        if (!IsLayoutValid || QueueLayoutRefresh)
                        {
                            QueueLayoutRefresh = false;
                            if (RecentSizeToContentSettings.HasValue)
                            {
                                RevalidateSizeToContent(true);
                            }
                            else
                            {
                                UpdateLayout(new(this.Left, this.Top, WindowWidth, WindowHeight));
                            }
                        }
                    }

                    if (shouldUpdateHoveredElement)
                    {
                        using (UIPerformanceProbe.BeginDesktopPhase("Window.HoveredElement"))
                        {
                            HoveredElement = GetTopmostHoveredElement(e.UA);
                        }
                    }

                    //  Cross-window hover occlusion (option b, decision utilisateur 2026-09-02, documented in Docs/input-architecture.md "Fenetres superposees"):
                    //  a window that is visually covered by a higher window at the current mouse position must not light up HoveredElement,
                    //  unless it already owns an active mouse-drag capture, which must keep following its owner regardless of occlusion.
                    if (IsOccludedAtMousePos && !hasActiveDragCapture)
                    {
                        HoveredElement = null;
                    }

                    using (UIPerformanceProbe.BeginDesktopPhase("Window.PressedElement"))
                    {
                        if (MouseHandler.Tracker.MouseLeftButtonPressedRecently)
                        {
                            //  Same occlusion rule as HoveredElement above: a brand-new press over an occluded window must not light up PressedElement.
                            //  An already-in-progress drag is untouched here since this branch only runs on the exact press tick.
                            PressedElement = IsOccludedAtMousePos ? null : GetTopmostHoveredElement(e.UA);
                        }
                        else if (MouseHandler.Tracker.MouseLeftButtonReleasedRecently)
                        {
                            PressedElement = null;
                        }
                    }
                };

                OnEndUpdate += (sender, e) =>
                {
                    //  These 2 events are intentionally deferred because they affect MGElement.VisualState,
                    //  and subscribing code probably wants to access the most up-to-date MGElement.VisualState values.
                    if (PressedElementAtBeginUpdate != PressedElement)
                    {
                        PressedElementChanged?.Invoke(this, new(PressedElementAtBeginUpdate, PressedElement));
                    }

                    if (HoveredElementAtBeginUpdate != HoveredElement)
                    {
                        HoveredElementChanged?.Invoke(this, new(HoveredElementAtBeginUpdate, HoveredElement));
                    }
                };

                //  Ensure all mouse events that haven't already been handled by a child element of this window are handled, so that the mouse events won't fall-through to underneath this window
                MouseHandler.PressedInside += (sender, e) =>
                {
                    if (!AllowsClickThrough || IsModalWindow)
                    {
                        e.SetHandledBy(this, false);
                    }
                };
                MouseHandler.ReleasedInside += (sender, e) =>
                {
                    if (!AllowsClickThrough || IsModalWindow)
                    {
                        e.SetHandledBy(this, false);
                    }
                };
                MouseHandler.DragStart += (sender, e) =>
                {
                    if (!AllowsClickThrough || IsModalWindow)
                    {
                        e.SetHandledBy(this, false);
                    }
                };
                MouseHandler.Scrolled += (sender, e) =>
                {
                    if (!AllowsClickThrough || IsModalWindow)
                    {
                        e.SetHandledBy(this, false);
                    }
                };
                MouseHandler.PressedOutside += (sender, e) =>
                {
                    if (IsModalWindow)
                    {
                        e.SetHandledBy(this, false);
                    }
                };
                MouseHandler.ReleasedOutside += (sender, e) =>
                {
                    if (IsModalWindow)
                    {
                        e.SetHandledBy(this, false);
                    }
                };
                MouseHandler.DragStartOutside += (sender, e) =>
                {
                    if (IsModalWindow)
                    {
                        e.SetHandledBy(this, false);
                    }
                };

                //  Window activation on click (decision utilisateur, Docs/input-window-activation-design.md section 3.a):
                //  bring this window to front and move keyboard focus into it, guarded by the existing modal guard so it
                //  stays inert behind an active modal window / modal overlay. Cross-window occlusion is already handled
                //  upstream: a press over a point occluded by a higher window is consumed by that higher window's own
                //  catch-all handlers above (e.SetHandledBy) before this window's MouseHandler ever sees it, so this
                //  handler naturally never fires for an occluded press - no extra IsOccludedAtMousePos check is needed here.
                MouseHandler.LMBPressedInside += (sender, e) =>
                {
                    if (!ActivatesOnClick || Desktop.IsBlockedByModalOrOverlay(this))
                    {
                        return;
                    }

                    if (ParentWindow != null)
                    {
                        ParentWindow.BringToFront(this);
                    }
                    else
                    {
                        Desktop.BringToFront(this);
                    }

                    //  If the click itself already focused an element (e.g. a focusable control was clicked, queuing its
                    //  own focus via MGElement.IsFocusable's auto-focus-on-click, which already ran since content children
                    //  are updated before this window's own MouseHandler), that focus wins over the resolved target below.
                    if (Desktop.QueuedFocusedKeyboardHandler == null)
                    {
                        MGElement autoFocusTarget = Desktop.ResolveAutoFocusTarget(this, false);
                        autoFocusTarget?.Focus(KeyboardFocusSource.Pointer);
                    }
                };

                OnBeginUpdateContents += (sender, e) =>
                {
                    ElementUpdateArgs UpdateArgs = e.UA.ChangeOffset(Origin);

                    //  Pumped here, before this window's content children are updated below (OnBeginUpdateContents fires
                    //  before UpdateContents, see MGElement.Update), so a subscriber genuinely previews the tick's keys
                    //  regardless of whether keyboard focus is on this window or on any descendant (e.g. a focused MGTextBox).
                    using (UIPerformanceProbe.BeginDesktopPhase("Window.PreviewKeyboardHandler"))
                    {
                        PreviewKeyboardHandler.ManualUpdate();
                    }

                    using (UIPerformanceProbe.BeginDesktopPhase("Window.NestedWindows"))
                    {
                        //  ModalWindow is intentionally updated BEFORE NestedWindows so that it can mark mouse/keyboard
                        //  events as handled first. Since event args are shared objects, once ModalWindow sets IsHandled=true,
                        //  the subsequent NestedWindow updates will see the event as already handled and skip processing it.
                        //  This ensures the ModalWindow effectively blocks all input to NestedWindows.
                        //  For nested ModalWindows (e.g., a NestedWindow that itself has a ModalWindow), the recursive
                        //  call to Nested.Update() will apply the same ordering inside each nested window.
                        ModalWindow?.Update(UpdateArgs);

                        //  Track ToolTip occlusion for nested windows, mirroring the logic in MGDesktop.Update().
                        //  When a nested window is being hovered, windows beneath it should not be able to override the active ToolTip.
                        bool isNestedWindowOccludedAtMousePos = ModalWindow != null && ModalWindow.VisualState.IsPressedOrHovered;
                        foreach (MGWindow Nested in _NestedWindows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost))
                        {
                            MGToolTip previousQueuedToolTip = GetDesktop().QueuedToolTip;
                            Nested.Update(UpdateArgs);
                            //  If a higher-priority nested window is occluding the mouse, prevent this window from overriding the ToolTip
                            if (isNestedWindowOccludedAtMousePos)
                            {
                                GetDesktop().QueuedToolTip = previousQueuedToolTip;
                            }
                            else if (Nested.VisualState.IsPressedOrHovered && !Nested.AllowsClickThrough)
                            {
                                isNestedWindowOccludedAtMousePos = true;
                            }
                        }

                        //  Nested-window occlusion of this window's own content (same rule as the desktop's cross-window occlusion, option b,
                        //  documented in Docs/input-architecture.md "Fenetres superposees"): when the modal window or a non-click-through nested window (e.g. a ComboBox dropdown)
                        //  is hovered at the mouse position, the content beneath it must not light up HoveredElement/PressedElement - unless this
                        //  window already owns an active mouse-drag capture, which keeps following its owner. This runs before UpdateContents,
                        //  so the children see the suppressed state on this same tick.
                        if (isNestedWindowOccludedAtMousePos && !HasActiveMouseDragCapture())
                        {
                            HoveredElement = null;
                            if (MouseHandler.Tracker.MouseLeftButtonPressedRecently)
                            {
                                PressedElement = null;
                            }
                        }

                        //  Once the nested occluder is gone, HoveredElement must be recomputed on the next tick even if the mouse did not move.
                        if (_WasOccludedByNestedWindowAtMousePos && !isNestedWindowOccludedAtMousePos)
                        {
                            _RefreshHoveredElementAfterNestedOcclusion = true;
                        }
                        _WasOccludedByNestedWindowAtMousePos = isNestedWindowOccludedAtMousePos;
                    }
                };

                OnEndUpdateContents += (sender, e) =>
                {
                    using (UIPerformanceProbe.BeginDesktopPhase("Window.WindowHandlers"))
                    {
                        WindowMouseHandler.ManualUpdate();
#pragma warning disable CS0618 // WindowKeyboardHandler is [Obsolete]; MGWindow itself must still pump it unchanged (it delivers nothing) for source compatibility with existing subscribers.
                        WindowKeyboardHandler.ManualUpdate();
#pragma warning restore CS0618
                    }
                };

                //  Nested windows inherit their WindowDataContext from the parent if they don't have their own explicit value
                if (ParentWindow != null)
                {
                    _onParentWindowDataContextChanged = (sender, e) =>
                    {
                        if (_WindowDataContext == null)
                        {
                            NPC(nameof(WindowDataContext));
                            NPC(nameof(DataContextOverride));
                            NPC(nameof(DataContext));
                            WindowDataContextChanged?.Invoke(this, WindowDataContext);
                            RevalidateSizeToContent(false);
                        }
                    };
                    ParentWindow.WindowDataContextChanged += _onParentWindowDataContextChanged;
                    //  Unsubscribe when this window closes to prevent the parent holding a reference to this window
                    WindowClosed += (_, __) => CleanUpParentDataContextListener();
                }

                MakeDraggable();
                MakeCollapsible();
            }
        }
        #endregion Constructors

        #region Drag Window Position
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool IsDraggingWindowPosition = false;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Point? DragWindowPositionOffset = null;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool IsDrawingDraggedWindowPreview = false;

        /// <summary>Intended to be called once during initialization. Subscribes to the appropriate events to allow the user to move this <see cref="MGWindow"/> by clicking and dragging the window's Title bar.</summary>
        private void MakeDraggable()
        {
            MouseHandler.DragStart += (sender, e) =>
            {
                if (e.IsLMB && IsDraggable && e.Condition == DragStartCondition.MouseMovedAfterPress && IsTitleBarVisible)
                {
                    Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                    if (TitleBarElement.LayoutBounds.ContainsInclusive(LayoutSpacePosition) && !CloseButtonElement.LayoutBounds.ContainsInclusive(LayoutSpacePosition))
                    {
                        IsDraggingWindowPosition = true;
                        DragWindowPositionOffset = Point.Zero;
                        e.SetHandledBy(this, false);
                        //Debug.WriteLine($"{nameof(MGWindow)}: Drag Start at: {e.Position}");
                    }
                }
            };

            MouseHandler.Dragged += (sender, e) =>
            {
                if (e.IsLMB && IsDraggingWindowPosition)
                {
                    float Scalar = 1.0f / Scale;
                    Point Delta = new((int)(e.PositionDelta.X * Scalar), (int)(e.PositionDelta.Y * Scalar));
                    DragWindowPositionOffset = Delta;
                }
            };

            MouseHandler.DragEnd += (sender, e) =>
            {
                try
                {
                    if (e.IsLMB && IsDraggingWindowPosition && DragWindowPositionOffset.HasValue)
                    {
                        Point Delta = new((int)(DragWindowPositionOffset.Value.X * Scale), (int)(DragWindowPositionOffset.Value.Y * Scale));

                        Left += Delta.X;
                        Top += Delta.Y;

                        foreach (MGWindow Nested in _NestedWindows)
                        {
                            Nested.Left += Delta.X;
                            Nested.Top += Delta.Y;
                        }

                        foreach (MGWindow modalWindow in _ModalWindows)
                        {
                            modalWindow.Left += Delta.X;
                            modalWindow.Top += Delta.Y;
                        }

                        MouseHandler.Tracker.CurrentButtonReleasedEvents[MouseButton.Left]?.SetHandledBy(this, false);

                        //  Reposition the window so that the title bar is fully on screen
                        {
                            //  Immediately update the layout to refresh child positions
                            ValidateWindowSizeAndPosition();
                            UpdateLayout(new Rectangle(Left, Top, WindowWidth, WindowHeight));

                            Rectangle TitleBarLayoutBounds = TitleBarElement.LayoutBounds;
                            Rectangle TitleBarScreenBounds = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, TitleBarLayoutBounds);

                            Rectangle ValidScreenBounds = Desktop.ValidScreenBounds;
                            Rectangle ValidLayoutBounds = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, ValidScreenBounds);

                            //  Shift the window up so the bottom of the title bar is above the bottom of the desktop bounds
                            if (TitleBarScreenBounds.Bottom > ValidScreenBounds.Bottom)
                            {
                                Top -= Math.Abs(TitleBarScreenBounds.Bottom - ValidScreenBounds.Bottom);
                            }
                            //  Shift the window down so the top of the title bar is below the top of the desktop bounds
                            else if (TitleBarScreenBounds.Top < ValidScreenBounds.Top)
                            {
                                Top += Math.Abs(ValidScreenBounds.Top - TitleBarScreenBounds.Top);
                            }

                            //  Shift the window left so the right of the title bar is left of the rightmost desktop bounds
                            if (TitleBarScreenBounds.Right > ValidScreenBounds.Right)
                            {
                                Left -= Math.Abs(TitleBarScreenBounds.Right - ValidScreenBounds.Right);
                            }
                            //  Shift the window right so the left of the title bar is right of the leftmost desktop bounds
                            else if (TitleBarScreenBounds.Left < ValidScreenBounds.Left)
                            {
                                Left += Math.Abs(ValidScreenBounds.Left - TitleBarScreenBounds.Left);
                            }
                        }

                        //Debug.WriteLine($"{nameof(MGWindow)}: Drag End at: {e.EndPosition}");
                    }
                }
                finally
                {
                    IsDraggingWindowPosition = false;
                    DragWindowPositionOffset = null;
                }
            };

            //  Pre-emptively handle mouse release events if user was dragging this window
            //  so that the child content doesn't also react to the mouse release
            //  (such as if you end the mouse drag by releasing the mouse overtop of a button)
            OnBeginUpdateContents += (sender, e) =>
            {
                if (IsDraggingWindowPosition)
                {
                    MouseHandler.Tracker.CurrentButtonReleasedEvents[MouseButton.Left]?.SetHandledBy(this, false);
                }
            };
        }
#endregion Drag Window Position

#region Indexed Elements
        private Dictionary<string, MGElement> ElementsByName { get; }

        public MGElement GetElementByName(string Name) => ElementsByName[Name];
        public T GetElementByName<T>(string Name) where T : MGElement => ElementsByName[Name] as T;

        public new bool TryGetElementByName(string Name, out MGElement Element) => ElementsByName.TryGetValue(Name, out Element);
        public bool TryGetElementByName<T>(string Name, out T Element) where T : MGElement
        {
            if (TryGetElementByName(Name, out MGElement Result))
            {
                Element = Result as T;
                return Element != null;
            }
            else
            {
                Element = default;
                return false;
            }
        }

        private void Element_Added(object sender, MGElement e)
        {
            if (e.Name != null)
            {
                ElementsByName.Add(e.Name, e);
            }

            if (e.ToolTip != null)
            {
                MGToolTip TT = e.ToolTip;
                foreach (MGElement Element in TT.TraverseVisualTree(true, false, false, false, TreeTraversalMode.Preorder))
                {
                    Element_Added(TT, Element);
                }
            }

            if (e.ContextMenu != null)
            {
                MGContextMenu CM = e.ContextMenu;
                foreach (MGElement Element in CM.TraverseVisualTree(true, false, false, false, TreeTraversalMode.Preorder))
                {
                    Element_Added(CM, Element);
                }
            }

            e.ToolTipChanged += Element_ToolTipChanged;
            e.ContextMenuChanged += Element_ContextMenuChanged;
            e.OnNameChanged += Element_NameChanged;
        }

        private void Element_Removed(object sender, MGElement e)
        {
            if (e.Name != null)
            {
                ElementsByName.Remove(e.Name);
            }

            if (e.ToolTip != null)
            {
                MGToolTip TT = e.ToolTip;
                foreach (MGElement Element in TT.TraverseVisualTree(true, false, false, false, TreeTraversalMode.Preorder))
                {
                    Element_Removed(TT, Element);
                }
            }

            if (e.ContextMenu != null)
            {
                MGContextMenu CM = e.ContextMenu;
                foreach (MGElement Element in CM.TraverseVisualTree(true, false, false, false, TreeTraversalMode.Preorder))
                {
                    Element_Removed(CM, Element);
                }
            }

            e.ToolTipChanged -= Element_ToolTipChanged;
            e.ContextMenuChanged -= Element_ContextMenuChanged;
            e.OnNameChanged -= Element_NameChanged;
        }

        private void Element_NameChanged(object sender, EventArgs<string> e)
        {
            if (e.PreviousValue != null)
            {
                ElementsByName.Remove(e.PreviousValue);
            }

            if (e.NewValue != null)
            {
                ElementsByName.Add(e.NewValue, sender as MGElement);
            }
        }

        private void Element_ToolTipChanged(object sender, EventArgs<MGToolTip> e) => Element_NestedElementChanged(e.PreviousValue, e.NewValue);
        private void Element_ContextMenuChanged(object sender, EventArgs<MGContextMenu> e) => Element_NestedElementChanged(e.PreviousValue, e.NewValue);

        private void Element_NestedElementChanged(MGSingleContentHost Previous, MGSingleContentHost New)
        {
            if (Previous != null)
            {
                Previous.OnDirectOrNestedContentAdded -= Element_Added;
                Previous.OnDirectOrNestedContentRemoved -= Element_Removed;

                foreach (MGElement Element in Previous.TraverseVisualTree(true, false, false, false, TreeTraversalMode.Preorder))
                {
                    Element_Removed(Previous, Element);
                }
            }

            if (New != null)
            {
                New.OnDirectOrNestedContentAdded += Element_Added;
                New.OnDirectOrNestedContentRemoved += Element_Removed;

                foreach (MGElement Element in New.TraverseVisualTree(true, false, false, false, TreeTraversalMode.Preorder))
                {
                    Element_Added(New, Element);
                }
            }
        }
#endregion Indexed Elements

        private VisualStateFillBrush PreviousBackgroundBrush = null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private WindowStyle _WindowStyle = WindowStyle.Default;
        public WindowStyle WindowStyle
        {
            get => _WindowStyle;
            set
            {
                if (WindowStyle != value)
                {
                    _WindowStyle = value;

                    switch (_WindowStyle)
                    {
                        case WindowStyle.Default:
                            IsTitleBarVisible = true;
                            IsCloseButtonVisible = true;
                            IsUserResizable = true;
                            SetPadding(GetTheme().Window.Padding, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                            SetBorderThicknessTagged(GetTheme().Window.BorderThickness, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                            BackgroundBrush = PreviousBackgroundBrush ?? BackgroundBrush;
                            break;
                        case WindowStyle.None:
                            IsTitleBarVisible = false;
                            IsCloseButtonVisible = false;
                            IsUserResizable = false;
                            SetPadding(GetTheme().Window.ChromelessPadding, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                            SetBorderThicknessTagged(GetTheme().Window.ChromelessBorderThickness, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                            PreviousBackgroundBrush = BackgroundBrush.Copy();
                            BackgroundBrush.SetAll(SolidFillBrushes.Transparent);
                            //  WindowStyle.None sets AllowsClickThrough=false by default so that
                            //  chrome-less windows still block mouse events.
                            //  XAML can legitimately override this afterwards via the AllowsClickThrough property
                            //  (e.g. HUD overlays that need click-through in empty areas).
                            AllowsClickThrough = false;
                            break;
                        default: throw new NotImplementedException($"Unrecognized {nameof(WindowStyle)}: {value}");
                    }

                    NPC(nameof(WindowStyle));
                }
            }
        }

        public void Draw(DrawBaseArgs BA) => Draw(new ElementDrawArgs(BA, VisualState, Point.Zero));

        public event EventHandler<ElementDrawArgs> OnBeginDrawNestedWindows;
        public event EventHandler<ElementDrawArgs> OnEndDrawNestedWindows;

        private IEnumerable<MGWindow> ParentWindows
        {
            get
            {
                MGWindow Current = ParentWindow;
                while (Current != null)
                {
                    yield return Current;
                    Current = Current.ParentWindow;
                }
            }
        }

        public override void Draw(ElementDrawArgs DA)
        {
            if (!IsWindowScaled && !ParentWindows.Any(x => x.IsWindowScaled))
            {
                base.Draw(DA);
            }
            else
            {
#if true
                using (DA.DT.SetTransformTemporary(UnscaledScreenSpaceToScaledScreenSpace))
                {
                    base.Draw(DA);
                }
#else
                using (DA.DT.SetRenderTargetTemporary(RenderTarget, Color.Transparent))
                {
                    ElementDrawArgs Translated = DA with { Offset = DA.Offset - TopLeft };
                    base.Draw(Translated);
                }

                Rectangle LayoutSpaceBounds = new(Left, Top, WindowWidth, WindowHeight);
                Rectangle Destination = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, LayoutSpaceBounds);
                DA.DT.DrawTextureTo(RenderTarget, null, Destination);
#endif
            }

            OnBeginDrawNestedWindows?.Invoke(this, DA);

            //  Draw a transparent black overlay if there is a Modal window overtop of this window
            if (HasModalWindow)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), LayoutBounds, Color.Black * 0.5f);
            }

            foreach (MGWindow Nested in _NestedWindows.OrderBy(x => x.IsTopmost))
            {
                Nested.Draw(DA);
            }

            ModalWindow?.Draw(DA);

            if (!IsDrawingDraggedWindowPreview && IsDraggingWindowPosition && DragWindowPositionOffset.HasValue && DragWindowPositionOffset.Value != Point.Zero)
            {
                try
                {
                    IsDrawingDraggedWindowPreview = true;
                    float TempOpacity = DA.Opacity * 0.25f;
                    Point TempOffset = DA.Offset + DragWindowPositionOffset.Value;
                    Draw(DA.SetOpacity(TempOpacity) with { Offset = TempOffset });
                }
                finally { IsDrawingDraggedWindowPreview = false; }
            }

            OnEndDrawNestedWindows?.Invoke(this, DA);
        }

        public override void DrawBackground(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            base.DrawBackground(DA, LayoutBounds);

            if (!IsTitleBarVisible || TitleBarElement == null || TitleBarElement.Visibility != Visibility.Visible)
            {
                return;
            }

            MGBoxShape windowShape = new(LayoutBounds, BorderThickness, CornerRadius);
            MGBoxShape normalizedWindowShape = windowShape.Normalize();
            Rectangle hostBounds = normalizedWindowShape.InnerBounds;
            Rectangle titleBarBounds = Rectangle.Intersect(hostBounds, TitleBarElement.LayoutBounds);
            if (titleBarBounds.Width <= 0 || titleBarBounds.Height <= 0)
            {
                return;
            }

            MGBoxShape titleBarShape = MGBoxShapeRegionHelper.CreateSubShape(hostBounds, normalizedWindowShape.InnerCornerRadius, titleBarBounds);
            MGBoxGeometry titleBarGeometry = MGBoxGeometryBuilder.Build(titleBarShape);
            TitleBarElement.BackgroundBrush.GetUnderlay(TitleBarElement.VisualState.Primary)?.Draw(DA, TitleBarElement, titleBarShape, titleBarGeometry);
            TitleBarElement.BackgroundBrush.GetFillOverlay(TitleBarElement.VisualState.Secondary)?.Draw(DA, TitleBarElement, titleBarShape, titleBarGeometry);
        }
    }
}
