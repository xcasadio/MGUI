using System;
using System.ComponentModel;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;
using XamlSetter = MGUI.Core.UI.XAML.Setter;
using XamlStyle = MGUI.Core.UI.XAML.Style;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Backlog task 10 (styling-theme-tasks.md), scenario <c>SCN-THEME-001</c>: <see cref="MGElement.RefreshStyles"/> re-applies the implicit and named styles
/// of the resource scopes to a subtree loaded from XAML without reparsing it, keeps the scoping of the parse, never overwrites a XAML attribute, a local
/// value or a binding, and stays within the refreshed subtree.
/// </summary>
public class StyleRefreshTests
{
    [Fact]
    public void An_Implicit_Style_Added_After_Load_Reaches_The_Existing_Subtree()
    {
        Harness harness = Harness.Create();
        MGWindow window = harness.Load(@"<StackPanel Orientation=""Vertical""><Border Name=""A"" /><TextBlock Name=""T"" Text=""Label"" /></StackPanel>");
        MGBorder a = window.GetElementByName<MGBorder>("A");
        MGTextBlock t = window.GetElementByName<MGTextBlock>("T");
        Thickness loadedPadding = a.Padding;

        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "7"), ("Background", "Red"), ("BorderThickness", "3")));
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.TextBlock, ("Foreground", "Yellow")));
        Assert.Equal(loadedPadding, a.Padding);

        UIStyleRefreshResult result = window.RefreshStyles();

        Assert.Equal(new Thickness(7), a.Padding);
        AssertWinner(a, UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle);
        Assert.Equal(new Thickness(3), a.BorderThickness);
        AssertWinner(a, UIPilotProperty.BorderThickness, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle);
        Assert.Equal(Color.Red, ((MGSolidFillBrush)a.BackgroundBrush.NormalValue).Color);
        Assert.True(a.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.ImplicitStyle, out UIResolvedValue<IFillBrush> _));
        Assert.Equal(Color.Yellow, t.Foreground.NormalValue);
        AssertWinner(t, UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.ImplicitStyle);
        Assert.Empty(result.Skipped);
        Assert.Equal(4, result.WrittenValues);
        Assert.True(result.StyledElements >= 4, $"{result.StyledElements} styled elements");
    }

    [Fact]
    public void A_Replaced_Named_Style_Replaces_The_Values_Of_The_Previous_One()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "5")));
        harness.Desktop.Resources.AddStyle("Accent", NamedStyle("Accent", MGElementType.Border, ("Margin", "9")));
        MGWindow window = harness.Load(@"<Border Name=""P"" StyleNames=""Accent"" />");
        MGBorder p = window.GetElementByName<MGBorder>("P");
        Assert.Equal(new Thickness(5), p.Padding);
        Assert.Equal(new Thickness(9), p.Margin);

        harness.Desktop.Resources.RemoveStyle("Accent");
        harness.Desktop.Resources.AddStyle("Accent", NamedStyle("Accent", MGElementType.Border, ("Padding", "11"), ("Margin", "2")));
        window.RefreshStyles();

        Assert.Equal(new Thickness(11), p.Padding);
        AssertWinner(p, UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ExplicitStyle);
        Assert.Equal(new Thickness(2), p.Margin);
        AssertWinner(p, UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.ExplicitStyle);

        // As at parse time, a property set by a named style keeps no implicit style contribution.
        Assert.False(p.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle, out UIResolvedValue<Thickness> _));
    }

    [Fact]
    public void A_Style_Of_A_Nearer_Scope_Overrides_The_Style_Of_An_Outer_Scope()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "7")));
        MGWindow window = harness.Load(@"<StackPanel Orientation=""Vertical"">
    <StackPanel Name=""Scoped"" Orientation=""Vertical""><Border Name=""Inner"" /></StackPanel>
    <Border Name=""Outer"" />
</StackPanel>");
        MGBorder inner = window.GetElementByName<MGBorder>("Inner");
        MGBorder outer = window.GetElementByName<MGBorder>("Outer");
        Assert.Equal(new Thickness(7), inner.Padding);
        Assert.Equal(new Thickness(7), outer.Padding);

        window.GetElementByName<MGStackPanel>("Scoped").EnsureResourceScope().AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "3")));
        window.RefreshStyles();

        Assert.Equal(new Thickness(3), inner.Padding);
        AssertWinner(inner, UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle);
        Assert.Equal(new Thickness(7), outer.Padding);
    }

    [Fact]
    public void A_Refresh_Never_Overwrites_A_Xaml_Attribute_A_Local_Value_Or_A_Binding()
    {
        Harness harness = Harness.Create();
        MGWindow window = harness.Load(@"<StackPanel Orientation=""Vertical""><Border Name=""Attribute"" Padding=""4"" /><Border Name=""Local"" /><Border Name=""Bound"" /></StackPanel>");
        MGBorder attribute = window.GetElementByName<MGBorder>("Attribute");
        MGBorder local = window.GetElementByName<MGBorder>("Local");
        MGBorder bound = window.GetElementByName<MGBorder>("Bound");
        local.Padding = new Thickness(5);
        PaddingViewModel viewModel = new() { Pad = new Thickness(6) };
        bound.DataContextOverride = viewModel;
        DataBindingManager.AddBinding(new BindingConfig("Padding", nameof(PaddingViewModel.Pad)), bound);
        Assert.Equal(new Thickness(6), bound.Padding);

        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "7")));
        window.RefreshStyles();

        AssertPaddingOutranksTheStyle(attribute, new Thickness(4), UIValueSourceKind.LocalValue);
        AssertPaddingOutranksTheStyle(local, new Thickness(5), UIValueSourceKind.LocalValue);
        AssertPaddingOutranksTheStyle(bound, new Thickness(6), UIValueSourceKind.LocalBinding);

        viewModel.Pad = new Thickness(8);
        Assert.Equal(new Thickness(8), bound.Padding);
    }

    [Fact]
    public void A_Removed_Style_Gives_Its_Value_Up_On_Refresh()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "7")));
        MGWindow window = harness.Load(@"<Border Name=""P"" />");
        MGBorder p = window.GetElementByName<MGBorder>("P");
        Assert.Equal(new Thickness(7), p.Padding);
        UIResolvedContribution fallback = p.EnumerateResolvedContributions(UIPilotProperty.Padding, UIValueSlot.Whole).First(contribution => contribution.Kind != UIValueSourceKind.ImplicitStyle);

        harness.Desktop.Resources.RemoveImplicitStyle(MGElementType.Border);
        UIStyleRefreshResult result = window.RefreshStyles();

        Assert.False(p.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle, out UIResolvedValue<Thickness> _));
        Assert.Equal(fallback.Value, p.Padding);
        AssertWinner(p, UIPilotProperty.Padding, UIValueSlot.Whole, fallback.Kind);
        Assert.Equal(1, result.ClearedValues);
        Assert.Equal(0, result.WrittenValues);
    }

    [Fact]
    public void A_Refresh_Is_Bounded_To_The_Refreshed_Subtree()
    {
        Harness harness = Harness.Create();
        MGWindow window = harness.Load(@"<StackPanel Orientation=""Vertical"">
    <StackPanel Name=""First"" Orientation=""Vertical""><Border Name=""A"" /></StackPanel>
    <StackPanel Name=""Second"" Orientation=""Vertical""><Border Name=""B"" /></StackPanel>
</StackPanel>");
        MGStackPanel first = window.GetElementByName<MGStackPanel>("First");
        MGBorder a = window.GetElementByName<MGBorder>("A");
        MGBorder b = window.GetElementByName<MGBorder>("B");
        Thickness loadedPadding = b.Padding;

        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "7")));
        UIStyleRefreshResult result = first.RefreshStyles();

        Assert.Equal(new Thickness(7), a.Padding);
        Assert.Equal(loadedPadding, b.Padding);
        Assert.Equal(first.TraverseVisualTree().Distinct().Count(), result.VisitedElements);
        Assert.True(result.VisitedElements < window.TraverseVisualTree().Distinct().Count());
    }

    [Fact]
    public void A_Refresh_Keeps_The_Scoping_Of_Inline_Styles_And_Of_InheritsParentStyles()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "7")));
        MGWindow window = harness.Load(@"<StackPanel Orientation=""Vertical"">
    <StackPanel Orientation=""Vertical"">
        <StackPanel.Styles>
            <Style TargetType=""Border"">
                <Setter Property=""Padding"" Value=""5"" />
            </Style>
        </StackPanel.Styles>
        <Border Name=""Inline"" />
    </StackPanel>
    <StackPanel Orientation=""Vertical"" InheritsParentStyles=""False"">
        <Border Name=""Isolated"" />
    </StackPanel>
    <Border Name=""Outside"" />
</StackPanel>");
        MGBorder inline = window.GetElementByName<MGBorder>("Inline");
        MGBorder isolated = window.GetElementByName<MGBorder>("Isolated");
        MGBorder outside = window.GetElementByName<MGBorder>("Outside");
        Assert.Equal(new Thickness(5), inline.Padding);
        Assert.Equal(new Thickness(7), outside.Padding);
        Thickness isolatedPadding = isolated.Padding;

        harness.Desktop.Resources.RemoveImplicitStyle(MGElementType.Border);
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "8")));
        window.RefreshStyles();

        // The inline style still comes after the resource style, and the isolated subtree still sees no inherited style.
        Assert.Equal(new Thickness(5), inline.Padding);
        Assert.Equal(new Thickness(8), outside.Padding);
        Assert.Equal(isolatedPadding, isolated.Padding);
        Assert.False(isolated.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle, out UIResolvedValue<Thickness> _));
    }

    [Fact]
    public void A_Composite_Border_Setter_Is_Refreshed_On_The_Border_Its_Facade_Targets()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Button, ("BorderThickness", "2")));
        MGWindow window = harness.Load(@"<Button Name=""B""><TextBlock Text=""Go"" /></Button>");
        MGBorder border = window.GetElementByName<MGButton>("B").GetBorder();
        Assert.Equal(new Thickness(2), border.BorderThickness);

        harness.Desktop.Resources.RemoveImplicitStyle(MGElementType.Button);
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Button, ("BorderThickness", "4")));
        UIStyleRefreshResult result = window.RefreshStyles();

        Assert.Equal(new Thickness(4), border.BorderThickness);
        AssertWinner(border, UIPilotProperty.BorderThickness, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle);
        Assert.Equal(0, result.ClearedValues);
    }

    [Fact]
    public void Setters_A_Refresh_Cannot_Apply_Are_Reported_And_Left_Untouched()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.AddStyle("Accent", NamedStyle("Accent", MGElementType.Border, ("Margin", "3")));
        MGWindow window = harness.Load(@"<Border Name=""P"" StyleNames=""Accent"" />");
        MGBorder p = window.GetElementByName<MGBorder>("P");
        HorizontalAlignment loadedAlignment = p.HorizontalAlignment;
        Assert.Equal(new Thickness(3), p.Margin);

        harness.Desktop.Resources.RemoveStyle("Accent");
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border,
            ("HorizontalAlignment", loadedAlignment == HorizontalAlignment.Left ? "Right" : "Left"), ("NotAProperty", "1"), ("Padding", "not a thickness")));
        UIStyleRefreshResult result = window.RefreshStyles();

        Assert.Equal(loadedAlignment, p.HorizontalAlignment);
        Assert.Contains(result.Skipped, skip => ReferenceEquals(skip.Element, p) && skip.Name == "HorizontalAlignment" && skip.Reason == UIStyleRefreshSkipReason.NotRefreshable);
        Assert.Contains(result.Skipped, skip => ReferenceEquals(skip.Element, p) && skip.Name == "Accent" && skip.Reason == UIStyleRefreshSkipReason.StyleNotFound);
        Assert.Contains(result.Skipped, skip => ReferenceEquals(skip.Element, p) && skip.Name == "Padding" && skip.Reason == UIStyleRefreshSkipReason.InvalidValue && skip.Detail != null);
        Assert.DoesNotContain(result.Skipped, skip => skip.Name == "NotAProperty");
        Assert.False(p.TryGetResolvedContribution(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.ExplicitStyle, out UIResolvedValue<Thickness> _));
    }

    [Fact]
    public void A_Refresh_With_Unchanged_Styles_Does_Not_Touch_Layout_Values()
    {
        Harness harness = Harness.Create();
        harness.Desktop.Resources.AddImplicitStyle(ImplicitStyle(MGElementType.Border, ("Padding", "7"), ("BorderThickness", "2"), ("MinHeight", "20")));
        MGWindow window = harness.Load(@"<Border Name=""P"" />");
        MGBorder p = window.GetElementByName<MGBorder>("P");
        int layoutNotifications = 0;
        p.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MGElement.Padding) or nameof(MGBorder.BorderThickness) or nameof(MGElement.MinHeight))
            {
                layoutNotifications++;
            }
        };

        UIStyleRefreshResult first = window.RefreshStyles();
        UIStyleRefreshResult second = window.RefreshStyles();

        Assert.Equal(0, layoutNotifications);
        Assert.Equal(3, first.WrittenValues);
        Assert.Equal(3, second.WrittenValues);
        Assert.Equal(0, second.ClearedValues);
    }

    private static void AssertPaddingOutranksTheStyle(MGElement element, Thickness expected, UIValueSourceKind winnerKind)
    {
        Assert.Equal(expected, element.Padding);
        AssertWinner(element, UIPilotProperty.Padding, UIValueSlot.Whole, winnerKind);
        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle, out UIResolvedValue<Thickness> style));
        Assert.Equal(new Thickness(7), style.Value);
    }

    private static void AssertWinner(MGElement element, UIPilotProperty property, UIValueSlot slot, UIValueSourceKind kind)
    {
        Assert.True(element.TryGetResolvedValueSource(property, slot, out UIValueResolutionSource source), $"{property} has no resolved value");
        Assert.Equal(kind, source.Kind);
    }

    private static XamlStyle ImplicitStyle(MGElementType targetType, params (string Property, string Value)[] setters)
        => new() { TargetType = targetType, Setters = setters.Select(setter => new XamlSetter { Property = setter.Property, Value = setter.Value }).ToList() };

    private static XamlStyle NamedStyle(string name, MGElementType targetType, params (string Property, string Value)[] setters)
    {
        XamlStyle style = ImplicitStyle(targetType, setters);
        style.Name = name;
        return style;
    }

    private sealed class PaddingViewModel : INotifyPropertyChanged
    {
        private Thickness _pad;
        public Thickness Pad
        {
            get => _pad;
            set
            {
                _pad = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pad)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            Harness harness = new(runtime, new MGDesktop(runtime));
            harness.Frame(0);
            return harness;
        }

        /// <summary>Loads a root window whose content is <paramref name="content"/>, parsed with the desktop resources, and shows it.</summary>
        public MGWindow Load(string content)
        {
            MGWindow window = MGUIXamlParser.LoadRootWindow(Desktop,
                $@"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""300"" Height=""200"">{content}</Window>", false, true);
            Desktop.Windows.Add(window);
            Frame(1);
            return window;
        }

        public void Frame(int frameIndex)
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
