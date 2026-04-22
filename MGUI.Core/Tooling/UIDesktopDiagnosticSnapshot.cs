using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MGUI.Core.Tooling
{
    public record UIDesktopDiagnosticSnapshot(
        string DesktopDiagnosticId,
        TimeSpan TotalElapsed,
        TimeSpan FrameElapsed,
        UIInputDiagnosticSnapshot Input,
        string FocusedElementDiagnosticId,
        string ActiveToolTipDiagnosticId,
        string ActiveContextMenuDiagnosticId,
        string ActiveOverlayHostDiagnosticId,
        string ActiveOverlayDiagnosticId,
        IReadOnlyList<string> OpenOverlayDiagnosticIds,
        UIWindowDiagnosticSnapshot OverlayWindow,
        IReadOnlyList<UIWindowDiagnosticSnapshot> Windows);

    public record UIWindowDiagnosticSnapshot(
        string DiagnosticId,
        string RuntimeUniqueId,
        string TitleText,
        bool IsOverlayWindow,
        bool IsTopmost,
        bool AllowsClickThrough,
        bool HasModalWindow,
        string HoveredElementDiagnosticId,
        string PressedElementDiagnosticId,
        UIVisualTreeSnapshot VisualTree,
        IReadOnlyList<UIWindowDiagnosticSnapshot> NestedWindows,
        IReadOnlyList<UIWindowDiagnosticSnapshot> ModalWindows);

    public record UIInputDiagnosticSnapshot(
        Point MousePosition,
        Point PreviousMousePosition,
        int ScrollWheelValue,
        bool MouseMovedRecently,
        bool MouseLeftPressedRecently,
        bool MouseLeftReleasedRecently,
        bool IsShiftDown,
        bool IsControlDown,
        bool IsAltDown,
        bool HasGamePadActivity,
        IReadOnlyList<string> PressedKeys,
        IReadOnlyList<string> TriggeredGamePadButtons);
}