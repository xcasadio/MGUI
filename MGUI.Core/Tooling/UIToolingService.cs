using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
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

            MGResources effectiveResourceScope = element.GetResources();

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
                effectiveResourceScope.Scope,
                FindResourceScopeOwnerDiagnosticId(element, effectiveResourceScope),
                element.LocalResources != null,
                depth,
                children);
        }

        /// <summary>Finds the stable diagnostic id of the element (or desktop) that owns the <see cref="MGResources"/> instance an
        /// element's <see cref="MGElement.GetResources"/> effectively resolves against. Walks the same chain as
        /// <c>MGElement.GetInheritedResources()</c> (<see cref="MGElement.Parent"/>, else <see cref="MGElement.ParentWindow"/>, which for a
        /// nested window is its owner window), looking for the element whose <see cref="MGElement.LocalResources"/> is the same instance as
        /// <paramref name="effectiveScope"/>; falls back to the desktop scope. Returns null when no owner can be found in that chain (for
        /// example a scope injected by hand, or a <see cref="UIResourceScope.Template"/> scope owned by nothing walkable from this element).</summary>
        private static string FindResourceScopeOwnerDiagnosticId(MGElement element, MGResources effectiveScope)
        {
            for (MGElement current = element; current != null; current = current.Parent ?? current.ParentWindow)
            {
                if (ReferenceEquals(current.LocalResources, effectiveScope))
                {
                    return GetStableDiagnosticId(current);
                }
            }

            MGDesktop desktop = element.GetDesktop();
            if (desktop != null && ReferenceEquals(desktop.Resources, effectiveScope))
            {
                return GetStableDiagnosticId(desktop);
            }

            return null;
        }

        /// <summary>The property paths covered by <see cref="TryGetResolvedValueSource"/>: the pilot properties of the resolved value store
        /// (ADR-0005), as CLR paths rooted at the element, followed by the XAML property names that target them. <c>BorderBrush</c> and
        /// <c>BorderThickness</c> resolve on the element's border (<see cref="MGElement.GetBorder"/>), and the <c>Foreground</c> paths only on
        /// an <see cref="MGTextBlock"/>. Every other property (for example <c>Opacity</c>, <c>PreferredWidth</c> or <c>Visibility</c>) is an
        /// ordinary C# property whose origin is not tracked, so it is not covered.</summary>
        public static IReadOnlyList<string> ResolvedValueSourcePropertyPaths { get; } = new[]
        {
            "Margin",
            "Padding",
            "MinHeight",
            "BorderBrush",
            "BorderThickness",
            "BackgroundBrush",
            "BackgroundBrush.NormalValue",
            "BackgroundBrush.SelectedValue",
            "BackgroundBrush.DisabledValue",
            "BackgroundBrush.FocusedValue",
            "BackgroundBrush.FocusedColor",
            "DefaultTextForeground",
            "DefaultTextForeground.NormalValue",
            "DefaultTextForeground.SelectedValue",
            "DefaultTextForeground.DisabledValue",
            "DefaultTextForeground.FocusedValue",
            "Foreground.NormalValue",
            "Foreground.SelectedValue",
            "Foreground.DisabledValue",
            "Foreground.FocusedValue",
            "Background",
            "SelectedBackground",
            "DisabledBackground",
            "TextForeground",
            "SelectedTextForeground",
            "DisabledTextForeground",
            "Foreground",
        };

        /// <summary>Answers "where does this value come from" for <paramref name="propertyPath"/> on <paramref name="element"/>: the
        /// <see cref="UIValueResolutionSource"/> (kind, precedence, invalidation and name) of the value currently in effect, as resolved by the
        /// element's resolved value store (ADR-0005), read-time fall-backs included (for example an <see cref="MGTextBlock"/> foreground
        /// inherited from an ancestor). <paramref name="propertyPath"/> is one of <see cref="ResolvedValueSourcePropertyPaths"/>.<para/>
        /// Returns false, with <paramref name="source"/> left <c>default</c>, for a null element, a path outside that subset (non-pilot properties
        /// are not covered), a path the element cannot resolve (no border, <c>Foreground</c> on an element that is not a text block) or a value
        /// that no source has written. A pure diagnostic read: it never throws and costs nothing outside the call.</summary>
        public static bool TryGetResolvedValueSource(MGElement element, string propertyPath, out UIValueResolutionSource source)
        {
            source = default;
            if (element == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return false;
            }

            string clrPath = MGUI.Core.UI.XAML.Element.MapBindingTargetPath(propertyPath.Trim());
            return UIPilotPropertyResolver.TryResolve(element, clrPath, out MGElement owner, out UIPilotProperty pilot, out UIValueSlot slot)
                && owner.TryGetResolvedValueSource(pilot, slot, out source);
        }

        /// <summary>Captures the per-element debug view of <paramref name="element"/>: its visual state, effective resource scope, applied control
        /// template and registered parts, and the origin of its main visual values (background, text foreground, border brush, border thickness and
        /// padding), each with its winning source, effective value and recorded contributions. A single call therefore answers "why does this
        /// border have a thickness of 1" for the properties covered by <see cref="ResolvedValueSourcePropertyPaths"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="element"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="element"/> is not attached to a window.</exception>
        public static UIElementDebugView CaptureElementDebugView(MGElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            string diagnosticId = GetStableDiagnosticId(element);

            Dictionary<string, string> templateParts = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, MGElement> templatePart in element.TemplateParts)
            {
                templateParts[templatePart.Key] = templatePart.Value?.GetType().Name ?? nameof(MGElement);
            }

            VisualState visualState = element.VisualState;
            MGResources effectiveResourceScope = element.GetResources();
            UIValueOriginView[] valueOrigins =
            {
                CaptureValueOrigin(element, "Background"),
                CaptureValueOrigin(element, element is MGTextBlock ? "Foreground" : "TextForeground"),
                CaptureValueOrigin(element, "BorderBrush"),
                CaptureValueOrigin(element, "BorderThickness"),
                CaptureValueOrigin(element, "Padding"),
            };

            return new(
                diagnosticId,
                element.Name,
                element.ElementType,
                visualState.Primary,
                visualState.Secondary,
                effectiveResourceScope.Scope,
                FindResourceScopeOwnerDiagnosticId(element, effectiveResourceScope),
                element.LocalResources != null,
                element.AppliedControlTemplateName,
                templateParts,
                element.LastControlTemplateError,
                valueOrigins);
        }

        /// <summary>Renders <paramref name="view"/> as a readable text artifact: one line per section, then one line per value origin
        /// (<c>path = effective value &lt;- winning source</c>) followed by one line per recorded contribution.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> is null.</exception>
        public static string RenderElementDebugView(UIElementDebugView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            StringBuilder artifact = new();
            artifact.AppendLine($"element: {view.DiagnosticId} [{view.ElementType}] name={view.Name ?? "<none>"}");
            artifact.AppendLine($"visual-state: primary={view.PrimaryVisualState} secondary={view.SecondaryVisualState}");
            artifact.AppendLine($"resource-scope: scope={view.ResourceScope} scopeOwner={view.ResourceScopeOwnerDiagnosticId ?? "<none>"} localScope={view.HasLocalResourceScope}");
            artifact.AppendLine($"template: {view.AppliedControlTemplate ?? "<none>"} error={view.LastControlTemplateError ?? "<none>"}");

            List<string> templateParts = new();
            foreach (KeyValuePair<string, string> templatePart in view.TemplateParts)
            {
                templateParts.Add($"{templatePart.Key}={templatePart.Value}");
            }
            templateParts.Sort(StringComparer.Ordinal);
            artifact.Append("template-parts: ");
            AppendDelimited(artifact, templateParts);
            artifact.AppendLine();

            artifact.AppendLine("values:");
            foreach (UIValueOriginView origin in view.ValueOrigins)
            {
                AppendIndent(artifact, 1);
                artifact.AppendLine($"{origin.PropertyPath} = {origin.EffectiveValue ?? "<none>"} <- {(origin.IsResolved ? FormatValueSource(origin.Source) : "<not resolved>")}");
                foreach (UIResolvedContribution contribution in origin.Contributions)
                {
                    AppendIndent(artifact, 2);
                    artifact.AppendLine($"{FormatValueSource(contribution.Source)} = {contribution.Value?.ToString() ?? "<null>"}");
                }
            }

            return artifact.ToString();
        }

        private static UIValueOriginView CaptureValueOrigin(MGElement element, string propertyPath)
        {
            bool isResolved = TryGetResolvedValueSource(element, propertyPath, out UIValueResolutionSource source);

            IReadOnlyList<UIResolvedContribution> contributions = Array.Empty<UIResolvedContribution>();
            string clrPath = MGUI.Core.UI.XAML.Element.MapBindingTargetPath(propertyPath);
            if (UIPilotPropertyResolver.TryResolve(element, clrPath, out MGElement owner, out UIPilotProperty pilot, out UIValueSlot slot))
            {
                contributions = owner.EnumerateResolvedContributions(pilot, slot);
            }

            return new(propertyPath, isResolved, source, DescribeEffectiveValue(element, propertyPath), contributions);
        }

        private static string DescribeEffectiveValue(MGElement element, string propertyPath)
        {
            MGBorder border = element as MGBorder ?? element.GetBorder();
            object value = propertyPath switch
            {
                "Background" => element.BackgroundBrush?.NormalValue,
                "Foreground" when element is MGTextBlock textBlock => textBlock.ActualForeground,
                "TextForeground" => element.DerivedDefaultTextForeground,
                "BorderBrush" => border?.BorderBrush,
                "BorderThickness" => border?.BorderThickness,
                "Padding" => element.Padding,
                _ => null,
            };
            return value?.ToString();
        }

        private static string FormatValueSource(UIValueResolutionSource source)
            => string.IsNullOrEmpty(source.Name)
                ? $"{source.Kind}({(int)source.Precedence}) invalidation={source.Invalidation}"
                : $"{source.Kind}({(int)source.Precedence}) '{source.Name}' invalidation={source.Invalidation}";

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
            artifact.AppendLine($"{snapshot.DiagnosticId} [{snapshot.ElementType}] visible={snapshot.IsEffectivelyVisible} visibility={snapshot.Visibility} hitTest={snapshot.IsHitTestVisible} mouse={snapshot.CanReceiveMouseInput} keyboard={snapshot.CanReceiveKeyboardInput} focus={snapshot.HasKeyboardFocus} hover={snapshot.IsHovered} clipped={snapshot.RecentDrawWasClipped} primary={snapshot.PrimaryVisualState} secondary={snapshot.SecondaryVisualState} scope={snapshot.ResourceScope} scopeOwner={snapshot.ResourceScopeOwnerDiagnosticId ?? "<none>"} localScope={snapshot.HasLocalResourceScope}");

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