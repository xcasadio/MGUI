using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using MGUI.Core.UI;
using MGUI.Core.UI.Styling;

namespace MGUI.Tests.Architecture;

public class ThemeRefreshRegressionTests
{
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

        FieldInfo? appliedDefaultsField = typeof(MGElement).GetField("_AppliedTemplateDefaults", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(appliedDefaultsField);
        appliedDefaultsField!.SetValue(owner, new Dictionary<string, object>(StringComparer.Ordinal));

        return owner;
    }

    [Fact]
    public void Theme_Refresh_Reapplies_Template_Default_When_Current_Value_Still_Matches_Previous_Default()
    {
        ThemeRefreshOwnerStub owner = CreateOwnerStub();
        int currentValue = 10;

        MGControlTemplate initialTemplate = new("Test.Template.Initial", context =>
            context.ApplyThemeDefault("Widget.Padding", 20, () => currentValue, value => currentValue = value));
        initialTemplate.Apply(owner, false);

        Assert.Equal(20, currentValue);

        MGControlTemplate refreshedTemplate = new("Test.Template.Refreshed", context =>
            context.ApplyThemeDefault("Widget.Padding", 32, () => currentValue, value => currentValue = value));
        refreshedTemplate.Apply(owner, true);

        Assert.Equal(32, currentValue);
    }

    [Fact]
    public void Theme_Refresh_Does_Not_Overwrite_Local_Value_That_Diverged_From_Previous_Template_Default()
    {
        ThemeRefreshOwnerStub owner = CreateOwnerStub();
        int currentValue = 10;

        MGControlTemplate initialTemplate = new("Test.Template.Initial", context =>
            context.ApplyThemeDefault("Widget.Padding", 20, () => currentValue, value => currentValue = value));
        initialTemplate.Apply(owner, false);

        currentValue = 99;

        MGControlTemplate refreshedTemplate = new("Test.Template.Refreshed", context =>
            context.ApplyThemeDefault("Widget.Padding", 32, () => currentValue, value => currentValue = value));
        refreshedTemplate.Apply(owner, true);

        Assert.Equal(99, currentValue);
    }

    [Fact]
    public void Structure_Rebuild_Refresh_Applies_A_Part_Default_Over_The_Construction_Value_Of_The_New_Part()
    {
        ThemeRefreshOwnerStub owner = CreateOwnerStub();
        int replacedPart = 10;

        MGControlTemplate initialTemplate = new("Test.Template.Initial", context =>
            context.ApplyThemeDefault("Widget.Part.Padding", 20, () => replacedPart, value => replacedPart = value));
        initialTemplate.Apply(owner, false);
        Assert.Equal(20, replacedPart);

        // The new part holds the value of its construction, which the record of the replaced part cannot tell from a value set since.
        int newPart = 5;
        MGControlTemplate refreshedTemplate = new("Test.Template.Refreshed", context =>
            context.ApplyThemeDefault("Widget.Part.Padding", 32, () => newPart, value => newPart = value));
        refreshedTemplate.Apply(owner, true);
        Assert.Equal(5, newPart);

        refreshedTemplate.Apply(owner, true, true);
        Assert.Equal(32, newPart);
    }

    [Fact]
    public void Structure_Rebuild_Refresh_Does_Not_Overwrite_An_Owner_Value_That_Diverged_From_Its_Default()
    {
        ThemeRefreshOwnerStub owner = CreateOwnerStub();
        int ownerValue = 10;

        MGControlTemplate initialTemplate = new("Test.Template.Initial", context =>
            context.ApplyOwnerThemeDefault("Widget.IndentSize", 20, () => ownerValue, value => ownerValue = value));
        initialTemplate.Apply(owner, false);
        Assert.Equal(20, ownerValue);

        ownerValue = 99;

        MGControlTemplate refreshedTemplate = new("Test.Template.Refreshed", context =>
            context.ApplyOwnerThemeDefault("Widget.IndentSize", 32, () => ownerValue, value => ownerValue = value));
        refreshedTemplate.Apply(owner, true, true);

        Assert.Equal(99, ownerValue);
    }
}