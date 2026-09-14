using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

/// <summary>Proves the two value-equality guards (ADR-0009, W1) keep their pre-existing observable behaviour now that they go through
/// <see cref="UIBrushEquality.ForGuards{T}"/> instead of <see cref="EqualityComparer{T}.Default"/> directly: a distinct-but-equal-valued
/// brush instance does not count as a change for <see cref="VisualStateSetting{TDataType}"/>'s slot setters, and still lets
/// <see cref="MGControlTemplate"/> re-apply a brush-typed template default on a theme refresh. A mutation of <see cref="UIBrushEquality.ForGuards{T}"/>
/// pointed at reference equality for brush <c>T</c> was run by hand to confirm both tests below fail without the value comparer
/// (and that <see cref="ThemeRefreshRegressionTests"/> stays green, since none of its cases use a brush-typed <c>T</c>); see the slice
/// status in Docs/Tasks/animation-v4-tasks.md for the exact recorded run.</summary>
public class BrushGuardEqualityTests
{
    [Fact]
    public void VisualStateSetting_Of_IFillBrush_Does_Not_Notify_When_An_Equal_Valued_New_Instance_Is_Assigned()
    {
        VisualStateSetting<IFillBrush> setting = new(new MGSolidFillBrush(Color.Red));
        int notificationCount = 0;
        setting.PropertyChanged += (_, _) => notificationCount++;

        setting.NormalValue = new MGSolidFillBrush(Color.Red);

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
}
