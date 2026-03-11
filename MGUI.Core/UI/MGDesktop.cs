using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input;
using MGUI.Shared.Input.GamePad;
using MGUI.Shared.Text;
using MGUI.Shared.Rendering;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.DragDrop;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MGUI.Shared.Text.Engines;

namespace MGUI.Core.UI
{
    /// <summary>Represents a Rectanglular screen bounds that you can add or remove <see cref="MGWindow"/>s to/from, 
    /// and handles mutual exclusion with things like input handling or ensuring there is only 1 <see cref="MGToolTip"/> or <see cref="MGContextMenu"/> on the user interface at a time.</summary>
    public class MGDesktop : ViewModelBase, IMouseHandlerHost, IKeyboardHandlerHost, IContextMenuHost
    {
        public MainRenderer Renderer { get; }
        public UIView View { get; private set; }
        public InputTracker InputTracker => Renderer.Input;
        public FontManager FontManager => Renderer.FontManager;

        internal void AttachView(UIView View)
        {
            this.View = View ?? throw new ArgumentNullException(nameof(View));
        }

        internal static bool HasKeyboardActivity(KeyboardTracker keyboard)
            => keyboard.CurrentKeyPressedEvents.Values.Any(x => x != null)
            || keyboard.CurrentKeyReleasedEvents.Values.Any(x => x != null)
            || keyboard.CurrentKeyClickedEvents.Values.Any(x => x != null);

        internal const int PointerModeMouseMovementThreshold = 2;

        internal static bool HasMouseMovementActivity(Point previousPosition, Point currentPosition, int threshold = PointerModeMouseMovementThreshold)
            => Math.Abs(currentPosition.X - previousPosition.X) >= threshold
            || Math.Abs(currentPosition.Y - previousPosition.Y) >= threshold;

        internal static bool HasMouseActivity(MouseTracker mouse)
            => HasMouseMovementActivity(mouse.PreviousState.Position, mouse.CurrentState.Position)
            || mouse.MouseLeftButtonPressedRecently
            || mouse.MouseLeftButtonReleasedRecently
            || mouse.CurrentScrollEvent != null;

        internal static UIInputMode ResolveInputMode(bool hasMouseActivity, bool hasKeyboardActivity, bool isTextEntryFocused, UIInputMode currentMode)
        {
            if (hasMouseActivity)
                return UIInputMode.Pointer;

            if (hasKeyboardActivity)
                return isTextEntryFocused ? UIInputMode.TextEntry : UIInputMode.Navigation;

            return currentMode;
        }

        internal static bool TryMapNavigationAction(Keys key, bool isShiftDown, out UINavigationAction action)
        {
            switch (key)
            {
                case Keys.Tab:
                    action = isShiftDown ? UINavigationAction.MovePrevious : UINavigationAction.MoveNext;
                    return true;
                case Keys.Enter:
                case Keys.Space:
                    action = UINavigationAction.Submit;
                    return true;
                case Keys.Escape:
                    action = UINavigationAction.Cancel;
                    return true;
                case Keys.Up:
                    action = UINavigationAction.MoveUp;
                    return true;
                case Keys.Down:
                    action = UINavigationAction.MoveDown;
                    return true;
                case Keys.Left:
                    action = UINavigationAction.MoveLeft;
                    return true;
                case Keys.Right:
                    action = UINavigationAction.MoveRight;
                    return true;
                case Keys.Home:
                    action = UINavigationAction.Home;
                    return true;
                case Keys.End:
                    action = UINavigationAction.End;
                    return true;
                case Keys.PageUp:
                    action = UINavigationAction.PageUp;
                    return true;
                case Keys.PageDown:
                    action = UINavigationAction.PageDown;
                    return true;
                case Keys.Apps:
                    action = UINavigationAction.OpenContext;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }

        internal static bool TryMapGamePadNavigationAction(GamePadButton button, out UINavigationAction action)
        {
            switch (button)
            {
                case GamePadButton.A:
                    action = UINavigationAction.Submit;
                    return true;
                case GamePadButton.B:
                case GamePadButton.Back:
                    action = UINavigationAction.Cancel;
                    return true;
                case GamePadButton.DPadUp:
                case GamePadButton.LeftStickUp:
                    action = UINavigationAction.MoveUp;
                    return true;
                case GamePadButton.DPadDown:
                case GamePadButton.LeftStickDown:
                    action = UINavigationAction.MoveDown;
                    return true;
                case GamePadButton.DPadLeft:
                case GamePadButton.LeftStickLeft:
                    action = UINavigationAction.MoveLeft;
                    return true;
                case GamePadButton.DPadRight:
                case GamePadButton.LeftStickRight:
                    action = UINavigationAction.MoveRight;
                    return true;
                case GamePadButton.LeftShoulder:
                    action = UINavigationAction.ShoulderPrevious;
                    return true;
                case GamePadButton.RightShoulder:
                    action = UINavigationAction.ShoulderNext;
                    return true;
                case GamePadButton.LeftTrigger:
                    action = UINavigationAction.Decrement;
                    return true;
                case GamePadButton.RightTrigger:
                    action = UINavigationAction.Increment;
                    return true;
                case GamePadButton.X:
                    action = UINavigationAction.OpenContext;
                    return true;
                default:
                    action = default;
                    return false;
            }
        }

        internal static bool TryDispatchNavigationAction(UINavigationAction action, Func<UINavigationAction, bool> tryHandleFocusedAction)
            => tryHandleFocusedAction?.Invoke(action) == true;

        internal static void EnsureNavigationTargetVisible(MGElement focusedElement)
        {
            if (focusedElement is INavigationTargetVisibilityHandler navigationTargetVisibilityHandler)
                navigationTargetVisibilityHandler.EnsureNavigationTargetVisible();
        }

        internal static KeyboardFocusSource GetNavigationFocusSource(bool isGamePadNavigation)
            => isGamePadNavigation ? KeyboardFocusSource.GamePad : KeyboardFocusSource.Keyboard;

        internal static bool ShouldAutoScrollFocusedElement(KeyboardFocusSource focusSource)
            => focusSource != KeyboardFocusSource.Pointer;

        internal static T ResolveAutoFocusTarget<T>(T defaultFocus, T lastFocused, T firstFocusable, bool preferWindowDefault)
            where T : class
            => preferWindowDefault
                ? defaultFocus ?? lastFocused ?? firstFocusable
                : lastFocused ?? defaultFocus ?? firstFocusable;

        private static readonly IReadOnlyList<GamePadButton> GamePadNavigationButtons = new[]
        {
            GamePadButton.A,
            GamePadButton.B,
            GamePadButton.X,
            GamePadButton.Back,
            GamePadButton.DPadUp,
            GamePadButton.DPadDown,
            GamePadButton.DPadLeft,
            GamePadButton.DPadRight,
            GamePadButton.LeftStickUp,
            GamePadButton.LeftStickDown,
            GamePadButton.LeftStickLeft,
            GamePadButton.LeftStickRight,
            GamePadButton.LeftShoulder,
            GamePadButton.RightShoulder,
            GamePadButton.LeftTrigger,
            GamePadButton.RightTrigger,
        };

        internal static int GetWrappedFocusIndex(int count, int currentIndex, bool moveNext)
        {
            if (count <= 0)
                return -1;

            if (currentIndex < 0 || currentIndex >= count)
                return moveNext ? 0 : count - 1;

            return moveNext
                ? (currentIndex + 1) % count
                : (currentIndex - 1 + count) % count;
        }

        internal static int FindDirectionalNavigationTarget(Rectangle currentBounds, IReadOnlyList<Rectangle> candidateBounds, NavigationDirection direction)
        {
            int bestIndex = -1;
            double bestDistance = double.MaxValue;

            Vector2 currentCenter = currentBounds.Center.ToVector2();
            for (int i = 0; i < candidateBounds.Count; i++)
            {
                Rectangle candidate = candidateBounds[i];
                Vector2 candidateCenter = candidate.Center.ToVector2();
                Vector2 delta = candidateCenter - currentCenter;

                bool isValidDirection = direction switch
                {
                    NavigationDirection.Up => delta.Y < 0,
                    NavigationDirection.Down => delta.Y > 0,
                    NavigationDirection.Left => delta.X < 0,
                    NavigationDirection.Right => delta.X > 0,
                    _ => false
                };

                if (!isValidDirection)
                    continue;

                double distance = delta.LengthSquared();
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private UIInputMode _ActiveInputMode = UIInputMode.Pointer;
        public UIInputMode ActiveInputMode
        {
            get => _ActiveInputMode;
            private set
            {
                if (_ActiveInputMode != value)
                {
                    _ActiveInputMode = value;
                    NPC(nameof(ActiveInputMode));
                }
            }
        }

        internal bool ShouldDisplayFocusedState => ActiveInputMode != UIInputMode.Pointer;

        private sealed class FocusScopeEntry
        {
            public MGElement ScopeRoot { get; }
            public MGElement RestoreFocusTarget { get; set; }

            public FocusScopeEntry(MGElement scopeRoot, MGElement restoreFocusTarget)
            {
                ScopeRoot = scopeRoot;
                RestoreFocusTarget = restoreFocusTarget;
            }
        }

        private List<FocusScopeEntry> FocusScopes { get; } = new();

        internal static T GetActiveFocusScopeRoot<T>(IReadOnlyList<T> scopeRoots)
            where T : class
            => scopeRoots?.LastOrDefault();

        internal static T ResolveNavigationRoot<T>(T activeScopeRoot, T focusedWindowRoot, T hoveredWindowRoot, T topWindowRoot)
            where T : class
            => activeScopeRoot ?? focusedWindowRoot ?? hoveredWindowRoot ?? topWindowRoot;

        internal static bool IsWithinFocusScope<T>(T scopeRoot, T element, Func<T, T> getParent)
            where T : class
        {
            if (scopeRoot == null || element == null || getParent == null)
                return false;

            T current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, scopeRoot))
                    return true;

                current = getParent(current);
            }

            return false;
        }

