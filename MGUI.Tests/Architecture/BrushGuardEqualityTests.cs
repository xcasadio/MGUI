using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Tests.Animation;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

/// <summary>Proves the two value-equality guards (ADR-0009, W1, fix round W2-fix) keep their intended, now DIFFERENT, observable
/// behaviour: <see cref="VisualStateSetting{TDataType}"/>'s slot setters (<see cref="UIBrushEquality.ForSlots{T}"/>) treat a
/// distinct-but-equal-valued brush instance as a change (identity matters: the caller may mutate that exact instance afterwards),
/// while <see cref="MGControlTemplate"/>'s theme-refresh re-application guard (<see cref="UIBrushEquality.ForGuards{T}"/>) still
/// treats it as unchanged (value only, identity irrelevant there). The mutation proof (Work item 3 of the W2-fix brief) was run by
/// hand: pointing <see cref="UIBrushEquality.ForSlots{T}"/> at the value comparer instead of reference equality makes
/// <see cref="VisualStateSetting_Of_IFillBrush_Stores_The_New_Instance_And_Notifies_Once_For_An_Equal_Valued_New_Instance"/> and
/// <see cref="Mutating_A_Slot_Assigned_Equal_Valued_Instance_Changes_What_The_Next_Frame_Draws"/> fail (the slot keeps the OLD
/// instance, so <c>Assert.Same</c> fails and the mutated colour never reaches the frame), while the template-guard tests below and
/// <see cref="ThemeRefreshRegressionTests"/> stay green throughout (their brush-typed cases never rely on <c>ForSlots</c>); see the
/// slice status in Docs/Tasks/animation-v4-tasks.md for the exact recorded run.</summary>
public class BrushGuardEqualityTests
{
    [Fact]
    public void VisualStateSetting_Of_IFillBrush_Stores_The_New_Instance_And_Notifies_Once_For_An_Equal_Valued_New_Instance()
    {
        MGSolidFillBrush initial = new(Color.Red);
        VisualStateSetting<IFillBrush> setting = new(initial);
        int notificationCount = 0;
        setting.PropertyChanged += (_, _) => notificationCount++;

        MGSolidFillBrush distinctButEqual = new(Color.Red);
        setting.NormalValue = distinctButEqual;

        Assert.Same(distinctButEqual, setting.NormalValue);
        Assert.NotSame(initial, setting.NormalValue);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public void VisualStateSetting_Of_IFillBrush_Does_Not_Notify_When_The_Same_Instance_Is_Reassigned()
    {
        MGSolidFillBrush brush = new(Color.Red);
        VisualStateSetting<IFillBrush> setting = new(brush);
        int notificationCount = 0;
        setting.PropertyChanged += (_, _) => notificationCount++;

        setting.NormalValue = brush;

        Assert.Same(brush, setting.NormalValue);
        Assert.Equal(0, notificationCount);
    }

    [Fact]
    public void VisualStateSetting_Of_IFillBrush_Notifies_For_A_Different_Colored_Instance()
    {
        VisualStateSetting<IFillBrush> setting = new(new MGSolidFillBrush(Color.Red));
        int notificationCount = 0;
        setting.PropertyChanged += (_, _) => notificationCount++;

        setting.NormalValue = new MGSolidFillBrush(Color.Blue);

        Assert.Equal(1, notificationCount);
    }

    /// <summary>End-to-end (ACCEPTANCE 2): the slot already holds a brush equal in value to the one about to be assigned; the new,
    /// distinct instance is assigned anyway (W2-fix: identity is a change), then mutated -- the mutation reaches the element and the
    /// next drawn frame reflects it. Before the fix, the equal-valued assignment was a silent no-op: the slot kept the OLD instance,
    /// so mutating the new one would have had no visible effect.</summary>
    [Fact]
    public void Mutating_A_Slot_Assigned_Equal_Valued_Instance_Changes_What_The_Next_Frame_Draws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Red);
        scene.Frames(1);

        // Distinct instance, same value (Color.Red) as the one already in the slot.
        MGSolidFillBrush distinctButEqual = new(Color.Red);
        scene.Top.BackgroundBrush.NormalValue = distinctButEqual;
        scene.Frames(1);

        var beforeMutation = scene.Draw();
        Assert.Contains(beforeMutation.FillRectangleCalls, call => call.Color == Color.Red);

        distinctButEqual.Color = Color.Blue;
        scene.Frames(1);

