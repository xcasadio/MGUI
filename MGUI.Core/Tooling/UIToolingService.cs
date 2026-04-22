using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Input;
using MGUI.Shared.Input.GamePad;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Input.Semantic;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace MGUI.Core.Tooling
{
    public static class UIToolingService
    {
        /// <summary>Returns the stable desktop root segment used by diagnostic paths.</summary>
        public static string GetStableDiagnosticId(MGDesktop desktop)
        {
            if (desktop == null)
            {
                throw new ArgumentNullException(nameof(desktop));
            }

            return "desktop";
        }

        /// <summary>Returns a stable path-based identifier derived from window ancestry, element names, template-part names, and sibling order.</summary>
        public static string GetStableDiagnosticId(MGElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            List<string> segments = new();
            AppendStableDiagnosticSegments(element, segments);

            StringBuilder result = new();
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                {
                    result.Append('/');
                }

                result.Append(segments[i]);
            }

            return result.ToString();
        }

        /// <summary>Captures a desktop-level diagnostic snapshot including input state, active focus/overlay/menu state, and window trees.</summary>
        public static UIDesktopDiagnosticSnapshot CaptureDesktopSnapshot(MGDesktop desktop)
        {
            if (desktop == null)
            {
                throw new ArgumentNullException(nameof(desktop));
            }

            MGWindow overlayWindow = desktop.OverlayHost?.SelfOrParentWindow ?? throw new InvalidOperationException(
                $"Unable to capture desktop diagnostics because the {nameof(MGDesktop)} overlay window is not available.");

            List<UIWindowDiagnosticSnapshot> windows = new();
            for (int i = 0; i < desktop.Windows.Count; i++)
            {
                windows.Add(CreateWindowSnapshot(desktop.Windows[i], false));
            }

            List<string> openOverlayIds = new();
            IReadOnlyList<MGOverlay> openOverlays = desktop.OverlayHost.OpenOverlays;
            for (int i = 0; i < openOverlays.Count; i++)
            {
                openOverlayIds.Add(GetStableDiagnosticId(openOverlays[i]));
            }

            return new(
                GetStableDiagnosticId(desktop),
                desktop.Runtime.UpdateArgs.TotalElapsed,
                desktop.Runtime.UpdateArgs.FrameElapsed,
                CaptureInputSnapshot(desktop.InputTracker),
                TryGetStableDiagnosticId(desktop.FocusedKeyboardHandler),
                TryGetStableDiagnosticId(desktop.ActiveToolTip),
                TryGetStableDiagnosticId(desktop.ActiveContextMenu),
                GetStableDiagnosticId(desktop.OverlayHost),
                TryGetStableDiagnosticId(desktop.OverlayHost.ActiveOverlay),
                openOverlayIds,
                CreateWindowSnapshot(overlayWindow, true),
                windows);
        }

        /// <summary>Renders a readable diagnostic artifact for a desktop snapshot.</summary>
        public static string RenderDesktopSnapshot(UIDesktopDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            StringBuilder artifact = new();
            artifact.AppendLine($"desktop: {snapshot.DesktopDiagnosticId}");
            artifact.AppendLine($"time: total={snapshot.TotalElapsed:c} frame={snapshot.FrameElapsed:c}");
            artifact.AppendLine($"focus: {snapshot.FocusedElementDiagnosticId ?? "<none>"}");
            artifact.AppendLine($"tooltip: {snapshot.ActiveToolTipDiagnosticId ?? "<none>"}");
            artifact.AppendLine($"context-menu: {snapshot.ActiveContextMenuDiagnosticId ?? "<none>"}");
            artifact.AppendLine($"overlay-host: {snapshot.ActiveOverlayHostDiagnosticId ?? "<none>"}");
            artifact.AppendLine($"active-overlay: {snapshot.ActiveOverlayDiagnosticId ?? "<none>"}");
            artifact.Append("open-overlays: ");
            AppendDelimited(artifact, snapshot.OpenOverlayDiagnosticIds);
            artifact.AppendLine();
            artifact.AppendLine($"input: mouse={snapshot.Input.MousePosition} prev={snapshot.Input.PreviousMousePosition} wheel={snapshot.Input.ScrollWheelValue} moved={snapshot.Input.MouseMovedRecently} lmbPressed={snapshot.Input.MouseLeftPressedRecently} lmbReleased={snapshot.Input.MouseLeftReleasedRecently} shift={snapshot.Input.IsShiftDown} ctrl={snapshot.Input.IsControlDown} alt={snapshot.Input.IsAltDown} gamePadActive={snapshot.Input.HasGamePadActivity}");
            artifact.Append("pressed-keys: ");
            AppendDelimited(artifact, snapshot.Input.PressedKeys);
            artifact.AppendLine();
            artifact.Append("triggered-gamepad-buttons: ");
            AppendDelimited(artifact, snapshot.Input.TriggeredGamePadButtons);
            artifact.AppendLine();

            artifact.AppendLine("overlay-window:");
            AppendWindowSnapshot(artifact, snapshot.OverlayWindow, 1);

            artifact.AppendLine("windows:");
            for (int i = 0; i < snapshot.Windows.Count; i++)
            {
                AppendWindowSnapshot(artifact, snapshot.Windows[i], 1);
            }

            return artifact.ToString();
        }

        /// <summary>Replays a bounded input sequence against a desktop, capturing an artifact after each frame.</summary>
        public static UIInputReplayResult ReplayFrames(MGDesktop desktop, IReadOnlyList<UIInputReplayFrame> frames, Action<UpdateBaseArgs> setRuntimeUpdateArgs)
        {
            if (desktop == null)
            {
                throw new ArgumentNullException(nameof(desktop));
            }

            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (setRuntimeUpdateArgs == null)
            {
                throw new ArgumentNullException(nameof(setRuntimeUpdateArgs));
            }

            List<UIInputReplayStepResult> results = new(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                UIInputReplayFrame frame = frames[i] ?? throw new ArgumentException("Replay frame entries cannot be null.", nameof(frames));
                setRuntimeUpdateArgs(frame.UpdateArgs);
                desktop.Runtime.Input.Update(frame.UpdateArgs);
                desktop.Update();

                IReadOnlyList<InputActionEvent> actions = frame.Actions ?? Array.Empty<InputActionEvent>();
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    desktop.TryHandleInputAction(actions[actionIndex]);
                }

                UIDesktopDiagnosticSnapshot snapshot = CaptureDesktopSnapshot(desktop);
                string artifact = RenderDesktopSnapshot(snapshot);

                if (!string.IsNullOrWhiteSpace(frame.ExpectedFocusedElementDiagnosticId))
                {
                    UIDiagnosticAssertions.ExpectFocusedElement(snapshot, frame.ExpectedFocusedElementDiagnosticId, artifact, frame.Name);
                }

                if (!string.IsNullOrWhiteSpace(frame.ExpectedActiveOverlayDiagnosticId))
                {
                    UIDiagnosticAssertions.ExpectActiveOverlay(snapshot, frame.ExpectedActiveOverlayDiagnosticId, artifact, frame.Name);
                }

                results.Add(new(frame.Name, snapshot, artifact));
            }

            return new(results);
        }

        public static UIVisualTreeSnapshot CaptureVisualTree(MGElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            return CreateSnapshot(root, 0);
        }

        private static UIVisualTreeSnapshot CreateSnapshot(MGElement element, int depth)
        {
            MGWindow window = element.SelfOrParentWindow ?? throw new InvalidOperationException(
                $"Unable to capture a visual tree snapshot for an element that is not attached to a {nameof(MGWindow)}.");
            VisualState visualState = element.VisualState;

            List<UIVisualTreeSnapshot> children = new();
            IReadOnlyList<MGElement> visualChildren = element.GetVisualTreeChildren(true, true);
            for (int i = 0; i < visualChildren.Count; i++)
            {
                children.Add(CreateSnapshot(visualChildren[i], depth + 1));
            }

            Dictionary<string, string> templateParts = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MGElement> templatePart in element.TemplateParts)
            {
                templateParts[templatePart.Key] = templatePart.Value?.GetType().Name ?? nameof(MGElement);
            }

            return new(
                GetStableDiagnosticId(element),
                GetStableDiagnosticId(window),
                element.UniqueId,
                element.Visibility,
                element.Visibility == Visibility.Visible && !element.RecentDrawWasClipped,
                element.IsHitTestVisible,
                ((IMouseHandlerHost)element).CanReceiveMouseInput(),
                ((IKeyboardHandlerHost)element).CanReceiveKeyboardInput(),
                ((IKeyboardHandlerHost)element).HasKeyboardFocus(),
                element.IsHovered,
                visualState.Primary,
                visualState.Secondary,
                element.ClipToBounds,
                element.RecentDrawWasClipped,
                element.Name,
                element.ElementType,
                element.LayoutBounds,
                element.ActualLayoutBounds,
                element.AppliedControlTemplateName,
                templateParts,
                element.LastControlTemplateError,
                depth,
                children);
        }

        public static MGElement LoadPreview(MGWindow window, XamlDocumentSource source, object dataContext = null,
            bool sanitizeXamlString = false, bool replaceLinebreakLiterals = true)
        {
            return XAMLParser.LoadPreview(window, source, dataContext, sanitizeXamlString, replaceLinebreakLiterals);
        }

        public static MGElement LoadPreview(MGWindow window, XamlDocumentSource source, object dataContext,
            XamlLoaderMode mode, bool sanitizeXamlString = false, bool replaceLinebreakLiterals = true)
        {
            return XAMLParser.LoadPreview(window, source, dataContext, mode, sanitizeXamlString, replaceLinebreakLiterals);
        }

        private static UIWindowDiagnosticSnapshot CreateWindowSnapshot(MGWindow window, bool isOverlayWindow)
        {
            List<UIWindowDiagnosticSnapshot> nestedWindows = new();
            IReadOnlyList<MGWindow> nestedChildren = window.NestedWindows;
            for (int i = 0; i < nestedChildren.Count; i++)
            {
                nestedWindows.Add(CreateWindowSnapshot(nestedChildren[i], false));
            }

            List<UIWindowDiagnosticSnapshot> modalWindows = new();
            IReadOnlyList<MGWindow> modalChildren = window.ModalWindows;
            for (int i = 0; i < modalChildren.Count; i++)
            {
                modalWindows.Add(CreateWindowSnapshot(modalChildren[i], false));
            }

            return new(
                GetStableDiagnosticId(window),
                window.UniqueId,
                window.TitleText,
                isOverlayWindow,
                window.IsTopmost,
                window.AllowsClickThrough,
                window.HasModalWindow,
                TryGetStableDiagnosticId(window.HoveredElement),
                TryGetStableDiagnosticId(window.PressedElement),
                CaptureVisualTree(window),
                nestedWindows,
                modalWindows);
        }

        private static UIInputDiagnosticSnapshot CaptureInputSnapshot(InputTracker inputTracker)
        {
            List<string> pressedKeys = new();
            Keys[] currentKeys = inputTracker.Keyboard.CurrentState.GetPressedKeys();
            for (int i = 0; i < currentKeys.Length; i++)
            {
                pressedKeys.Add(currentKeys[i].ToString());
            }

            List<string> triggeredButtons = new();
            foreach (KeyValuePair<GamePadButton, bool> triggeredButton in inputTracker.GamePad.CurrentTriggeredButtons)
            {
                if (triggeredButton.Value)
                {
                    triggeredButtons.Add(triggeredButton.Key.ToString());
                }
            }

            return new(
                inputTracker.Mouse.CurrentState.Position,
                inputTracker.Mouse.PreviousState.Position,
                inputTracker.Mouse.CurrentState.ScrollWheelValue,
                inputTracker.Mouse.MouseMovedRecently,
                inputTracker.Mouse.MouseLeftButtonPressedRecently,
                inputTracker.Mouse.MouseLeftButtonReleasedRecently,
                inputTracker.Keyboard.IsShiftDown,
                inputTracker.Keyboard.IsControlDown,
                inputTracker.Keyboard.IsAltDown,
                inputTracker.GamePad.HasActivity(),
                pressedKeys,
                triggeredButtons);
        }

        private static string TryGetStableDiagnosticId(MGElement element)
            => element == null ? null : GetStableDiagnosticId(element);

        private static void AppendDelimited(StringBuilder artifact, IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                artifact.Append("<none>");
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    artifact.Append(", ");
                }

                artifact.Append(values[i]);
            }
        }

        private static void AppendWindowSnapshot(StringBuilder artifact, UIWindowDiagnosticSnapshot window, int indent)
        {
            if (window == null)
            {
                AppendIndent(artifact, indent);
                artifact.AppendLine("<none>");
                return;
            }

            AppendIndent(artifact, indent);
            artifact.AppendLine($"window: {window.DiagnosticId} title={window.TitleText ?? "<none>"} overlay={window.IsOverlayWindow} topmost={window.IsTopmost} clickThrough={window.AllowsClickThrough} hasModal={window.HasModalWindow}");
            AppendIndent(artifact, indent + 1);
            artifact.AppendLine($"hovered={window.HoveredElementDiagnosticId ?? "<none>"} pressed={window.PressedElementDiagnosticId ?? "<none>"}");
            AppendIndent(artifact, indent + 1);
            artifact.AppendLine("visual-tree:");
            AppendVisualTreeSnapshot(artifact, window.VisualTree, indent + 2);

            if (window.ModalWindows.Count > 0)
            {
                AppendIndent(artifact, indent + 1);
                artifact.AppendLine("modal-windows:");
                for (int i = 0; i < window.ModalWindows.Count; i++)
                {
                    AppendWindowSnapshot(artifact, window.ModalWindows[i], indent + 2);
                }
            }

            if (window.NestedWindows.Count > 0)
            {
                AppendIndent(artifact, indent + 1);
                artifact.AppendLine("nested-windows:");
                for (int i = 0; i < window.NestedWindows.Count; i++)
                {
                    AppendWindowSnapshot(artifact, window.NestedWindows[i], indent + 2);
                }
            }
        }

        private static void AppendVisualTreeSnapshot(StringBuilder artifact, UIVisualTreeSnapshot snapshot, int indent)
        {
            AppendIndent(artifact, indent);
            artifact.AppendLine($"{snapshot.DiagnosticId} [{snapshot.ElementType}] visible={snapshot.IsEffectivelyVisible} visibility={snapshot.Visibility} hitTest={snapshot.IsHitTestVisible} mouse={snapshot.CanReceiveMouseInput} keyboard={snapshot.CanReceiveKeyboardInput} focus={snapshot.HasKeyboardFocus} hover={snapshot.IsHovered} clipped={snapshot.RecentDrawWasClipped} primary={snapshot.PrimaryVisualState} secondary={snapshot.SecondaryVisualState}");

            for (int i = 0; i < snapshot.Children.Count; i++)
            {
                AppendVisualTreeSnapshot(artifact, snapshot.Children[i], indent + 1);
            }
        }

        private static void AppendIndent(StringBuilder artifact, int indent)
        {
            for (int i = 0; i < indent; i++)
            {
                artifact.Append("  ");
            }
        }

        private static void AppendStableDiagnosticSegments(MGElement element, List<string> segments)
        {
            if (element.Parent != null)
            {
                AppendStableDiagnosticSegments(element.Parent, segments);
                segments.Add(CreateChildSegment(element.Parent, element));
                return;
            }

            if (element is MGWindow nestedWindow && nestedWindow.ParentWindow != null)
            {
                AppendStableDiagnosticSegments(nestedWindow.ParentWindow, segments);
                segments.Add(CreateChildSegment(nestedWindow.ParentWindow, nestedWindow));
                return;
            }

            if (element is MGWindow rootWindow)
            {
                segments.Add(GetStableDiagnosticId(rootWindow.Desktop));
                segments.Add(CreateRootWindowSegment(rootWindow));
                return;
            }

            throw new InvalidOperationException(
                $"Unable to compute a stable diagnostic id for detached element '{element.GetType().Name}'.");
        }

        private static string CreateRootWindowSegment(MGWindow window)
        {
            if (ReferenceEquals(window, window.Desktop.OverlayHost?.SelfOrParentWindow))
            {
                return "overlay-window";
            }

            string typeToken = GetElementTypeToken(window);
            if (!string.IsNullOrWhiteSpace(window.Name))
            {
                return $"{typeToken}:{NormalizeIdentifierToken(window.Name)}";
            }

            int ordinal = GetRootWindowOrdinal(window);
            return ordinal >= 0
                ? $"{typeToken}[{ordinal}]"
                : $"{typeToken}:detached";
        }

        private static string CreateChildSegment(MGElement parent, MGElement child)
        {
            string typeToken = GetElementTypeToken(child);
            if (!string.IsNullOrWhiteSpace(child.Name))
            {
                return $"{typeToken}:{NormalizeIdentifierToken(child.Name)}";
            }

            if (TryGetTemplatePartName(parent, child, out string templatePartName))
            {
                return $"part:{NormalizeIdentifierToken(templatePartName)}";
            }

            return $"{typeToken}[{GetSiblingTypeOrdinal(parent, child)}]";
        }

        private static bool TryGetTemplatePartName(MGElement parent, MGElement child, out string templatePartName)
        {
            foreach (KeyValuePair<string, MGElement> templatePart in parent.TemplateParts)
            {
                if (ReferenceEquals(templatePart.Value, child) && !string.IsNullOrWhiteSpace(templatePart.Key))
                {
                    templatePartName = templatePart.Key;
                    return true;
                }
            }

            templatePartName = null;
            return false;
        }

        private static int GetRootWindowOrdinal(MGWindow window)
        {
            int ordinal = 0;
            for (int i = 0; i < window.Desktop.Windows.Count; i++)
            {
                MGWindow candidate = window.Desktop.Windows[i];
                if (ReferenceEquals(candidate, window))
                {
                    return ordinal;
                }

                if (candidate.ElementType == window.ElementType)
                {
                    ordinal++;
                }
            }

            return -1;
        }

        private static int GetSiblingTypeOrdinal(MGElement parent, MGElement child)
        {
            int ordinal = 0;
            IReadOnlyList<MGElement> siblings = parent.GetVisualTreeChildren(true, true);
            for (int i = 0; i < siblings.Count; i++)
            {
                MGElement sibling = siblings[i];
                if (ReferenceEquals(sibling, child))
                {
                    return ordinal;
                }

                if (sibling.ElementType == child.ElementType)
                {
                    ordinal++;
                }
            }

            return ordinal;
        }

        private static string GetElementTypeToken(MGElement element)
            => NormalizeIdentifierToken(element.ElementType.ToString());

        private static string NormalizeIdentifierToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            StringBuilder token = new(value.Length);
            bool lastWasSeparator = false;
            foreach (char c in value.Trim())
            {
                if (char.IsLetterOrDigit(c))
                {
                    token.Append(char.ToLowerInvariant(c));
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    token.Append('-');
                    lastWasSeparator = true;
                }
            }

            while (token.Length > 0 && token[token.Length - 1] == '-')
            {
                token.Length--;
            }

            return token.Length > 0 ? token.ToString() : "unnamed";
        }
    }
}