        internal void PushFocusScope(MGElement scopeRoot, MGElement restoreFocusTarget = null)
        {
            if (scopeRoot == null)
                return;

            int existingIndex = FocusScopes.FindLastIndex(x => x.ScopeRoot == scopeRoot);
            if (existingIndex >= 0)
            {
                FocusScopeEntry existingEntry = FocusScopes[existingIndex];
                FocusScopes.RemoveAt(existingIndex);
                existingEntry.RestoreFocusTarget = restoreFocusTarget ?? existingEntry.RestoreFocusTarget;
                FocusScopes.Add(existingEntry);
                return;
            }

            FocusScopes.Add(new(scopeRoot, restoreFocusTarget ?? FocusedKeyboardHandler));
        }

        internal void PopFocusScope(MGElement scopeRoot)
        {
            if (scopeRoot == null)
                return;

            int existingIndex = FocusScopes.FindLastIndex(x => x.ScopeRoot == scopeRoot);
            if (existingIndex < 0)
                return;

            bool wasActiveScope = existingIndex == FocusScopes.Count - 1;
            FocusScopeEntry entry = FocusScopes[existingIndex];
            FocusScopes.RemoveAt(existingIndex);

            bool shouldRestoreFocus = wasActiveScope
                && QueuedFocusedKeyboardHandler == null
                && (FocusedKeyboardHandler == null || IsWithinFocusScope(scopeRoot, FocusedKeyboardHandler, current => current.Parent));
            if (shouldRestoreFocus && IsNavigationTarget(entry.RestoreFocusTarget))
                entry.RestoreFocusTarget.Focus();
        }

        private static MGElement GetNearestNavigationTarget(MGElement element)
        {
            for (MGElement current = element; current != null; current = current.Parent)
            {
                if (IsNavigationTarget(current))
                    return current;
            }

            return null;
        }

        private IEnumerable<MGWindow> GetWindowsFrontToBack()
            => Windows.OrderByDescending(x => x.IsTopmost);

        private MGElement GetHoveredNavigationTarget()
        {
            foreach (MGWindow window in GetWindowsFrontToBack())
            {
                MGElement hoveredTarget = GetNearestNavigationTarget(window.HoveredElement);
                if (hoveredTarget != null)
                    return hoveredTarget;
            }

            return null;
        }

        private MGElement GetNavigationRoot()
        {
            MGElement activeScopeRoot = GetActiveFocusScopeRoot(FocusScopes.Select(x => x.ScopeRoot).ToList());
            MGElement focusedWindowRoot = FocusedKeyboardHandler?.SelfOrParentWindow;
            MGElement hoveredWindowRoot = GetHoveredNavigationTarget()?.SelfOrParentWindow;
            MGElement topWindowRoot = GetWindowsFrontToBack().FirstOrDefault();

            return ResolveNavigationRoot(activeScopeRoot, focusedWindowRoot, hoveredWindowRoot, topWindowRoot);
        }

        private static bool IsNavigationTarget(MGElement element)
            => element != null
            && element.IsFocusable
            && element.DerivedIsEnabled
            && element.DerivedIsHitTestVisible
            && element.Visibility == Visibility.Visible;

        private static IReadOnlyList<MGElement> GetFocusableElements(MGElement root)
        {
            if (root == null)
                return Array.Empty<MGElement>();

            return root.TraverseVisualTree(true, false, false, false, MGElement.TreeTraversalMode.Preorder)
                .Where(IsNavigationTarget)
                .Distinct()
                .OrderBy(x => x.TabIndex)
                .ThenBy(x => x.ActualLayoutBounds.Top)
                .ThenBy(x => x.ActualLayoutBounds.Left)
                .ToList();
        }

