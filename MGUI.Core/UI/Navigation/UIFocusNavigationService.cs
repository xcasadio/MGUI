using MGUI.Shared.Input.GamePad;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI.Navigation
{
    public class UIFocusNavigationService
    {
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

        private MGDesktop Desktop { get; }
        private List<FocusScopeEntry> FocusScopes { get; } = new();

        public UIFocusNavigationService(MGDesktop desktop)
        {
            Desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        }

        internal void PushFocusScope(MGElement scopeRoot, MGElement restoreFocusTarget = null)
        {
            if (scopeRoot == null)
            {
                return;
            }

            int existingIndex = FocusScopes.FindLastIndex(x => x.ScopeRoot == scopeRoot);
            if (existingIndex >= 0)
            {
                FocusScopeEntry existingEntry = FocusScopes[existingIndex];
                FocusScopes.RemoveAt(existingIndex);
                existingEntry.RestoreFocusTarget = restoreFocusTarget ?? existingEntry.RestoreFocusTarget;
                FocusScopes.Add(existingEntry);
                return;
            }

            FocusScopes.Add(new(scopeRoot, restoreFocusTarget ?? Desktop.FocusedKeyboardHandler));
        }

        internal void PopFocusScope(MGElement scopeRoot)
        {
            if (scopeRoot == null)
            {
                return;
            }

            int existingIndex = FocusScopes.FindLastIndex(x => x.ScopeRoot == scopeRoot);
            if (existingIndex < 0)
            {
                return;
            }

            bool wasActiveScope = existingIndex == FocusScopes.Count - 1;
            FocusScopeEntry entry = FocusScopes[existingIndex];
            FocusScopes.RemoveAt(existingIndex);

            bool shouldRestoreFocus = wasActiveScope
                && Desktop.QueuedFocusedKeyboardHandler == null
                && (Desktop.FocusedKeyboardHandler == null || IsWithinFocusScope(scopeRoot, Desktop.FocusedKeyboardHandler, current => current.Parent));
            if (shouldRestoreFocus && IsNavigationTarget(entry.RestoreFocusTarget))
            {
                entry.RestoreFocusTarget.Focus(KeyboardFocusSource.Pointer);
            }
        }

        public IReadOnlyList<MGElement> GetFocusableElements()
            => GetFocusableElements(GetNavigationRoot());

        public bool MoveFocusNext()
            => MoveFocusNext(KeyboardFocusSource.Keyboard);

        public bool MoveFocusNext(KeyboardFocusSource source)
        {
            IReadOnlyList<MGElement> focusableElements = GetFocusableElements();
            MGElement anchor = Desktop.FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
            if (anchor == null)
            {
                MGElement autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
                if (!IsNavigationTarget(autoFocusTarget))
                {
                    return false;
                }

                autoFocusTarget.Focus(source);
                return true;
            }

            int currentIndex = focusableElements.Select((element, index) => new { element, index }).FirstOrDefault(x => x.element == anchor)?.index ?? -1;
            int nextIndex = MGDesktop.GetWrappedFocusIndex(focusableElements.Count, currentIndex, true);
            if (nextIndex < 0)
            {
                return false;
            }

            focusableElements[nextIndex].Focus(source);
            return true;
        }

        public bool MoveFocusPrevious()
            => MoveFocusPrevious(KeyboardFocusSource.Keyboard);

        public bool MoveFocusPrevious(KeyboardFocusSource source)
        {
            IReadOnlyList<MGElement> focusableElements = GetFocusableElements();
            MGElement anchor = Desktop.FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
            if (anchor == null)
            {
                MGElement autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
                if (!IsNavigationTarget(autoFocusTarget))
                {
                    return false;
                }

                autoFocusTarget.Focus(source);
                return true;
            }

            int currentIndex = focusableElements.Select((element, index) => new { element, index }).FirstOrDefault(x => x.element == anchor)?.index ?? -1;
            int nextIndex = MGDesktop.GetWrappedFocusIndex(focusableElements.Count, currentIndex, false);
            if (nextIndex < 0)
            {
                return false;
            }

            focusableElements[nextIndex].Focus(source);
            return true;
        }

        public bool NavigateTo(NavigationDirection direction)
            => NavigateTo(direction, KeyboardFocusSource.Keyboard);

        public bool NavigateTo(NavigationDirection direction, KeyboardFocusSource source)
        {
            MGElement focusedElement = Desktop.FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
            if (focusedElement == null)
            {
                MGElement autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
                if (!IsNavigationTarget(autoFocusTarget))
                {
                    return false;
                }

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
            int targetIndex = MGDesktop.FindDirectionalNavigationTarget(focusedElement.ActualLayoutBounds, candidates.Select(x => x.ActualLayoutBounds).ToList(), direction);
            if (targetIndex < 0)
            {
                if (Desktop.FocusedKeyboardHandler == null && IsNavigationTarget(focusedElement))
                {
                    focusedElement.Focus(source);
                    return true;
                }

                return false;
            }

            candidates[targetIndex].Focus(source);
            return true;
        }

        internal bool TryDispatchNavigationAction(BaseKeyPressedEventArgs e)
        {
            if (e.IsHandled)
            {
                return false;
            }

            MGElement focusedElement = Desktop.CanElementReceiveKeyboardInput(Desktop.FocusedKeyboardHandler)
                ? Desktop.FocusedKeyboardHandler
                : null;
            bool shouldPreserveTextEntryKey = focusedElement is MGTextBox focusedTextBox && focusedTextBox.ShouldPreserveTextEntryKey(e.Key);
            if (!FocusInputPolicy.TryGetNavigationAction(e.Key, e.Tracker.IsShiftDown, focusedElement is MGTextBox, shouldPreserveTextEntryKey, out UINavigationAction action))
            {
                return false;
            }

            if (TryDispatchNavigationAction(action, KeyboardFocusSource.Keyboard, out IKeyboardHandlerHost handledBy))
            {
                e.SetHandledBy(handledBy, false);
                return true;
            }

            return false;
        }

        internal bool TryDispatchNavigationAction(UINavigationAction action, KeyboardFocusSource source)
            => TryDispatchNavigationAction(action, source, out _);

        internal bool TryDispatchNavigationAction(UINavigationAction action, KeyboardFocusSource source, out IKeyboardHandlerHost handledBy)
        {
            MGElement focusedElement = Desktop.CanElementReceiveKeyboardInput(Desktop.FocusedKeyboardHandler)
                ? Desktop.FocusedKeyboardHandler
                : null;

            Func<UINavigationAction, bool> tryHandleFocusedAction = focusedElement == null ? null : new Func<UINavigationAction, bool>(actionToHandle => focusedElement.TryHandleNavigationAction(actionToHandle));
            if (tryHandleFocusedAction?.Invoke(action) == true)
            {
                EnsureNavigationTargetVisible(focusedElement);
                handledBy = focusedElement;
                return true;
            }

            if (TryPerformFallbackNavigation(action, source))
            {
                handledBy = Desktop;
                return true;
            }

            handledBy = null;
            return false;
        }

        internal bool TryDispatchGamePadNavigationActions()
        {
            bool handledAny = false;
            foreach (GamePadButton button in GamePadNavigationButtons)
            {
                if (!Desktop.InputTracker.GamePad.WasTriggered(button) || !TryMapGamePadNavigationAction(button, out UINavigationAction action))
                {
                    continue;
                }

                MGElement focusedElement = Desktop.CanElementReceiveKeyboardInput(Desktop.FocusedKeyboardHandler)
                    ? Desktop.FocusedKeyboardHandler
                    : null;
                Func<UINavigationAction, bool> tryHandleFocusedAction = focusedElement == null ? null : new Func<UINavigationAction, bool>(actionToHandle => focusedElement.TryHandleNavigationAction(actionToHandle));
                if (tryHandleFocusedAction?.Invoke(action) == true)
                {
                    EnsureNavigationTargetVisible(focusedElement);
                    handledAny = true;
                }
                else if (TryPerformFallbackNavigation(action, KeyboardFocusSource.GamePad))
                {
                    handledAny = true;
                }
            }

            return handledAny;
        }

        internal void QueueAutoFocusIfNeeded(bool preferWindowDefault)
        {
            if (Desktop.ActiveInputMode == UIInputMode.Pointer || Desktop.QueuedFocusedKeyboardHandler != null || Desktop.FocusedKeyboardHandler != null || GetHoveredNavigationTarget() != null)
            {
                return;
            }

            MGElement target = ResolveAutoFocusTarget(GetNavigationRoot(), preferWindowDefault);
            if (target != null)
            {
                Desktop.QueueFocusedKeyboardHandler(target, KeyboardFocusSource.Programmatic);
            }
        }

        internal void NotifyWindowOpened(MGWindow window)
        {
            if (window == null || Desktop.ActiveInputMode == UIInputMode.Pointer)
            {
                return;
            }

            MGElement target = ResolveAutoFocusTarget(window, true);
            if (target != null)
            {
                Desktop.QueueFocusedKeyboardHandler(target, KeyboardFocusSource.Programmatic);
            }
        }

        internal void NotifyWindowClosed(MGWindow window)
        {
            if (window == null)
            {
                return;
            }

            Desktop.State.WindowFocusHistory.Remove(window);

            if (Desktop.QueuedFocusedKeyboardHandler?.SelfOrParentWindow == window)
            {
                Desktop.ClearQueuedFocusedKeyboardHandler();
            }

            if (Desktop.FocusedKeyboardHandler?.SelfOrParentWindow == window)
            {
                Desktop.ClearFocusedKeyboardHandler();
            }

            if (Desktop.ActiveInputMode != UIInputMode.Pointer)
            {
                QueueAutoFocusIfNeeded(false);
            }
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

        private MGElement ResolveAutoFocusTarget(MGElement root, bool preferWindowDefault)
        {
            if (root is not MGWindow window)
            {
                return GetFocusableElements(root).FirstOrDefault();
            }

            MGElement defaultFocus = window.DefaultFocusElement;
            MGElement lastFocused = Desktop.State.WindowFocusHistory.TryGetValue(window, out MGElement previousFocus) ? previousFocus : null;
            MGElement firstFocusable = GetFocusableElements(window).FirstOrDefault();

            defaultFocus = IsNavigationTarget(defaultFocus) && window.IsSelfOrAncestorOf(defaultFocus) ? defaultFocus : null;
            lastFocused = IsNavigationTarget(lastFocused) && window.IsSelfOrAncestorOf(lastFocused) ? lastFocused : null;

            return MGDesktop.ResolveAutoFocusTarget(defaultFocus, lastFocused, firstFocusable, preferWindowDefault);
        }

        private MGElement GetHoveredNavigationTarget()
        {
            foreach (MGWindow window in Desktop.Windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost))
            {
                MGElement hoveredTarget = GetNearestNavigationTarget(window.HoveredElement);
                if (hoveredTarget != null)
                {
                    return hoveredTarget;
                }
            }

            return null;
        }

        private MGElement GetNavigationRoot()
        {
            MGElement activeScopeRoot = GetActiveFocusScopeRoot(FocusScopes.Select(x => x.ScopeRoot).ToList());
            MGElement focusedWindowRoot = Desktop.FocusedKeyboardHandler?.SelfOrParentWindow;
            MGElement hoveredWindowRoot = GetHoveredNavigationTarget()?.SelfOrParentWindow;
            MGElement topWindowRoot = Desktop.Windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost).FirstOrDefault();

            return ResolveNavigationRoot(activeScopeRoot, focusedWindowRoot, hoveredWindowRoot, topWindowRoot);
        }

        private MGElement GetNearestNavigationTarget(MGElement element)
        {
            for (MGElement current = element; current != null; current = current.Parent)
            {
                if (IsNavigationTarget(current))
                {
                    return current;
                }
            }

            return null;
        }

        private IReadOnlyList<MGElement> GetFocusableElements(MGElement root)
        {
            if (root == null)
            {
                return Array.Empty<MGElement>();
            }

            return root.TraverseVisualTree(true, false, false, false, MGElement.TreeTraversalMode.Preorder)
                .Where(IsNavigationTarget)
                .Distinct()
                .OrderBy(x => x.TabIndex)
                .ThenBy(x => x.ActualLayoutBounds.Top)
                .ThenBy(x => x.ActualLayoutBounds.Left)
                .ToList();
        }

        private bool IsNavigationTarget(MGElement element)
            => Desktop.IsNavigationTarget(element);

        private static void EnsureNavigationTargetVisible(MGElement focusedElement)
        {
            if (focusedElement is INavigationTargetVisibilityHandler navigationTargetVisibilityHandler)
            {
                navigationTargetVisibilityHandler.EnsureNavigationTargetVisible();
            }
        }

        private static T GetActiveFocusScopeRoot<T>(IReadOnlyList<T> scopeRoots)
            where T : class
            => scopeRoots?.LastOrDefault();

        private static T ResolveNavigationRoot<T>(T activeScopeRoot, T focusedWindowRoot, T hoveredWindowRoot, T topWindowRoot)
            where T : class
            => activeScopeRoot ?? focusedWindowRoot ?? hoveredWindowRoot ?? topWindowRoot;

        private static bool IsWithinFocusScope<T>(T scopeRoot, T element, Func<T, T> getParent)
            where T : class
        {
            if (scopeRoot == null || element == null || getParent == null)
            {
                return false;
            }

            T current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, scopeRoot))
                {
                    return true;
                }

                current = getParent(current);
            }

            return false;
        }

        private static bool TryMapNavigationAction(Keys key, bool isShiftDown, out UINavigationAction action)
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

        private static bool TryMapGamePadNavigationAction(GamePadButton button, out UINavigationAction action)
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
    }
}