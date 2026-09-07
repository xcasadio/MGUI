using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S2 wiring of the five scalar pilot properties (ADR-0005) through
/// <see cref="UIResolvedPropertyStore"/>: <see cref="MGElement.Margin"/>, <see cref="MGElement.Padding"/>,
/// <see cref="MGElement.MinHeight"/> on <see cref="MGElement"/>, and <see cref="MGBorder.BorderBrush"/>,
/// <see cref="MGBorder.BorderThickness"/> on <see cref="MGBorder"/>. Precedence, fall-back, notification
/// parity with the pre-S2 direct setters, and the accepted cost budget.
/// </summary>
public class ResolvedScalarPilotsTests
{
    private static readonly Thickness DefaultThickness = new(0);

    [Fact]
    public void Constructor_Contributions_Are_DefaultValue_On_PilotProbeElement()
    {
        Harness harness = Harness.Create();
        PilotProbeElement element = new(harness.Window);

        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<Thickness> margin));
        Assert.Equal(DefaultThickness, margin.Value);

        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<Thickness> padding));
        Assert.Equal(DefaultThickness, padding.Value);

        // MinHeight has no constructor default: no contribution at all until a tagged write occurs.
        Assert.False(element.TryGetResolvedContribution<int?>(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out _));
    }

    [Fact]
    public void Constructor_Contributions_Are_DefaultValue_On_MGBorder()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);

        Assert.True(border.TryGetResolvedContribution(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<IBorderBrush> brush));
        // MGUniformBorderBrush is a value type: two independently-boxed copies compare structurally equal
        // (Equals) even though the store, matching the pre-S2 setter's `!=` on the interface, keys contributions
        // by reference equality (see MGBorder.SetBorderBrush's comparer).
        Assert.Equal(MGUniformBorderBrush.Black, brush.Value);

        Assert.True(border.TryGetResolvedContribution(UIPilotProperty.BorderThickness, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<Thickness> thickness));
        Assert.Equal(new Thickness(1), thickness.Value);
    }

    [Fact]
    public void LocalValue_Wins_Over_Theme_And_A_Later_Theme_Write_Neither_Changes_Effective_Nor_Notifies()
    {
        Harness harness = Harness.Create();
        PilotProbeElement element = new(harness.Window);
        harness.Show(element);

        element.SetPadding(new Thickness(5), UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        Assert.Equal(new Thickness(5), element.Padding);

        element.Padding = new Thickness(10); // public setter => LocalValue
        Assert.Equal(new Thickness(10), element.Padding);

        int npcCount = 0;
        element.PropertyChanged += (_, _) => npcCount++;

        element.SetPadding(new Thickness(20), UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

        Assert.Equal(new Thickness(10), element.Padding); // local still wins
        Assert.Equal(0, npcCount); // effective value did not change, so no notification
    }

    [Fact]
    public void ClearPilotSource_Padding_LocalValue_FallsBackToTheme_WithNotification()
    {
        Harness harness = Harness.Create();
        PilotProbeElement element = new(harness.Window);
        harness.Show(element);

        element.SetPadding(new Thickness(5), UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        element.Padding = new Thickness(10);
        Assert.Equal(new Thickness(10), element.Padding);

        List<string> raised = new();
        element.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        element.ClearPilotSource(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.LocalValue);

        Assert.Equal(new Thickness(5), element.Padding);
        Assert.Contains(nameof(MGElement.Padding), raised);
    }

    [Fact]
    public void ClearPilotSource_MinHeight_WithNoConstructorDefault_KeepsClrValue_RaisesNoNotification_AndReadFails()
    {
        Harness harness = Harness.Create();
        PilotProbeElement element = new(harness.Window);
        harness.Show(element);

        element.SetMinHeight(20, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        Assert.Equal(20, element.MinHeight);

        int npcCount = 0;
        element.PropertyChanged += (_, _) => npcCount++;

        element.ClearPilotSource(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.LocalValue);

        Assert.Equal(20, element.MinHeight); // CLR value kept, per ADR-0005's documented fall-back
        Assert.Equal(0, npcCount);
        Assert.False(element.TryGetResolvedPilotValue(UIPilotProperty.MinHeight, UIValueSlot.Whole, out UIResolvedValue<int?> _));
    }

    [Fact]
    public void NPC_Counts_Through_The_Public_Setter_Match_The_Pre_S2_Baseline()
    {
        Harness harness = Harness.Create();
        PilotProbeElement element = new(harness.Window);
        harness.Show(element);

        // LayoutChanged (raised identically before and after S2) also flips IsLayoutValid, which is itself a
        // notified property; the pilot-specific baseline from the plan counts only the setter's own NPC(...)
        // calls, so IsLayoutValid is excluded here.
        List<string> marginNpc = new();
        int onMarginChangedCount = 0;
        element.PropertyChanged += (_, e) => { if (e.PropertyName != nameof(MGElement.IsLayoutValid)) marginNpc.Add(e.PropertyName); };
        element.OnMarginChanged += (_, _) => onMarginChangedCount++;
        element.Margin = new Thickness(1, 2, 3, 4);
        Assert.Equal(11, marginNpc.Count);
        Assert.Equal(1, onMarginChangedCount);

        List<string> paddingNpc = new();
        element.PropertyChanged += (_, e) => { if (e.PropertyName != nameof(MGElement.IsLayoutValid)) paddingNpc.Add(e.PropertyName); };
        element.Padding = new Thickness(2, 3, 4, 5);
        Assert.Equal(7, paddingNpc.Count);

        List<string> minHeightNpc = new();
        element.PropertyChanged += (_, e) => { if (e.PropertyName != nameof(MGElement.IsLayoutValid)) minHeightNpc.Add(e.PropertyName); };
        element.MinHeight = 42;
        Assert.Equal(3, minHeightNpc.Count);
    }

    [Fact]
    public void LayoutChanged_Is_Raised_For_Padding_Margin_MinHeight_BorderThickness_But_Not_BorderBrush()
    {
        Harness harness = Harness.Create();
        PilotProbeElement element = new(harness.Window);
        harness.Show(element);
        Assert.True(element.IsLayoutValid);

        element.Margin = new Thickness(1);
        Assert.False(element.IsLayoutValid);
        harness.Frame(2, Point.Zero);
        harness.Frame(3, Point.Zero);
        Assert.True(element.IsLayoutValid);

        element.Padding = new Thickness(1);
        Assert.False(element.IsLayoutValid);
        harness.Frame(4, Point.Zero);
        harness.Frame(5, Point.Zero);
        Assert.True(element.IsLayoutValid);

        element.MinHeight = 5;
        Assert.False(element.IsLayoutValid);
        harness.Frame(6, Point.Zero);
        harness.Frame(7, Point.Zero);
        Assert.True(element.IsLayoutValid);

        MGBorder border = new(harness.Window);
        harness.Show(border);
        Assert.True(border.IsLayoutValid);

        border.BorderThickness = new Thickness(2);
        Assert.False(border.IsLayoutValid);
        harness.Frame(8, Point.Zero);
        harness.Frame(9, Point.Zero);
        Assert.True(border.IsLayoutValid);

        border.BorderBrush = MGUniformBorderBrush.Transparent;
        Assert.True(border.IsLayoutValid); // BorderBrush is a Draw-only pilot: no layout invalidation
    }

    [Fact]
    public void Composite_MGButton_Resolves_BorderBrush_On_Its_Inner_Border()
    {
        Harness harness = Harness.Create();
        MGButton button = new(harness.Window);
        harness.Show(button);

        MGBorder inner = button.GetBorder();
        Assert.NotNull(inner);

        Assert.True(button.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> fromComposite));
        Assert.True(inner.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> fromBorder));
        Assert.Equal(fromBorder.Value, fromComposite.Value);
    }

    [Fact]
    public void Uninitialized_Element_Reads_Return_False_Without_Exception()
    {
        MGTextBlock element = (MGTextBlock)FormatterServices.GetUninitializedObject(typeof(MGTextBlock));

        Assert.Equal(0, element.ResolvedEntryCount);
        Assert.False(element.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> _));
        Assert.False(element.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<Thickness> _));
        Assert.False(element.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> _));
    }

    [Fact]
    public void Budget_EntryCount_Matches_The_Accepted_Cost()
    {
        Harness harness = Harness.Create();

        PilotProbeElement element = new(harness.Window);
        Assert.Equal(2, element.ResolvedEntryCount); // Margin, Padding

        MGBorder border = new(harness.Window);
        Assert.Equal(4, border.ResolvedEntryCount); // inherited Margin, Padding + own BorderBrush, BorderThickness

        int elementEntryCountBefore = element.ResolvedEntryCount;
        element.Opacity = 0.5f;
        element.Visibility = Visibility.Collapsed;
        Assert.Equal(elementEntryCountBefore, element.ResolvedEntryCount);
    }

    [Fact]
    public void ContextMenu_Constructor_DefaultValue_Contribution_Survives_Under_The_Catalogue_Winner()
    {
        Harness harness = Harness.Create();
        MGContextMenu menu = new(harness.Window);
        harness.Show(menu);

        // The constructor's own DefaultValue write (ADR-0005 worked example) is still recorded in the store even
        // though the catalogue (still on the untagged public setter in S2) now outranks it. BorderBrush's store
        // lives on the composite's inner MGBorder (reached via GetBorder()), not on MGContextMenu itself.
        MGBorder menuBorder = menu.GetBorder();
        Assert.NotNull(menuBorder);
        Assert.True(menuBorder.TryGetResolvedContribution(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<IBorderBrush> ctorContribution));
        Assert.Equal(MGUniformBorderBrush.Transparent, ctorContribution.Value);

        Assert.True(menu.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> winner));
        // Compared by content: theme brushes are clones, so reference equality would be wrong here.
        Assert.Equal(MGUniformBorderBrush.Gray, winner.Value);
        // In S2 the catalogue still writes through the public setter (LocalValue); S3 tightens this to Template.
        Assert.True(winner.Source.Kind > UIValueSourceKind.DefaultValue);
    }

    [Fact]
    public void Templated_MGWindow_WindowStyle_Changes_Effective_Padding_And_BorderThickness_As_LocalValue()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 0, 0, 400, 300);
        harness.Show(window);

        MGTheme theme = window.GetTheme();

        window.WindowStyle = WindowStyle.None;
        Assert.Equal(theme.Window.ChromelessPadding, window.Padding);
        Assert.Equal(theme.Window.ChromelessBorderThickness, window.BorderThickness);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> chromelessPadding));
        Assert.Equal(UIValueSourceKind.LocalValue, chromelessPadding.Source.Kind);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> chromelessBorder));
        Assert.Equal(UIValueSourceKind.LocalValue, chromelessBorder.Source.Kind);

        window.WindowStyle = WindowStyle.Default;
        Assert.Equal(theme.Window.Padding, window.Padding);
        Assert.Equal(theme.Window.BorderThickness, window.BorderThickness);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> defaultPadding));
        Assert.Equal(UIValueSourceKind.LocalValue, defaultPadding.Source.Kind);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> defaultBorder));
        Assert.Equal(UIValueSourceKind.LocalValue, defaultBorder.Source.Kind);
    }

    [Fact]
    public void GraphNode_Owner_Configured_InlineTextBox_Stays_At_LocalValue_Chrome_Across_A_Theme_Refresh()
    {
        Harness harness = Harness.Create();
        GraphCommentModel model = new(Guid.NewGuid(), new Rectangle(0, 0, 220, 120), "Title", "Body");
        MGGraphCommentBox commentBox = new(harness.Window, model);
        harness.Show(commentBox);

        MGTextBox bodyTextBox = commentBox.BodyTextBox;
        Assert.NotNull(bodyTextBox);

        AssertInlineEditorChrome(bodyTextBox);

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        AssertInlineEditorChrome(bodyTextBox);

        static void AssertInlineEditorChrome(MGTextBox textBox)
        {
            Assert.Equal(new Thickness(0), textBox.Padding);
            Assert.Equal(new Thickness(0), textBox.BorderThickness);
            Assert.Equal(MGUniformBorderBrush.Transparent, textBox.BorderBrush);

            Assert.True(textBox.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> padding));
            Assert.Equal(UIValueSourceKind.LocalValue, padding.Source.Kind);
            Assert.True(textBox.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> borderThickness));
            Assert.Equal(UIValueSourceKind.LocalValue, borderThickness.Source.Kind);
            Assert.True(textBox.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> borderBrush));
            Assert.Equal(UIValueSourceKind.LocalValue, borderBrush.Source.Kind);
        }
    }

    /// <summary>Test-only <see cref="MGElement"/> subclass with no pilot writes of its own: every contribution
    /// observed on an instance of this type comes from <see cref="MGElement"/>'s own base constructor or from a
    /// test calling a tagged setter directly.</summary>
    private sealed class PilotProbeElement : MGElement
    {
        public PilotProbeElement(MGWindow window)
            : base(window, MGElementType.Custom)
        {
        }
    }

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
            Frame(0, Point.Zero);
            Frame(1, Point.Zero);
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
