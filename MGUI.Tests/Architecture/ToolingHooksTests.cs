using MGUI.Core.Tooling;
using MGUI.Core.UI;
using System.Reflection;

namespace MGUI.Tests.Architecture;

public class ToolingHooksTests
{
    [Fact]
    public void MGElement_ExposesVisualTreeEnumerationHook()
    {
        Assert.NotNull(typeof(MGElement).GetMethod(nameof(MGElement.EnumerateVisualTree), BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void UIToolingService_ExposesSnapshotAndPreviewHooks()
    {
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.CaptureVisualTree), BindingFlags.Static | BindingFlags.Public));
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.CaptureDesktopSnapshot), BindingFlags.Static | BindingFlags.Public));
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.GetStableDiagnosticId), BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(MGDesktop) }, null));
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.GetStableDiagnosticId), BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(MGElement) }, null));
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.LoadPreview), BindingFlags.Static | BindingFlags.Public));
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.RenderDesktopSnapshot), BindingFlags.Static | BindingFlags.Public));
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.ReplayFrames), BindingFlags.Static | BindingFlags.Public));
        Assert.NotNull(typeof(UIDesktopDiagnosticSnapshot).GetProperty(nameof(UIDesktopDiagnosticSnapshot.ActiveOverlayDiagnosticId)));
        Assert.NotNull(typeof(UIDesktopDiagnosticSnapshot).GetProperty(nameof(UIDesktopDiagnosticSnapshot.FocusedElementDiagnosticId)));
        Assert.NotNull(typeof(UIVisualTreeSnapshot).GetProperty(nameof(UIVisualTreeSnapshot.DiagnosticId)));
        Assert.NotNull(typeof(UIVisualTreeSnapshot).GetProperty(nameof(UIVisualTreeSnapshot.WindowDiagnosticId)));
        Assert.NotNull(typeof(UIVisualTreeSnapshot).GetProperty(nameof(UIVisualTreeSnapshot.RuntimeUniqueId)));
        Assert.NotNull(typeof(UIVisualTreeSnapshot).GetProperty(nameof(UIVisualTreeSnapshot.HasKeyboardFocus)));
        Assert.NotNull(typeof(UIVisualTreeSnapshot).GetProperty(nameof(UIVisualTreeSnapshot.CanReceiveMouseInput)));
        Assert.NotNull(typeof(UIVisualTreeSnapshot).GetProperty(nameof(UIVisualTreeSnapshot.AppliedControlTemplate)));
        Assert.NotNull(typeof(UIVisualTreeSnapshot).GetProperty(nameof(UIVisualTreeSnapshot.TemplateParts)));
        Assert.NotNull(typeof(UIInputReplayFrame).GetProperty(nameof(UIInputReplayFrame.ExpectedFocusedElementDiagnosticId)));
        Assert.NotNull(typeof(UIDiagnosticAssertions).GetMethod(nameof(UIDiagnosticAssertions.ExpectFocusedElement), BindingFlags.Static | BindingFlags.Public));
    }

    [Fact]
    public void Designer_UsesSharedToolingPreviewHook()
    {
        string designerSource = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGXAMLDesigner.cs");

        Assert.Contains("UIToolingService.LoadPreview", designerSource);
    }
}