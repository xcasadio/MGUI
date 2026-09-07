using System;
using System.Collections.Generic;
using MGUI.Core.UI.Styling;
using Xunit;

namespace MGUI.Tests.Architecture;

public class ResolvedPropertyStoreTests
{
    private static readonly UIValueSourceKind[] AllKinds =
    {
        UIValueSourceKind.DefaultValue,
        UIValueSourceKind.Inherited,
        UIValueSourceKind.Theme,
        UIValueSourceKind.DynamicResource,
        UIValueSourceKind.ImplicitStyle,
        UIValueSourceKind.ExplicitStyle,
        UIValueSourceKind.Template,
        UIValueSourceKind.VisualState,
        UIValueSourceKind.LocalBinding,
        UIValueSourceKind.LocalValue,
        UIValueSourceKind.Animation,
    };

    private static UIValueResolutionSource SourceFor(UIValueSourceKind kind, string name = null) => kind switch
    {
        UIValueSourceKind.DefaultValue => UIValueResolutionSource.Default(UIInvalidationKind.None, name),
        UIValueSourceKind.Inherited => UIValueResolutionSource.Inherited(UIInvalidationKind.None, name),
        UIValueSourceKind.Theme => UIValueResolutionSource.Theme(UIInvalidationKind.None, name),
        UIValueSourceKind.DynamicResource => UIValueResolutionSource.DynamicResource(UIInvalidationKind.None, name),
        UIValueSourceKind.ImplicitStyle => UIValueResolutionSource.ImplicitStyle(UIInvalidationKind.None, name),
        UIValueSourceKind.ExplicitStyle => UIValueResolutionSource.ExplicitStyle(UIInvalidationKind.None, name),
        UIValueSourceKind.Template => UIValueResolutionSource.Template(UIInvalidationKind.None, name),
        UIValueSourceKind.VisualState => UIValueResolutionSource.VisualState(UIInvalidationKind.None, name),
        UIValueSourceKind.LocalBinding => UIValueResolutionSource.LocalBinding(UIInvalidationKind.None, name),
        UIValueSourceKind.LocalValue => UIValueResolutionSource.LocalValue(UIInvalidationKind.None, name),
        UIValueSourceKind.Animation => UIValueResolutionSource.Animation(UIInvalidationKind.None, name),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Fact]
    public void Precedence_Matrix_Higher_Kind_Always_Wins()
    {
        foreach (UIValueSourceKind a in AllKinds)
        {
            foreach (UIValueSourceKind b in AllKinds)
            {
                if (a == b)
                    continue;

                UIValueResolutionSource sourceA = SourceFor(a);
                UIValueResolutionSource sourceB = SourceFor(b);
                UIValueSourceKind higher = sourceA.Precedence > sourceB.Precedence ? a : b;
                int higherValue = higher == a ? 1 : 2;

                // Order 1: lower/higher written first, in that order.
                UIResolvedPropertyStore store1 = new();
                store1.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 1, sourceA, EqualityComparer<int>.Default, out _, out _);
                store1.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 2, sourceB, EqualityComparer<int>.Default, out _, out UIResolvedValue<int> effective1);
                Assert.True(effective1.IsSet);
                Assert.Equal(higherValue, effective1.Value);
                Assert.Equal(higher, effective1.Source.Kind);

                // Order 2: same two contributions written in reverse order.
                UIResolvedPropertyStore store2 = new();
                store2.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 2, sourceB, EqualityComparer<int>.Default, out _, out _);
                store2.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 1, sourceA, EqualityComparer<int>.Default, out _, out UIResolvedValue<int> effective2);
                Assert.True(effective2.IsSet);
                Assert.Equal(higherValue, effective2.Value);
                Assert.Equal(higher, effective2.Source.Kind);
            }
        }
    }

    [Fact]
    public void Equal_Precedence_Last_Writer_Wins()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 1, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        bool added = store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 2, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out bool changed, out UIResolvedValue<int> effective);

        Assert.False(added);
        Assert.True(changed);
        Assert.Equal(2, effective.Value);
        Assert.Equal(1, store.Contributions(UIPilotProperty.Padding, UIValueSlot.Whole).Count);
    }

    [Fact]
    public void Unset_Of_Winner_Falls_Back_To_Next_Contribution()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 1, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        store.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 2, UIValueResolutionSource.LocalValue(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);

        bool removed = store.Unset(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.LocalValue, EqualityComparer<int>.Default, out bool changed, out UIResolvedValue<int> effective);

        Assert.True(removed);
        Assert.True(changed);
        Assert.True(effective.IsSet);
        Assert.Equal(1, effective.Value);
        Assert.Equal(UIValueSourceKind.Theme, effective.Source.Kind);
    }

    [Fact]
    public void Unset_Of_NonWinning_Contribution_Leaves_Effective_Value_Unchanged()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 1, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        store.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 2, UIValueResolutionSource.LocalValue(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);

        bool removed = store.Unset(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.Theme, EqualityComparer<int>.Default, out bool changed, out UIResolvedValue<int> effective);

        Assert.True(removed);
        Assert.False(changed);
        Assert.Equal(2, effective.Value);
        Assert.Equal(UIValueSourceKind.LocalValue, effective.Source.Kind);
    }

    [Fact]
    public void Unset_Of_Sole_Contribution_Yields_Unset_Without_Notification()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.MinHeight, UIValueSlot.Whole, 5, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure), EqualityComparer<int>.Default, out _, out _);

        bool removed = store.Unset(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.LocalValue, EqualityComparer<int>.Default, out bool changed, out UIResolvedValue<int> effective);

        Assert.True(removed);
        Assert.False(changed);
        Assert.False(effective.IsSet);

        bool hasWinner = store.TryGetWinner(UIPilotProperty.MinHeight, UIValueSlot.Whole, out UIResolvedValue<int> winner);
        Assert.False(hasWinner);
        Assert.False(winner.IsSet);
        Assert.Empty(store.Contributions(UIPilotProperty.MinHeight, UIValueSlot.Whole));
    }

    [Fact]
    public void Unset_Does_Not_Deallocate_The_Entry()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.MinHeight, UIValueSlot.Whole, 5, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure), EqualityComparer<int>.Default, out _, out _);
        int countBefore = store.EntryCount;

        store.Unset(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.LocalValue, EqualityComparer<int>.Default, out _, out _);

        Assert.Equal(countBefore, store.EntryCount);
    }

    [Fact]
    public void EffectiveChanged_Is_False_When_A_Lower_Source_Writes_The_Same_Value()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 3, UIValueResolutionSource.LocalValue(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);

        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 3, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out bool changed, out UIResolvedValue<int> effective);

        Assert.False(changed);
        Assert.Equal(3, effective.Value);
        Assert.Equal(UIValueSourceKind.LocalValue, effective.Source.Kind);
    }

    [Fact]
    public void EffectiveChanged_Is_False_When_The_Current_Winner_Value_Is_Rewritten_By_A_Higher_Source()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 3, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);

        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 3, UIValueResolutionSource.LocalValue(UIInvalidationKind.None), EqualityComparer<int>.Default, out bool changed, out UIResolvedValue<int> effective);

        Assert.False(changed);
        Assert.Equal(3, effective.Value);
        Assert.Equal(UIValueSourceKind.LocalValue, effective.Source.Kind);
    }

    [Fact]
    public void EffectiveChanged_Is_True_When_The_Winner_Writes_A_Different_Value()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 3, UIValueResolutionSource.LocalValue(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);

        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 4, UIValueResolutionSource.LocalValue(UIInvalidationKind.None), EqualityComparer<int>.Default, out bool changed, out UIResolvedValue<int> effective);

        Assert.True(changed);
        Assert.Equal(4, effective.Value);
    }

    [Fact]
    public void Untouched_Entry_Has_No_Cost()
    {
        UIResolvedPropertyStore store = new();

        Assert.Equal(0, store.EntryCount);
        Assert.Empty(store.Contributions(UIPilotProperty.Background, UIValueSlot.Normal));
        Assert.False(store.TryGetWinner(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<int> winner));
        Assert.False(winner.IsSet);
    }

    [Fact]
    public void EntryCount_Grows_Once_Per_New_Property_Slot_Not_Per_Contribution()
    {
        UIResolvedPropertyStore store = new();

        store.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 1, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        Assert.Equal(1, store.EntryCount);

        store.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 2, UIValueResolutionSource.LocalValue(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        Assert.Equal(1, store.EntryCount);

        store.Set(UIPilotProperty.Margin, UIValueSlot.Whole, 3, UIValueResolutionSource.Animation(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        Assert.Equal(1, store.EntryCount);

        store.Set(UIPilotProperty.Padding, UIValueSlot.Whole, 1, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        Assert.Equal(2, store.EntryCount);

        store.Set(UIPilotProperty.Background, UIValueSlot.Normal, 1, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);
        Assert.Equal(3, store.EntryCount);
    }

    [Fact]
    public void Set_With_A_Different_Type_On_The_Same_Entry_Throws()
    {
        UIResolvedPropertyStore store = new();
        store.Set(UIPilotProperty.Foreground, UIValueSlot.Normal, 1, UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<int>.Default, out _, out _);

        Assert.Throws<InvalidOperationException>(() =>
            store.Set(UIPilotProperty.Foreground, UIValueSlot.Normal, "not an int", UIValueResolutionSource.Theme(UIInvalidationKind.None), EqualityComparer<string>.Default, out _, out _));
    }

    [Fact]
    public void HasInvalidation_Requires_All_Flags_HasAnyInvalidation_Requires_One()
    {
        UIResolvedValue<int> resolved = new(1, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

        Assert.True(resolved.HasInvalidation(UIInvalidationKind.Measure));
        Assert.False(resolved.HasInvalidation(UIInvalidationKind.Measure | UIInvalidationKind.Draw));
        Assert.True(resolved.HasAnyInvalidation(UIInvalidationKind.Measure | UIInvalidationKind.Draw));
        Assert.False(resolved.HasAnyInvalidation(UIInvalidationKind.Draw));
    }
}
