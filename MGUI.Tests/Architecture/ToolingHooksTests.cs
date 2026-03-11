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
        Assert.NotNull(typeof(UIToolingService).GetMethod(nameof(UIToolingService.LoadPreview), BindingFlags.Static | BindingFlags.Public));
    }

    [Fact]
    public void Designer_UsesSharedToolingPreviewHook()
    {
        string designerSource = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGXAMLDesigner.cs");

        Assert.Contains("UIToolingService.LoadPreview", designerSource);
    }
}