        public IReadOnlyList<MGElement> GetFocusableElements()
        {
            MGElement root = GetNavigationRoot();
            return GetFocusableElements(root);
        }

        public bool MoveFocusNext()
        {
            return MoveFocusNext(KeyboardFocusSource.Keyboard);
        }

        public bool MoveFocusNext(KeyboardFocusSource source)
        {
            IReadOnlyList<MGElement> focusableElements = GetFocusableElements();
            MGElement anchor = FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
            if (anchor == null)
            {
                MGElement autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
                if (!IsNavigationTarget(autoFocusTarget))
                    return false;

                autoFocusTarget.Focus(source);
                return true;
            }

            int currentIndex = focusableElements.Select((element, index) => new { element, index }).FirstOrDefault(x => x.element == anchor)?.index ?? -1;
            int nextIndex = GetWrappedFocusIndex(focusableElements.Count, currentIndex, true);
            if (nextIndex < 0)
                return false;

            focusableElements[nextIndex].Focus(source);
            return true;
        }

        public bool MoveFocusPrevious()
        {
            return MoveFocusPrevious(KeyboardFocusSource.Keyboard);
        }

        public bool MoveFocusPrevious(KeyboardFocusSource source)
        {
            IReadOnlyList<MGElement> focusableElements = GetFocusableElements();
            MGElement anchor = FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
            if (anchor == null)
            {
                MGElement autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
                if (!IsNavigationTarget(autoFocusTarget))
                    return false;

                autoFocusTarget.Focus(source);
                return true;
            }

            int currentIndex = focusableElements.Select((element, index) => new { element, index }).FirstOrDefault(x => x.element == anchor)?.index ?? -1;
            int nextIndex = GetWrappedFocusIndex(focusableElements.Count, currentIndex, false);
            if (nextIndex < 0)
                return false;

            focusableElements[nextIndex].Focus(source);
            return true;
        }

        public bool NavigateTo(NavigationDirection direction)
        {
            return NavigateTo(direction, KeyboardFocusSource.Keyboard);
        }

        public bool NavigateTo(NavigationDirection direction, KeyboardFocusSource source)
        {
            MGElement focusedElement = FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
            if (focusedElement == null)
            {
                MGElement autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
                if (!IsNavigationTarget(autoFocusTarget))
                    return false;

                autoFocusTarget.Focus(source);
                return true;
            }

            if (focusedElement.NavigationNeighbors.TryGetValue(direction, out MGElement explicitNeighbor) && IsNavigationTarget(explicitNeighbor))
            {
                explicitNeighbor.Focus(source);
                return true;
            }

            IReadOnlyList<MGElement> focusableElements = GetFocusableElements();
            List<MGElement> candidates = focusableElements.Where(x => x != focusedElement).ToList();
            int targetIndex = FindDirectionalNavigationTarget(focusedElement.ActualLayoutBounds, candidates.Select(x => x.ActualLayoutBounds).ToList(), direction);
            if (targetIndex < 0)
            {
                if (FocusedKeyboardHandler == null && IsNavigationTarget(focusedElement))
                {
                    focusedElement.Focus(source);
                    return true;
                }

                return false;
            }

            candidates[targetIndex].Focus(source);
            return true;
        }

        private bool TryPerformFallbackNavigation(UINavigationAction action, KeyboardFocusSource source)
            => action switch
            {
                UINavigationAction.MoveNext => MoveFocusNext(source),
                UINavigationAction.MovePrevious => MoveFocusPrevious(source),
                UINavigationAction.MoveUp => NavigateTo(NavigationDirection.Up, source),
                UINavigationAction.MoveDown => NavigateTo(NavigationDirection.Down, source),
                UINavigationAction.MoveLeft => NavigateTo(NavigationDirection.Left, source),
                UINavigationAction.MoveRight => NavigateTo(NavigationDirection.Right, source),
                _ => false
            };

        private bool TryDispatchNavigationAction(BaseKeyPressedEventArgs e)
        {
            MGElement focusedElement = FocusedKeyboardHandler;
            if (focusedElement is MGTextBox focusedTextBox && focusedTextBox.ShouldPreserveTextEntryKey(e.Key))
                return false;

            if (!TryMapNavigationAction(e.Key, e.Tracker.IsShiftDown, out UINavigationAction action))
                return false;

            Func<UINavigationAction, bool> tryHandleFocusedAction = focusedElement == null ? null : new Func<UINavigationAction, bool>(actionToHandle => focusedElement.TryHandleNavigationAction(actionToHandle));
            if (TryDispatchNavigationAction(action, tryHandleFocusedAction))
            {
                EnsureNavigationTargetVisible(focusedElement);
                e.SetHandledBy(focusedElement, false);
                return true;
            }

            if (TryPerformFallbackNavigation(action, KeyboardFocusSource.Keyboard))
            {
                e.SetHandledBy(this, false);
                return true;
            }

            return false;
        }

        private bool TryDispatchGamePadNavigationActions()
        {
            bool handledAny = false;
            foreach (GamePadButton button in GamePadNavigationButtons)
            {
                if (!InputTracker.GamePad.WasTriggered(button) || !TryMapGamePadNavigationAction(button, out UINavigationAction action))
                    continue;

                MGElement focusedElement = FocusedKeyboardHandler;
                Func<UINavigationAction, bool> tryHandleFocusedAction = focusedElement == null ? null : new Func<UINavigationAction, bool>(actionToHandle => focusedElement.TryHandleNavigationAction(actionToHandle));
                if (TryDispatchNavigationAction(action, tryHandleFocusedAction))
                {
                    EnsureNavigationTargetVisible(focusedElement);
                    handledAny = true;
                }
                else if (TryPerformFallbackNavigation(action, KeyboardFocusSource.GamePad))
                    handledAny = true;
            }

            return handledAny;
        }

        /// <summary>The active <see cref="ITextEngine"/> used for all
        /// text measurement and rendering.  Assign a different engine to switch backends globally.</summary>
        public ITextEngine TextEngine
        {
            get => Renderer.TextEngine;
            set => Renderer.TextEngine = value;
        }

        /// <summary>
        /// Invalidates the layout of every element on this desktop, forcing all elements to be
        /// re-measured on the next frame.
        /// </summary>
        public void InvalidateAllLayouts()
        {
            foreach (MGWindow window in Windows)
                foreach (MGElement element in window.TraverseVisualTree(true, true, true, true, MGElement.TreeTraversalMode.Preorder))
                    element.InvalidateLayout();
        }

