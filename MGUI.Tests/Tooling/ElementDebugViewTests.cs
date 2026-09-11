using System;
using System.Linq;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Tooling;

/// <summary>
/// Backlog task 6 (styling-theme-tasks.md): the per-element debug view, <see cref="UIToolingService.CaptureElementDebugView"/> and
/// <see cref="UIToolingService.RenderElementDebugView"/>, built on the resource scope capture of task 2 and the resolved value source
/// diagnostic of task 5.
/// </summary>
public class ElementDebugViewTests
{
    [Fact]
    public void A_Templated_Window_View_Reports_Its_Template_Scope_And_Theme_Padding()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 20, 30, 300, 200) { Name = "Inspector" };
        harness.Desktop.Windows.Add(window);
        harness.Frame(1, Point.Zero);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(window);

        Assert.Equal(UIToolingService.GetStableDiagnosticId(window), view.DiagnosticId);
        Assert.Equal("Inspector", view.Name);
        Assert.Equal(MGElementType.Window, view.ElementType);
        Assert.Equal(window.VisualState.Primary, view.PrimaryVisualState);
        Assert.Equal(window.AppliedControlTemplateName, view.AppliedControlTemplate);
        Assert.Contains(MGWindow.TitleBarPartName, view.TemplateParts.Keys);
        Assert.Equal(UIResourceScope.Window, view.ResourceScope);
        Assert.Equal(view.DiagnosticId, view.ResourceScopeOwnerDiagnosticId);
        Assert.True(view.HasLocalResourceScope);

        UIValueOriginView padding = Origin(view, "Padding");
        Assert.True(padding.IsResolved);
        Assert.Equal(UIValueSourceKind.Theme, padding.Source.Kind);
        Assert.Equal("Window.Padding", padding.Source.Name);
        Assert.Equal(window.Padding.ToString(), padding.EffectiveValue);
        Assert.Equal(UIValueSourceKind.Theme, padding.Contributions[0].Kind);
    }

    [Fact]
    public void A_Templated_ComboBox_View_Reports_Its_Parts_And_Theme_Chrome()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = new(harness.Window);
        harness.Show(comboBox);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(comboBox);

        Assert.Equal(MGControlTemplateCatalog.ComboBoxTemplateName, view.AppliedControlTemplate);
        Assert.Contains(MGComboBox<object>.DropdownWindowPartName, view.TemplateParts.Keys);
        Assert.False(view.HasLocalResourceScope);
        Assert.Equal(UIToolingService.GetStableDiagnosticId(harness.Window), view.ResourceScopeOwnerDiagnosticId);
        Assert.Equal(new[] { "Background", "TextForeground", "BorderBrush", "BorderThickness", "Padding" }, view.ValueOrigins.Select(x => x.PropertyPath));

        UIValueOriginView borderBrush = Origin(view, "BorderBrush");
        Assert.True(borderBrush.IsResolved);
        Assert.Equal(UIValueSourceKind.Theme, borderBrush.Source.Kind);
        Assert.Equal("ComboBox.BorderBrush", borderBrush.Source.Name);
    }

    [Fact]
    public void One_Call_Explains_Why_A_Border_Thickness_Is_1()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window) { Name = "Preview Pane" };
        border.BorderThickness = new Thickness(1);
        harness.Show(border);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(border);

        UIValueOriginView thickness = Origin(view, "BorderThickness");
        Assert.True(thickness.IsResolved);
        Assert.Equal(UIValueSourceKind.LocalValue, thickness.Source.Kind);
        Assert.Equal(new Thickness(1).ToString(), thickness.EffectiveValue);
        Assert.Equal(UIValueSourceKind.LocalValue, thickness.Contributions[0].Kind);
        Assert.Equal(new Thickness(1), thickness.Contributions[0].Value);
        Assert.Contains(thickness.Contributions, contribution => contribution.Kind == UIValueSourceKind.DefaultValue);

        string artifact = UIToolingService.RenderElementDebugView(view);
        Assert.Contains($"element: {view.DiagnosticId} [{MGElementType.Border}] name=Preview Pane", artifact);
        Assert.Contains($"BorderThickness = {new Thickness(1)} <- LocalValue(90)", artifact);
        Assert.Contains("DefaultValue(0)", artifact);
    }

    [Fact]
    public void A_Text_Block_View_Reports_Its_Inherited_Foreground_And_No_Border()
    {
        Harness harness = Harness.Create();
        MGBorder parent = new(harness.Window);
        MGTextBlock textBlock = new(harness.Window, "x");
        parent.SetContent(textBlock);
        parent.DefaultTextForeground.NormalValue = Color.Red;
        harness.Show(parent);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(textBlock);

        UIValueOriginView foreground = Origin(view, "Foreground");
        Assert.True(foreground.IsResolved);
        Assert.Equal(UIValueSourceKind.Inherited, foreground.Source.Kind);
        Assert.Equal(Color.Red.ToString(), foreground.EffectiveValue);

        UIValueOriginView borderBrush = Origin(view, "BorderBrush");
        Assert.False(borderBrush.IsResolved);
        Assert.Null(borderBrush.EffectiveValue);
        Assert.Empty(borderBrush.Contributions);
        Assert.Contains("BorderBrush = <none> <- <not resolved>", UIToolingService.RenderElementDebugView(view));
    }

    [Fact]
    public void Null_Arguments_Are_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => UIToolingService.CaptureElementDebugView(null));
        Assert.Throws<ArgumentNullException>(() => UIToolingService.RenderElementDebugView(null));
    }

    private static UIValueOriginView Origin(UIElementDebugView view, string propertyPath)
        => Assert.Single(view.ValueOrigins, origin => origin.PropertyPath == propertyPath);

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0),
            };
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0, Point.Zero);
            return harness;
        }

        /// <summary>Adds the element to the window, shows the window and runs two warm-up frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            if (!Desktop.Windows.Contains(Window))
                Desktop.Windows.Add(Window);
            Frame(1, Point.Zero);
            Frame(2, Point.Zero);
        }

        public void Frame(int frameIndex, Point mousePosition)
        {
            MouseState mouse = new(mousePosition.X, mousePosition.Y, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
