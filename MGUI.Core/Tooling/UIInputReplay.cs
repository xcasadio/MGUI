using MGUI.Shared.Input.Semantic;
using MGUI.Shared.Rendering;

namespace MGUI.Core.Tooling;

public record UIInputReplayFrame(
    string Name,
    UpdateBaseArgs UpdateArgs,
    IReadOnlyList<InputActionEvent> Actions,
    string ExpectedFocusedElementDiagnosticId = null,
    string ExpectedActiveOverlayDiagnosticId = null);

public record UIInputReplayStepResult(
    string Name,
    UIDesktopDiagnosticSnapshot Snapshot,
    string Artifact);

public record UIInputReplayResult(
    IReadOnlyList<UIInputReplayStepResult> Steps);