        /// <summary>
        /// Re-resolves all <see cref="MGTextBlock"/> font handles from the currently active
        /// <see cref="TextEngine"/> and invalidates their layout and measurement caches.<para/>
        /// Call this after switching <see cref="TextEngine"/> at runtime so that the new engine's
        /// metrics (e.g. different scale factors or glyph data) are reflected immediately on every
        /// text element across all windows.<para/>
        /// After re-resolving all font handles, also calls <see cref="InvalidateAllLayouts"/> to
        /// flush any stale cached measurements on container elements that depend on text sizes,
        /// ensuring the next frame re-measures the full layout tree with the new engine's metrics.
        /// </summary>
        public void RecalculateTextLayouts()
        {
            // Step 1: re-resolve font handles and clear TextBlock self-measurement caches.
            // RefreshTextEngine also calls InvokeLayoutChanged which propagates upward, but
            // that only invalidates the parent chain of each TextBlock, not the full tree.
            foreach (MGWindow window in Windows)
                foreach (MGTextBlock tb in window.TraverseVisualTree<MGTextBlock>(true, true, true, true, MGElement.TreeTraversalMode.Preorder))
                    tb.RefreshTextEngine();

            // Step 2: invalidate every element's layout cache so containers at all levels
            // re-measure their content with the new text-engine metrics on the next frame.
            InvalidateAllLayouts();
        }

        /// <summary>A <see cref="MouseHandler"/> that is updated at the start of <see cref="Update()"/>, before any <see cref="MGWindow"/>s in <see cref="Windows"/> are updated.<br/>
        /// Objects that subscribe to this handler's mouse events will be the very first to receive and handle the event.<para/>
        /// Highly recommended to avoid using this unless absolutely necessary, and if you do use it, you probably shouldn't call e.SetHandledBy(...) so other elements can still receive the input.</summary>
        public MouseHandler HighPriorityMouseHandler { get; }
        /// <summary>A <see cref="KeyboardHandler"/> that is updated at the start of <see cref="Update()"/>, before any <see cref="MGWindow"/>s in <see cref="Windows"/> are updated.<br/>
        /// Objects that subscribe to this handler's keyboard events will be the very first to receive and handle the event.<para/>
        /// Highly recommended to avoid using this unless absolutely necessary, and if you do use it, you probably shouldn't call e.SetHandledBy(...) so other elements can still receive the input.</summary>
        public KeyboardHandler HighPriorityKeyboardHandler { get; }

        /// <summary>Manages drag-and-drop operations for all elements on this desktop.
        /// Call <see cref="DragDropManager.DoDragDrop"/> from a mouse-pressed handler to initiate a drag.</summary>
        public DragDropManager DragDropManager { get; private set; }

        bool IMouseViewport.IsInside(Vector2 Position) => ValidScreenBounds.ContainsInclusive(Position);
        Vector2 IMouseViewport.GetOffset() => Vector2.Zero;

        #region ToolTip
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGToolTip _ActiveToolTip;
        public MGToolTip ActiveToolTip
        {
            get => _ActiveToolTip;
            private set
            {
                if (_ActiveToolTip != value)
                {
                    bool Cancellable = true;
                    if (ActiveToolTip != null && value != null && ActiveToolTip.Host == value.Host)
                        Cancellable = false;

                    if (Cancellable)
                    {
                        CancelEventArgs<MGToolTip> e = new(value);
                        ToolTipOpening?.Invoke(this, e);
                        if (e.Cancel)
                            return;
                    }

                    if (ActiveToolTip != null)
                    {
                        ToolTipClosed?.Invoke(this, ActiveToolTip);
                        ActiveToolTip.Host.ToolTipChanged -= Host_ToolTipChanged;
                    }

                    _ActiveToolTip = value;
                    NPC(nameof(ActiveToolTip));

                    if (ActiveToolTip != null)
                    {
                        ToolTipOpened?.Invoke(this, ActiveToolTip);
                        ActiveToolTip.Host.ToolTipChanged += Host_ToolTipChanged;
                    }
                }
            }
        }

        private void Host_ToolTipChanged(object sender, EventArgs<MGToolTip> e)
        {
            ActiveToolTip = e.NewValue;
        }

        public event EventHandler<MGToolTip> ToolTipClosed;
        public event EventHandler<CancelEventArgs<MGToolTip>> ToolTipOpening;
        public event EventHandler<MGToolTip> ToolTipOpened;

        internal MGToolTip QueuedToolTip { get; set; } = null;

        /// <summary>Default value: 0.3s</summary>
        public static TimeSpan DefaultToolTipShowDelay { get; set; } = TimeSpan.FromSeconds(0.30);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan _ToolTipShowDelay;
        /// <summary>The amount of time that the mouse must hover a particular <see cref="MGElement"/> before its <see cref="MGElement.ToolTip"/> can be shown.<para/>
        /// Default value: <see cref="DefaultToolTipShowDelay"/></summary>
        public TimeSpan ToolTipShowDelay
        {
            get => _ToolTipShowDelay;
            set
            {
                if (_ToolTipShowDelay != value)
                {
                    _ToolTipShowDelay = value;
                    NPC(nameof(ToolTipShowDelay));
                }
            }
        }
        #endregion ToolTip

        #region Context Menu
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGContextMenu _ActiveContextMenu;
        /// <summary>The currently open <see cref="MGContextMenu"/>.<para/>
        /// To set this value, use <see cref="TryCloseActiveContextMenu"/> or <see cref="TryOpenContextMenu(MGContextMenu, Point)"/></summary>
        public MGContextMenu ActiveContextMenu => _ActiveContextMenu;

        /// <returns>True if there was no <see cref="ActiveContextMenu"/> or it was successfully closed.<br/>
        /// False if the action was cancelled such as via <see cref="ContextMenuClosing"/>'s <see cref="ContextMenuOpeningClosingEventArgs"/>.Cancel.</returns>
        public bool TryCloseActiveContextMenu()
        {
            if (ActiveContextMenu != null)
            {
                //  Close nested menus
                if (!ActiveContextMenu.TryCloseActiveContextMenu())
                    return false;

                MGContextMenu Previous = ActiveContextMenu;

                if (ContextMenuClosing != null)
                {
                    ContextMenuOpeningClosingEventArgs ClosingArgs = new(ActiveContextMenu, null);
                    ContextMenuClosing.Invoke(this, ClosingArgs);
                    if (ClosingArgs.Cancel)
                        return false;
                }

                ActiveContextMenu.InvokeContextMenuClosing();

                _ActiveContextMenu = null;
                NPC(nameof(ActiveContextMenu));

                Previous.InvokeContextMenuClosed();
                ContextMenuClosed?.Invoke(this, Previous);

                return true;
            }
            else
                return true;
        }

