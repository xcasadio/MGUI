using MGUI.Shared.Input.GamePad;
using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Core.UI.Navigation;

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

        var existingIndex = FocusScopes.FindLastIndex(x => x.ScopeRoot == scopeRoot);
        if (existingIndex >= 0)
        {
            var existingEntry = FocusScopes[existingIndex];
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

        var existingIndex = FocusScopes.FindLastIndex(x => x.ScopeRoot == scopeRoot);
        if (existingIndex < 0)
        {
            return;
        }

        var wasActiveScope = existingIndex == FocusScopes.Count - 1;
        var entry = FocusScopes[existingIndex];
        FocusScopes.RemoveAt(existingIndex);

        var shouldRestoreFocus = wasActiveScope
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
        var focusableElements = GetFocusableElements();
        var anchor = Desktop.FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
        if (anchor == null)
        {
            var autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
            if (!IsNavigationTarget(autoFocusTarget))
            {
                return false;
            }

            autoFocusTarget.Focus(source);
            return true;
        }

        var currentIndex = focusableElements.Select((element, index) => new { element, index }).FirstOrDefault(x => x.element == anchor)?.index ?? -1;
        var nextIndex = MGDesktop.GetWrappedFocusIndex(focusableElements.Count, currentIndex, true);
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
        var focusableElements = GetFocusableElements();
        var anchor = Desktop.FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
        if (anchor == null)
        {
            var autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
            if (!IsNavigationTarget(autoFocusTarget))
            {
                return false;
            }

            autoFocusTarget.Focus(source);
            return true;
        }

        var currentIndex = focusableElements.Select((element, index) => new { element, index }).FirstOrDefault(x => x.element == anchor)?.index ?? -1;
        var nextIndex = MGDesktop.GetWrappedFocusIndex(focusableElements.Count, currentIndex, false);
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
        var focusedElement = Desktop.FocusedKeyboardHandler ?? GetHoveredNavigationTarget();
        if (focusedElement == null)
        {
            var autoFocusTarget = ResolveAutoFocusTarget(GetNavigationRoot(), true);
            if (!IsNavigationTarget(autoFocusTarget))
            {
                return false;
            }

            autoFocusTarget.Focus(source);
            return true;
        }

        if (focusedElement.NavigationNeighbors.TryGetValue(direction, out var explicitNeighbor) && IsNavigationTarget(explicitNeighbor))
        {
            explicitNeighbor.Focus(source);
            return true;
        }

        var focusableElements = GetFocusableElements();
        var candidates = focusableElements.Where(x => x != focusedElement).ToList();
        var targetIndex = MGDesktop.FindDirectionalNavigationTarget(focusedElement.ActualLayoutBounds, candidates.Select(x => x.ActualLayoutBounds).ToList(), direction);
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

        var focusedElement = Desktop.CanElementReceiveKeyboardInput(Desktop.FocusedKeyboardHandler)
            ? Desktop.FocusedKeyboardHandler
            : null;
        var shouldPreserveTextEntryKey = ShouldPreserveTextEntryKey(e.Key);
        if (!FocusInputPolicy.TryGetNavigationAction(e.Key, e.Tracker.IsShiftDown, focusedElement is ITextEntryHost, shouldPreserveTextEntryKey, out var action))
        {
            return false;
        }

        if (TryDispatchNavigationAction(action, KeyboardFocusSource.Keyboard, out var handledBy))
        {
            e.SetHandledBy(handledBy, false);
            return true;
        }

        return false;
    }

    internal bool TryDispatchNavigationAction(UINavigationAction action, KeyboardFocusSource source)
        => TryDispatchNavigationAction(action, source, out _);

    /// <summary>Semantic-path entry point: applies the same text-entry key preservation guard as the raw
    /// keyboard path (<see cref="TryDispatchNavigationAction(BaseKeyPressedEventArgs)"/>) before dispatching,
    /// so a key that a focused <see cref="MGTextBox"/> reserves for editing (e.g. arrow keys, Tab when
    /// <c>AcceptsTab</c>) is never reinterpreted as navigation on either path.</summary>
    internal bool TryDispatchNavigationAction(UINavigationAction action, KeyboardFocusSource source, Keys? key)
        => TryDispatchNavigationAction(action, source, key, out _);

    internal bool TryDispatchNavigationAction(UINavigationAction action, KeyboardFocusSource source, Keys? key, out IKeyboardHandlerHost handledBy)
    {
        if (ShouldPreserveTextEntryKey(key))
        {
            handledBy = null;
            return false;
        }

        return TryDispatchNavigationAction(action, source, out handledBy);
    }

    internal bool TryDispatchNavigationAction(UINavigationAction action, KeyboardFocusSource source, out IKeyboardHandlerHost handledBy)
    {
        var focusedElement = Desktop.CanElementReceiveKeyboardInput(Desktop.FocusedKeyboardHandler)
            ? Desktop.FocusedKeyboardHandler
            : null;

        var tryHandleFocusedAction = focusedElement == null ? null : new Func<UINavigationAction, bool>(actionToHandle => focusedElement.TryHandleNavigationAction(actionToHandle));
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
        var handledAny = false;
        foreach (var button in GamePadNavigationButtons)
        {
            if (!Desktop.InputTracker.GamePad.WasTriggered(button) || !TryMapGamePadNavigationAction(button, out var action))
            {
                continue;
            }

            var focusedElement = Desktop.CanElementReceiveKeyboardInput(Desktop.FocusedKeyboardHandler)
                ? Desktop.FocusedKeyboardHandler
                : null;
            var tryHandleFocusedAction = focusedElement == null ? null : new Func<UINavigationAction, bool>(actionToHandle => focusedElement.TryHandleNavigationAction(actionToHandle));
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

        var target = ResolveAutoFocusTarget(GetNavigationRoot(), preferWindowDefault);
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

        var target = ResolveAutoFocusTarget(window, true);
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

        var defaultFocus = window.DefaultFocusElement;
        var lastFocused = Desktop.State.WindowFocusHistory.TryGetValue(window, out var previousFocus) ? previousFocus : null;
        var firstFocusable = GetFocusableElements(window).FirstOrDefault();

        defaultFocus = IsNavigationTarget(defaultFocus) && window.IsSelfOrAncestorOf(defaultFocus) ? defaultFocus : null;
        lastFocused = IsNavigationTarget(lastFocused) && window.IsSelfOrAncestorOf(lastFocused) ? lastFocused : null;

        return MGDesktop.ResolveAutoFocusTarget(defaultFocus, lastFocused, firstFocusable, preferWindowDefault);
    }

    private MGElement GetHoveredNavigationTarget()
    {
        foreach (var window in Desktop.Windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost))
        {
            var hoveredTarget = GetNearestNavigationTarget(window.HoveredElement);
            if (hoveredTarget != null)
            {
                return hoveredTarget;
            }
        }

        return null;
    }

    private MGElement GetNavigationRoot()
    {
        var activeScopeRoot = GetActiveFocusScopeRoot(FocusScopes.Select(x => x.ScopeRoot).ToList());
        MGElement focusedWindowRoot = Desktop.FocusedKeyboardHandler?.SelfOrParentWindow;
        MGElement hoveredWindowRoot = GetHoveredNavigationTarget()?.SelfOrParentWindow;
        MGElement topWindowRoot = Desktop.Windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost).FirstOrDefault();

        return ResolveNavigationRoot(activeScopeRoot, focusedWindowRoot, hoveredWindowRoot, topWindowRoot);
    }

    private MGElement GetNearestNavigationTarget(MGElement element)
    {
        for (var current = element; current != null; current = current.Parent)
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

    /// <summary>True when <paramref name="key"/> is a key that the currently focused <see cref="ITextEntryHost"/>
    /// reserves for text editing (per <see cref="ITextEntryHost.ShouldPreserveTextEntryKey(Keys)"/>) and must
    /// therefore never be reinterpreted as UI navigation, on either the raw or the semantic input path.</summary>
    private bool ShouldPreserveTextEntryKey(Keys? key)
    {
        if (key is not Keys keyValue)
        {
            return false;
        }

        var focusedElement = Desktop.CanElementReceiveKeyboardInput(Desktop.FocusedKeyboardHandler)
            ? Desktop.FocusedKeyboardHandler
            : null;
        return focusedElement is ITextEntryHost focusedTextEntryHost && focusedTextEntryHost.ShouldPreserveTextEntryKey(keyValue);
    }

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

        var current = element;
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