        var afterMutation = scene.Draw();
        Assert.Contains(afterMutation.FillRectangleCalls, call => call.Color == Color.Blue);
        Assert.DoesNotContain(afterMutation.FillRectangleCalls, call => call.Color == Color.Red);
    }

    private sealed class ThemeRefreshOwnerStub : MGElement
    {
        public ThemeRefreshOwnerStub(MGDesktop desktop)
            : base(desktop, null, MGElementType.Custom)
        {
            throw new NotSupportedException("Use CreateOwnerStub() in tests.");
        }
    }

    private static ThemeRefreshOwnerStub CreateOwnerStub()
    {
#pragma warning disable SYSLIB0050
        ThemeRefreshOwnerStub owner = (ThemeRefreshOwnerStub)FormatterServices.GetUninitializedObject(typeof(ThemeRefreshOwnerStub));
#pragma warning restore SYSLIB0050

        FieldInfo appliedDefaultsField = typeof(MGElement).GetField("_appliedTemplateDefaults", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(appliedDefaultsField);
        appliedDefaultsField!.SetValue(owner, new Dictionary<string, object>(StringComparer.Ordinal));

        return owner;
    }

    /// <summary>Same pattern as <see cref="ThemeRefreshRegressionTests.Theme_Refresh_Reapplies_Template_Default_When_Current_Value_Still_Matches_Previous_Default"/>,
    /// but with a brush-typed default (<see cref="IBorderBrush"/>): the current value is a distinct <see cref="MGUniformBorderBrush"/> instance
    /// with the same color as the previous template default, so the guard (now <see cref="UIBrushEquality.ForGuards{T}"/> rather than the
    /// boxed struct's own value equality) must still treat it as "unchanged" and let the refreshed template's new default through.</summary>
    [Fact]
    public void Theme_Refresh_Reapplies_A_Brush_Template_Default_When_Current_Value_Is_A_Distinct_But_Equal_Valued_Instance()
    {
        ThemeRefreshOwnerStub owner = CreateOwnerStub();
        IBorderBrush currentValue = new MGUniformBorderBrush(Color.Red);

        MGControlTemplate initialTemplate = new("Test.Template.Initial", context =>
            context.ApplyThemeDefault("Widget.BorderBrush", new MGUniformBorderBrush(Color.Red), () => currentValue, value => currentValue = value));
        initialTemplate.Apply(owner, false);

        // A distinct instance, but the same value as the default the previous application recorded.
        currentValue = new MGUniformBorderBrush(Color.Red);

        MGControlTemplate refreshedTemplate = new("Test.Template.Refreshed", context =>
            context.ApplyThemeDefault("Widget.BorderBrush", new MGUniformBorderBrush(Color.Blue), () => currentValue, value => currentValue = value));
        refreshedTemplate.Apply(owner, true);

        Assert.True(UIBrushEquality.ValueEquals(new MGUniformBorderBrush(Color.Blue), currentValue));
    }

    /// <summary>Same pattern as <see cref="Theme_Refresh_Reapplies_A_Brush_Template_Default_When_Current_Value_Is_A_Distinct_But_Equal_Valued_Instance"/>,
    /// but with an <see cref="IFillBrush"/> default: <see cref="MGSolidFillBrush"/> is a reference type as of ADR-0009/W2, so the previous
    /// application's default and the current value below are now two genuinely distinct class instances (not two boxes of the same struct
    /// value); the guard must still treat them as "unchanged" through <see cref="UIBrushEquality.ForGuards{T}"/> so the refreshed template's
    /// new default is applied.</summary>
    [Fact]
    public void Theme_Refresh_Reapplies_A_Fill_Brush_Template_Default_When_Current_Value_Is_A_Distinct_But_Equal_Valued_Instance()
    {
        ThemeRefreshOwnerStub owner = CreateOwnerStub();
        IFillBrush currentValue = new MGSolidFillBrush(Color.Red);

        MGControlTemplate initialTemplate = new("Test.Template.Initial", context =>
            context.ApplyThemeDefault("Widget.FillBrush", new MGSolidFillBrush(Color.Red), () => currentValue, value => currentValue = value));
        initialTemplate.Apply(owner, false);

        // A distinct MGSolidFillBrush instance, but the same Color as the default the previous application recorded.
        currentValue = new MGSolidFillBrush(Color.Red);

        MGControlTemplate refreshedTemplate = new("Test.Template.Refreshed", context =>
            context.ApplyThemeDefault("Widget.FillBrush", new MGSolidFillBrush(Color.Blue), () => currentValue, value => currentValue = value));
        refreshedTemplate.Apply(owner, true);

        Assert.True(UIBrushEquality.ValueEquals(new MGSolidFillBrush(Color.Blue), currentValue));
    }
}