        /// <returns>True if the <paramref name="Menu"/> was already opened, or was successfully opened.<br/>
        /// False if the action was cancelled (such as via <see cref="ContextMenuOpeningClosingEventArgs"/>.Cancel while trying to close the current menu, or while trying to open the new menu), or because <see cref="MGContextMenu.CanContextMenuOpen"/> is false.</returns>
        public bool TryOpenContextMenu(MGContextMenu Menu, Rectangle Anchor)
        {
            if (!TryCloseActiveContextMenu())
                return false;

            if (Menu == null || !Menu.CanContextMenuOpen)
                return false;

            Rectangle ValidBounds = ValidScreenBounds;
            if (Menu.IsContextMenuOpen)
            {
                Size MenuSizeScreenSpace = new((int)(Menu.RenderBounds.Width * Menu.Scale), (int)(Menu.RenderBounds.Height * Menu.Scale));
                Point NewPosition = MGContextMenu.FitMenuToViewport(Anchor, MenuSizeScreenSpace, ValidBounds).TopLeft();
                Menu.Left = NewPosition.X;
                Menu.Top = NewPosition.Y;
                Menu.ValidateWindowSizeAndPosition();
                return true;
            }
            else
            {
                if (ContextMenuOpening != null)
                {
                    ContextMenuOpeningClosingEventArgs OpeningArgs = new(ActiveContextMenu, Menu);
                    ContextMenuOpening.Invoke(this, OpeningArgs);
                    if (OpeningArgs.Cancel)
                        return false;
                }

                if (!Menu.InvokeContextMenuOpening())
                    return false;

                _ActiveContextMenu = Menu;

                int MinWidth = 100;
                int MinHeight = 40;
                int MaxWidth = 1000;
                int MaxHeight = 800;

                Size MenuSizeUnscaledScreenSpace = Menu.ComputeContentSize(MinWidth, MinHeight, MaxWidth, MaxHeight);
                Size MenuSizeScreenSpace = new((int)(MenuSizeUnscaledScreenSpace.Width * Menu.Scale), (int)(MenuSizeUnscaledScreenSpace.Height * Menu.Scale));

                Point Position = MGContextMenu.FitMenuToViewport(Anchor, MenuSizeScreenSpace, ValidBounds).TopLeft();
                Menu.TopLeft = Position;
                _ = Menu.ApplySizeToContent(SizeToContent.WidthAndHeight, MinWidth, MinHeight, MaxWidth, MaxHeight, true);

                NPC(nameof(ActiveContextMenu));
                ActiveContextMenu.InvokeContextMenuOpened();
                ContextMenuOpened?.Invoke(this, Menu);

                return true;
            }
        }

        /// <returns>True if the <paramref name="Menu"/> was already opened, or was successfully opened.<br/>
        /// False if the action was cancelled (such as via <see cref="ContextMenuOpeningClosingEventArgs"/>.Cancel while trying to close the current menu, or while trying to open the new menu), or because <see cref="MGContextMenu.CanContextMenuOpen"/> is false.</returns>
        public bool TryOpenContextMenu(MGContextMenu Menu, Point Position) => TryOpenContextMenu(Menu, new Rectangle(Position.X, Position.Y, 1, 1));

        /// <summary>Invoked just before an <see cref="MGContextMenu"/> is opened. Allows cancellation.</summary>
        public event EventHandler<ContextMenuOpeningClosingEventArgs> ContextMenuOpening;
        /// <summary>Invoked immediately after an <see cref="MGContextMenu"/> is opened.</summary>
        public event EventHandler<MGContextMenu> ContextMenuOpened;
        /// <summary>Invoked just before an <see cref="MGContextMenu"/> is closed. Allows cancellation.</summary>
        public event EventHandler<ContextMenuOpeningClosingEventArgs> ContextMenuClosing;
        /// <summary>Invoked immediately after an <see cref="MGContextMenu"/> is closed.</summary>
        public event EventHandler<MGContextMenu> ContextMenuClosed;
        #endregion Context Menu

        #region Windows
        private MGWindow OverlayWindow { get; }
        /// <summary>A specialized <see cref="MGOverlayHost"/> used for drawing <see cref="MGOverlay"/>s overtop of this entire <see cref="MGDesktop"/>.<para/>
        /// This element's <see cref="MGWindow"/> is always updated first and drawn last, regardless of the <see cref="MGWindow.IsTopmost"/> state of other windows.</summary>
        public MGOverlayHost OverlayHost { get; }
        internal const string OverlayName = "DesktopOverlay";

        /// <summary>The last element represents the <see cref="MGWindow"/> that will be
        /// drawn last (I.E., rendered overtop of everything else), and updated first (I.E., has the first chance to handle inputs)<para/>
        /// except in cases where a Topmost window is prioritized (See: <see cref="MGWindow.IsTopmost"/>)<para/>
        /// Note: This list does not include the special window used to render overlays. See: <see cref="OverlayHost"/></summary>
        public List<MGWindow> Windows { get; }

        /// <summary>Moves the given <paramref name="Window"/> to the end of <see cref="Windows"/> list.<br/>
        /// It will typically be rendered overtop of all other <see cref="Windows"/> and have first chance at receiving/handling input,<br/>
        /// unless another window is Topmost (See: <see cref="MGWindow.IsTopmost"/>).</summary>
        /// <returns>True if the <paramref name="Window"/> was brought to the front. False if it was not a valid element in <see cref="Windows"/>.</returns>
        public bool BringToFront(MGWindow Window)
        {
            if (!Windows.Contains(Window))
            {
                return false;
            }
            else
            {
                if (Windows.IndexOf(Window) != Windows.Count - 1)
                {
                    Windows.Remove(Window);
                    Windows.Add(Window);
                }
                return true;
            }
        }

        /// <summary>Moves the given <paramref name="Window"/> to the start of <see cref="Windows"/> list.<br/>
        /// It will typically be rendered underneath of all other <see cref="Windows"/>, unless it is Topmost (See: <see cref="MGWindow.IsTopmost"/>)</summary>
        /// <returns>True if the <paramref name="Window"/> was moved to the back. False if it was not a valid element in <see cref="Windows"/>.</returns>
        public bool BringToBack(MGWindow Window)
        {
            if (!Windows.Contains(Window))
            {
                return false;
            }
            else
            {
                if (Windows.IndexOf(Window) != 0)
                {
                    Windows.Remove(Window);
                    Windows.Insert(0, Window);
                }
                return true;
            }
        }
        #endregion Windows

        #region Keyboard Focus
        private sealed record QueuedKeyboardFocusRequest(MGElement Target, KeyboardFocusSource Source);

        private QueuedKeyboardFocusRequest _QueuedFocusedKeyboardHandler;
        internal MGElement QueuedFocusedKeyboardHandler => _QueuedFocusedKeyboardHandler?.Target;
        internal KeyboardFocusSource? QueuedFocusedKeyboardHandlerSource => _QueuedFocusedKeyboardHandler?.Source;
        internal KeyboardFocusSource LastFocusChangeSource { get; private set; } = KeyboardFocusSource.Programmatic;
        private readonly Dictionary<MGWindow, MGElement> WindowFocusHistory = new();

        internal void QueueFocusedKeyboardHandler(MGElement target, KeyboardFocusSource source)
        {
            if (target == null)
            {
                ClearQueuedFocusedKeyboardHandler();
                return;
            }

            _QueuedFocusedKeyboardHandler = new(target, source);
        }

        internal void ClearQueuedFocusedKeyboardHandler() => _QueuedFocusedKeyboardHandler = null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _FocusedKeyboardHandler;
        /// <summary>The <see cref="MGElement"/> that should handle Keyboard inputs, if any.<para/>
        /// Only <see cref="MGElement"/>'s where <see cref="MGElement.CanHandleKeyboardInput"/> is true can be set as the <see cref="FocusedKeyboardHandler"/></summary>
        public MGElement FocusedKeyboardHandler
        {
            get => _FocusedKeyboardHandler;
            private set
            {
                if (_FocusedKeyboardHandler != value)
                {
                    if (value != null && !value.CanHandleKeyboardInput)
                        throw new InvalidOperationException($"{nameof(MGWindow)}.{nameof(FocusedKeyboardHandler)} cannot be set to an value with {nameof(MGElement)}.{nameof(MGElement.CanHandleKeyboardInput)}=false.");

                    MGElement Previous = FocusedKeyboardHandler;
                    LastFocusChangeSource = value != null && QueuedFocusedKeyboardHandler == value && QueuedFocusedKeyboardHandlerSource.HasValue
                        ? QueuedFocusedKeyboardHandlerSource.Value
                        : KeyboardFocusSource.Programmatic;
                    if (Previous is MGTextBox PreviousTextBox)
                        PreviousTextBox.ReadonlyChanged -= TextBox_ReadonlyChanged;

                    _FocusedKeyboardHandler = value;

                    if (FocusedKeyboardHandler is MGTextBox CurrentTextBox)
                        CurrentTextBox.ReadonlyChanged += TextBox_ReadonlyChanged;

                    if (FocusedKeyboardHandler?.SelfOrParentWindow != null)
                        WindowFocusHistory[FocusedKeyboardHandler.SelfOrParentWindow] = FocusedKeyboardHandler;

                    if (FocusedKeyboardHandler != null && ShouldAutoScrollFocusedElement(LastFocusChangeSource))
                        EnsureFocusedElementVisible(FocusedKeyboardHandler);

                    NPC(nameof(FocusedKeyboardHandler));
                    FocusedKeyboardHandlerChanged?.Invoke(this, new(Previous, FocusedKeyboardHandler));
                }
            }
        }

        private void TextBox_ReadonlyChanged(object sender, bool IsReadonly)
        {
            if (sender is MGTextBox TextBox && IsReadonly && FocusedKeyboardHandler == TextBox)
                FocusedKeyboardHandler = null;
        }

        private static void EnsureFocusedElementVisible(MGElement focusedElement)
        {
            for (MGElement current = focusedElement?.Parent; current != null; current = current.Parent)
            {
                if (current is MGScrollViewer scrollViewer)
                    scrollViewer.EnsureElementVisible(focusedElement);
            }
        }

        private void ApplyQueuedFocusChange()
        {
            if (_QueuedFocusedKeyboardHandler == null)
                return;

            FocusedKeyboardHandler = QueuedFocusedKeyboardHandler;
            ClearQueuedFocusedKeyboardHandler();
        }

        private MGElement ResolveAutoFocusTarget(MGElement root, bool preferWindowDefault)
        {
            if (root is not MGWindow window)
                return GetFocusableElements(root).FirstOrDefault();

            MGElement defaultFocus = window.DefaultFocusElement;
            MGElement lastFocused = WindowFocusHistory.TryGetValue(window, out MGElement previousFocus) ? previousFocus : null;
            MGElement firstFocusable = GetFocusableElements(window).FirstOrDefault();

            defaultFocus = IsNavigationTarget(defaultFocus) && window.IsSelfOrAncestorOf(defaultFocus) ? defaultFocus : null;
            lastFocused = IsNavigationTarget(lastFocused) && window.IsSelfOrAncestorOf(lastFocused) ? lastFocused : null;

            return ResolveAutoFocusTarget(defaultFocus, lastFocused, firstFocusable, preferWindowDefault);
        }

        private void QueueAutoFocusIfNeeded(bool preferWindowDefault)
        {
            if (ActiveInputMode == UIInputMode.Pointer || QueuedFocusedKeyboardHandler != null || FocusedKeyboardHandler != null || GetHoveredNavigationTarget() != null)
                return;

            MGElement target = ResolveAutoFocusTarget(GetNavigationRoot(), preferWindowDefault);
            if (target != null)
                QueueFocusedKeyboardHandler(target, KeyboardFocusSource.Programmatic);
        }

        internal void NotifyWindowOpened(MGWindow window)
        {
            if (window == null || ActiveInputMode == UIInputMode.Pointer)
                return;

            MGElement target = ResolveAutoFocusTarget(window, true);
            if (target != null)
                QueueFocusedKeyboardHandler(target, KeyboardFocusSource.Programmatic);
        }

        internal void NotifyWindowClosed(MGWindow window)
        {
            if (window == null)
                return;

            WindowFocusHistory.Remove(window);

            if (QueuedFocusedKeyboardHandler?.SelfOrParentWindow == window)
                ClearQueuedFocusedKeyboardHandler();

            if (FocusedKeyboardHandler?.SelfOrParentWindow == window)
                FocusedKeyboardHandler = null;

            if (ActiveInputMode != UIInputMode.Pointer)
                QueueAutoFocusIfNeeded(false);
        }

        public event EventHandler<EventArgs<MGElement>> FocusedKeyboardHandlerChanged;
        #endregion Keyboard Focus

        /// <summary>Represents the screen space that can be occupied with <see cref="MGElement"/>s.<para/>
        /// For example, an <see cref="MGContextMenu"/> will attempt to position itself such that it is not rendered outside of these bounds.</summary>
        public Rectangle ValidScreenBounds => Renderer.Surface.GetBounds();

        public MGResources Resources { get; }
        /// <summary>Convenience property that just returns <see cref="Resources"/>.<see cref="MGResources.DefaultTheme"/></summary>
        public MGTheme Theme => Resources.DefaultTheme;

        public MGDesktop(MainRenderer Renderer)
        {
            ApartmentState ThreadState = Thread.CurrentThread.GetApartmentState();
            if (ThreadState != ApartmentState.STA)
            {
                Debug.WriteLine(
                    $"WARNING: {nameof(MGUI)}.{nameof(Core)}.{nameof(UI)}.{nameof(MGDesktop)} is being instantiated from a thread whose {nameof(ApartmentState)}={ThreadState}. " +
                    $"You may experience unforeseen issues when running from a non-{ApartmentState.STA} {nameof(ApartmentState)}. " +
                    $"It is recommended to add the {nameof(STAThreadAttribute)} (\"[STAThread]\") to your program's main entry function to avoid issues."
                );
            }

            this.Renderer = Renderer;
            Windows = new();
            Resources = new(new MGTheme(Renderer.FontManager.DefaultFontFamily));
            _ = new UIView(this, Renderer.Surface);

            OverlayWindow = new(this, 0, 0, ValidScreenBounds.Width, ValidScreenBounds.Height)
            {
                WindowStyle = WindowStyle.None,
                AllowsClickThrough = true
            };
            OverlayHost = new(OverlayWindow) { Name = OverlayName };
            OverlayWindow.SetContent(OverlayHost);
            OverlayWindow.CanChangeContent = false;

            #region Sample Icons
            Texture2D CheckMark_64x64 = Renderer.Content.Load<Texture2D>(Path.Combine("Icons", "CheckMark_64x64"));
            Resources.AddTexture("CheckMark_64x64", new(CheckMark_64x64));

            Texture2D AngryMeteor_MilitaryIconsSet = Renderer.Content.Load<Texture2D>(Path.Combine("Icons", "AngryMeteor_MilitaryIconsSet"));
            Resources.AddTexture("AngryMeteor", new(AngryMeteor_MilitaryIconsSet));

            int TextureTopMargin = 6;
            int TextureSpacing = 1;
            int TextureIconSize = 16;
            List<(string Name, int Row, int Column)> Icons = new()
            {
                ("ArrowRightGreen", 0, 0),
                ("ArrowDownGreen", 0, 1),
                ("ArrowLeftGreen", 1, 0),
                ("ArrowUpGreen", 1, 1),

                ("GoldBullion", 1, 4),
                ("SilverBullion", 1, 5),
                ("BronzeBullion", 1, 6),

                ("SkullOpen", 1, 7),
                ("SkullClosed", 1, 8),
                ("SkullAndCrossbones", 4, 3),

                ("Wrench", 1, 10),
                ("Gear", 8, 3),

                ("Backpack", 1, 11),

                ("Diamond", 2, 9),
                ("Emerald", 2, 10),
                ("Ruby", 2, 11),

                ("GoldMedal", 2, 4),
                ("SilverMedal", 2, 5),
                ("BronzeMedal", 2, 6),

                ("Delete", 5, 3),
                ("CheckMarkGreen", 5, 4),

                ("Computer", 4, 10),
                ("Save", 4, 11),

                ("SteelFloor", 2, 2)
            };
            foreach (var (Name, Row, Column) in Icons)
            {
                Rectangle SourceRect = new(Column * (TextureIconSize + TextureSpacing), TextureTopMargin + Row * (TextureIconSize + TextureSpacing), TextureIconSize, TextureIconSize);
                MGTextureData TextureData = new(AngryMeteor_MilitaryIconsSet, SourceRect);
                Resources.AddTexture(Name, TextureData);
            }

            TextureTopMargin = 166;
            TextureIconSize = 12;
            Icons = new List<(string Name, int Row, int Column)>()
            {
                ("CheckMarkGreen_12x12", 1, 2)
            };
            foreach (var (Name, Row, Column) in Icons)
            {
                Rectangle SourceRect = new(Column * (TextureIconSize + TextureSpacing), TextureTopMargin + Row * (TextureIconSize + TextureSpacing), TextureIconSize, TextureIconSize);
                MGTextureData TextureData = new(AngryMeteor_MilitaryIconsSet, SourceRect);
                Resources.AddTexture(Name, TextureData);
            }
            #endregion Sample Icons

            #region Docking Icons
            string[] DockIconEntries = {
                "x-white",                    "DockClose",
                "maximize-white",             "DockMaximize",
                "minimize-white",             "DockMinimize",
                "pin-white",                  "DockPin",
                "pin-off-white",              "DockPinOff",
                // panel direction icons — dashed = per-panel joystick, solid = host-edge
                "panel-left-dashed-white",    "DockPanelLeftDashed",
                "panel-right-dashed-white",   "DockPanelRightDashed",
                "panel-top-dashed-white",     "DockPanelTopDashed",
                "panel-bottom-dashed-white",  "DockPanelBottomDashed",
                "panel-center-dashed-white",  "DockPanelCenterDashed",
                "panel-left-white",           "DockPanelLeft",
                "panel-right-white",          "DockPanelRight",
                "panel-top-white",            "DockPanelTop",
                "panel-bottom-white",         "DockPanelBottom",
                "panel-center-white",         "DockPanelCenter"
            };
            for (int i = 0; i < DockIconEntries.Length; i += 2)
            {
                string fileName   = DockIconEntries[i];
                string resourceId = DockIconEntries[i + 1];
                try
                {
                    Texture2D DockTex = Renderer.Content.Load<Texture2D>(Path.Combine("Icons", "docking", fileName));
                    Resources.AddTexture(resourceId, new(DockTex));
                }
                catch { /* icon not found — skip silently */ }
            }
            #endregion Docking Icons

            ToolTipShowDelay = DefaultToolTipShowDelay;

            HighPriorityMouseHandler = InputTracker.Mouse.CreateHandler(this, null);
            HighPriorityKeyboardHandler = InputTracker.Keyboard.CreateHandler(this, null);

            HighPriorityMouseHandler.PressedInside  += (sender, e) => { ClearQueuedFocusedKeyboardHandler(); };
            HighPriorityMouseHandler.PressedOutside += (sender, e) => { ClearQueuedFocusedKeyboardHandler(); };
            HighPriorityKeyboardHandler.Pressed += (sender, e) => { _ = TryDispatchNavigationAction(e); };

            // Wire LMB release to finalize or cancel any active drag-and-drop
            HighPriorityMouseHandler.ReleasedInside  += (sender, e) => { if (e.IsLMB && DragDropManager.IsDragging) DragDropManager.NotifyDrop(DragDropManager.CurrentDropTarget, e.Position); };
            HighPriorityMouseHandler.ReleasedOutside += (sender, e) => { if (e.IsLMB && DragDropManager.IsDragging) DragDropManager.CancelDrag(); };

            DragDropManager = new DragDropManager(this);

            //  Recalculate the layout of text-based elements when the text-rendering backend changes
            Renderer.TextEngineChanged += (sender, e) =>
            {
                foreach (MGWindow window in Windows)
                    foreach (MGTextBlock tb in window.TraverseVisualTree<MGTextBlock>(true, true, true, true, MGElement.TreeTraversalMode.Preorder))
                        tb.RefreshTextEngine();
                //  Note: We don't need to call InvalidateAllLayouts() because the parent elements of MGTextBlocks will already receive LayoutChanged notifications.
                //  The only reason InvalidateAllLayouts would be needed is if an MGElement instance other than MGTextBlock rendered text in its DrawSelf method
                //  (currently, all text-drawing is funnelled through MGTextBlocks, even for things like MGTimer/MGStopWatch/MGTextBox)
            };
        }

        public void Update()
        {
            //  Revalidate the size/position of the OverlayWindow
            if (ValidScreenBounds != new Rectangle(OverlayWindow.Left, OverlayWindow.Top, OverlayWindow.WindowWidth, OverlayWindow.WindowHeight))
            {
                OverlayWindow.Left = ValidScreenBounds.Left;
                OverlayWindow.Top = ValidScreenBounds.Top;
                OverlayWindow.WindowWidth = ValidScreenBounds.Width;
                OverlayWindow.WindowHeight = ValidScreenBounds.Height;
            }

            UpdateBaseArgs BA = Renderer.UpdateArgs;

            MGElement focusCandidate = QueuedFocusedKeyboardHandler ?? FocusedKeyboardHandler;
            bool isTextEntryFocused = focusCandidate is MGTextBox focusedTextBox && !focusedTextBox.IsReadonly;
            bool hasNavigationActivity = HasKeyboardActivity(InputTracker.Keyboard) || InputTracker.GamePad.HasActivity();
            ActiveInputMode = ResolveInputMode(HasMouseActivity(InputTracker.Mouse), hasNavigationActivity, isTextEntryFocused, ActiveInputMode);
            QueueAutoFocusIfNeeded(true);
            ApplyQueuedFocusChange();

            HighPriorityMouseHandler.ManualUpdate();
            HighPriorityKeyboardHandler.ManualUpdate();
            ApplyQueuedFocusChange();
            _ = TryDispatchGamePadNavigationActions();
            ApplyQueuedFocusChange();

            QueuedToolTip = null;

            ElementUpdateArgs UA = new(BA, true, false, true, Point.Zero, ValidScreenBounds);

            ActiveContextMenu?.Update(UA);
            ActiveToolTip?.Update(UA.ChangeHitTestVisible(ActiveToolTip.ParentWindow.IsHitTestVisible));

            bool IsWindowOccludedAtMousePos = false;

            List<MGWindow> OrderedWindows = Windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost).ToList();
            OrderedWindows.Insert(0, OverlayWindow);

            foreach (MGWindow Window in OrderedWindows)
            {
                MGToolTip PreviousQueuedToolTip = QueuedToolTip;

                bool IsOverlayWindow = Window == OverlayWindow;
                bool ProcessInputs = (IsOverlayWindow && OverlayHost.ActiveOverlay != null) || (!IsOverlayWindow && (OverlayHost.ActiveOverlay == null || !OverlayHost.IsModal));
                Window.Update(ProcessInputs ? UA : UA with { IsHitTestVisible = false });

                //  Disallow occluded windows from overriding the active ToolTip
                //  TODO probably also need similar logic in MGWindow.OnBeginUpdateContents in case it has nested window(s)
                QueuedToolTip = IsWindowOccludedAtMousePos ? PreviousQueuedToolTip : QueuedToolTip;

                //  The next window that we update is visually occluded at the current mouse position if the current window is being hovered,
                //  since that means the mouse is hovering a window that is drawn overtop of the next window
                if (!IsWindowOccludedAtMousePos && Window.VisualState.IsPressedOrHovered)
                {
                    if (IsOverlayWindow) // When an overlay is being shown, disallow showing of tooltips that belong to windows underneath the overlay
                        IsWindowOccludedAtMousePos = OverlayHost.ActiveOverlay != null;
                    else if (!Window.AllowsClickThrough)
                        IsWindowOccludedAtMousePos = true;
                    else
                    {
                        //  Since this window DOES allow click-through, validate that at least one opaque element is being hovered
                        MGElement OpaqueHoveredElement = FindFirstOpaqueParent(Window.HoveredElement, true);
                        if (OpaqueHoveredElement != null && OpaqueHoveredElement != Window)
                            IsWindowOccludedAtMousePos = true;
                    }
                }
            }

            ActiveToolTip = QueuedToolTip;
            ApplyQueuedFocusChange();
        }

        /// <summary>Traverses up the visual tree, starting from the given <paramref name="Element"/>, looking for an <see cref="MGElement"/> that is fully opaque (<see cref="MGElement.Opacity"/> >= 1.0f)</summary>
        /// <param name="IncludeSelf">If true, this method may return the input <paramref name="Element"/>. If false, starts checking for valid matches from the input's <see cref="MGElement.Parent"/></param>
        private static MGElement FindFirstOpaqueParent(MGElement Element, bool IncludeSelf)
        {
            MGElement Current = IncludeSelf ? Element : Element?.Parent;
            while (Current != null)
            {
                if (Current.Opacity >= 1f || Current.Opacity.IsAlmostEqual(1f))
                    return Current;
                else
                    Current = Current.Parent;
            }
            return null;
        }

        public void Draw(DrawTransaction DT, float Opacity = 1.0f)
        {
            DrawBaseArgs BA = new(Renderer.UpdateArgs.TotalElapsed, DT, Opacity);
            ElementDrawArgs DA = new(BA, new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);

            Rectangle ScreenBounds = ValidScreenBounds;
            if (!BA.DT.CurrentSettings.RasterizerState.ScissorTestEnable || ScreenBounds.Intersects(BA.DT.GD.ScissorRectangle))
            {
                using (BA.DT.SetClipTargetTemporary(ScreenBounds, true))
                {
                    foreach (MGWindow Window in Windows.OrderBy(x => x.IsTopmost))
                    {
                        Window.Draw(DA);
                    }

                    if (OverlayHost.ActiveOverlay != null)
                        OverlayWindow.Draw(DA);

                    //  The ToolTip only takes priority if it is a ToolTip belonging to the current ContextMenu
                    if (ActiveToolTip != null && ActiveContextMenu != null && ActiveToolTip.ParentWindow == ActiveContextMenu)
                    {
                        ActiveContextMenu?.Draw(DA);
                        ActiveToolTip?.DrawAtDefaultPosition(DA);
                    }
                    else
                    {
                        ActiveToolTip?.DrawAtDefaultPosition(DA);
                        ActiveContextMenu?.Draw(DA);
                    }
                }
            }
        }

        /// <param name="InitialDrawSettings">If null, uses <see cref="DrawSettings.Default"/></param>
        public void Draw(float Opacity = 1.0f, DrawSettings InitialDrawSettings = null)
        {
            using (DrawTransaction DT = new(Renderer, InitialDrawSettings ?? DrawSettings.Default, false))
            {
                Draw(DT, Opacity);
            }
        }
    }
}
