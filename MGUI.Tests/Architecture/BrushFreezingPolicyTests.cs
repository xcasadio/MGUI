using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Tests.Animation;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

/// <summary>Covers the freezing and notification policy added by ADR-0009 (W4): a theme's brushes -- direct properties, the
/// nested settings groups (<see cref="MGTheme.Window"/>, <see cref="MGTheme.Graph"/>, ...), <see cref="MGTheme.GetBackgroundBrush"/> --
/// and a <c>StaticResource</c> brush end up frozen and shared by reference; a XAML inline brush stays unfrozen and element-owned;
/// mutating an unfrozen slot or border brush is observed through a <see cref="System.ComponentModel.INotifyPropertyChanged"/>
/// subscription that exists only for an unfrozen brush (never for a theme default). Deliberately does not reuse
/// <see cref="MGTheme.FreezeThemeValue(object)"/>'s own recursive walk for the completeness probe below: the test re-implements an
/// independent reflection walk so it actually exercises the OUTCOME (every reachable brush frozen), not the same code path twice.</summary>
public class BrushFreezingPolicyTests
{
    #region Theme completeness (ACCEPTANCE 2)

    /// <summary>Recursively enumerates every public property reachable from <paramref name="Owner"/> and yields the brush-shaped
    /// leaves: a plain <see cref="IUIFreezable"/> brush, and each non-null slot of a <see cref="VisualStateFillBrush"/> (the container
    /// itself is never frozen, W3 -- not a leaf here). Recurses into a <see cref="ThemeManagedGetter{TDataType}"/> wrapper (via
    /// <c>GetValue(false)</c>, no clone), a settings-group instance (any reference type under <c>MGUI.Core.UI</c> whose name starts
    /// with <c>MGTheme</c>), and an <see cref="IEnumerable"/> (<see cref="MGTheme.ListBoxItemAlternatingRowBackgrounds"/>).</summary>
    private static IEnumerable<IUIFreezable> EnumerateReachableBrushes(object Owner, HashSet<object> Visited = null)
    {
        Visited ??= new(ReferenceEqualityComparer.Instance);
        if (Owner == null || !Visited.Add(Owner))
        {
            yield break;
        }

        if (Owner is IUIFreezable Direct)
        {
            yield return Direct;
            yield break;
        }

        if (Owner is VisualStateFillBrush Container)
        {
            foreach (var Slot in new[] { Container.NormalValue, Container.SelectedValue, Container.FocusedValue, Container.DisabledValue })
            {
                foreach (var Found in EnumerateReachableBrushes(Slot, Visited))
                {
                    yield return Found;
                }
            }
            if (Container.HasCheckedValue)
            {
                foreach (var Found in EnumerateReachableBrushes(Container.CheckedValue, Visited))
                {
                    yield return Found;
                }
            }
            yield break;
        }

        if (Owner is IEnumerable Sequence and not string)
        {
            foreach (var Item in Sequence)
            {
                foreach (var Found in EnumerateReachableBrushes(Item, Visited))
                {
                    yield return Found;
                }
            }
            yield break;
        }

        Type OwnerType = Owner.GetType();
        bool IsThemeManagedWrapper = OwnerType.IsGenericType && OwnerType.GetGenericTypeDefinition() == typeof(ThemeManagedGetter<>);
        bool IsSettingsGroup = OwnerType.Namespace == typeof(MGTheme).Namespace
            && (OwnerType.Name.StartsWith("MGTheme", StringComparison.Ordinal) || OwnerType == typeof(ThemeFontSettings));
        if (!IsThemeManagedWrapper && !IsSettingsGroup && OwnerType != typeof(MGTheme))
        {
            yield break;
        }

        if (IsThemeManagedWrapper)
        {
            object Wrapped = OwnerType.GetMethod("GetValue").Invoke(Owner, new object[] { false });
            foreach (var Found in EnumerateReachableBrushes(Wrapped, Visited))
            {
                yield return Found;
            }
            yield break;
        }

        foreach (PropertyInfo Property in OwnerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            foreach (var Found in EnumerateReachableBrushes(Property.GetValue(Owner), Visited))
            {
                yield return Found;
            }
        }
    }

    private static MGTheme BuildTheme() => new(MGTheme.BuiltInTheme.Dark_Blue, "Arial");

    [Fact]
    public void BuiltTheme_Every_Reachable_Brush_Is_Frozen()
    {
        MGTheme theme = BuildTheme();

        List<IUIFreezable> brushes = EnumerateReachableBrushes(theme).ToList();
        foreach (MGElementType elementType in Enum.GetValues(typeof(MGElementType)))
        {
            brushes.AddRange(EnumerateReachableBrushes(theme.GetBackgroundBrush(elementType)));
        }

        Assert.True(brushes.Count > 20, $"Expected a built-in theme to reach well over 20 brushes, found {brushes.Count}.");
        foreach (IUIFreezable brush in brushes)
        {
            if (brush.CanFreeze)
            {
                Assert.True(brush.IsFrozen, $"{brush.GetType().Name} reachable from a built theme is not frozen.");
            }
        }
    }

    [Fact]
    public void Theme_Copy_Shares_The_Frozen_Background_And_Direct_Brushes()
    {
        MGTheme theme = BuildTheme();
        MGTheme copy = theme.Copy();

        Assert.Same(theme.GetBackgroundBrush(MGElementType.Button).NormalValue, copy.GetBackgroundBrush(MGElementType.Button).NormalValue);
        Assert.Same(theme.TreeViewBorderBrush, copy.TreeViewBorderBrush);
        Assert.True(((IUIFreezable)theme.TreeViewBorderBrush).IsFrozen);
    }

    /// <summary>A brush handed to a <see cref="ThemeManagedGetter{TDataType}"/>-wrapped property (<see cref="MGTheme.SliderForeground"/>
    /// and the dozen others like it) is frozen the instant it is assigned -- the wrapper's <c>Value</c> setter is the one place every
    /// such property funnels through, so there is nothing to sweep afterwards.</summary>
    [Fact]
    public void Assigning_An_Unfrozen_Brush_To_A_ThemeManaged_Property_Freezes_It_Immediately()
    {
        MGTheme theme = new("Arial");
        MGSolidFillBrush managed = new(Color.Chartreuse);
        Assert.False(managed.IsFrozen);

        theme.SliderForeground.Value = managed;

        Assert.True(managed.IsFrozen);
    }

    /// <summary>A plain auto-property brush (<see cref="MGTheme.TreeViewBorderBrush"/>, and every brush-typed property of a settings
    /// group such as <see cref="MGTheme.Window"/>) freezes its incoming brush the instant it is assigned, exactly like a
    /// <see cref="ThemeManagedGetter{TDataType}"/>-wrapped property: the fix round for W4 (verifier P2) replaced every remaining plain
    /// brush-typed auto-property with a hand-written setter that calls <see cref="MGTheme.FreezeThemeValue(object)"/> before storing, so
    /// a theme can never hold an unfrozen, shared-by-reference brush regardless of whether <see cref="MGTheme.FreezeBrushes"/> ever runs
    /// again afterwards (it still runs at the end of every <see cref="XAML.ThemeDefinitionBuilder.Build"/> and is now a no-op sweep for
    /// brushes assigned this way).</summary>
    [Fact]
    public void Assigning_An_Unfrozen_Brush_To_A_Plain_Theme_Property_Freezes_It_Immediately()
    {
        MGTheme theme = new("Arial");
        MGSolidFillBrush brush = new(Color.HotPink);
        MGUniformBorderBrush border = brush.AsUniformBorderBrush();

        theme.TreeViewBorderBrush = border;
        Assert.True(border.IsFrozen);

        MGSolidFillBrush windowBorderBrush = new(Color.DeepSkyBlue);
        theme.Window.BorderBrush = windowBorderBrush.AsUniformBorderBrush();
        Assert.True(((IUIFreezable)theme.Window.BorderBrush).IsFrozen);
    }

    #endregion

    #region Sharing (ACCEPTANCE 3)

    [Fact]
    public void Two_Buttons_On_The_Built_In_Theme_Resolve_The_Same_Background_Slot_Instance()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        Assert.Same(scene.Top.BackgroundBrush.NormalValue, scene.Bottom.BackgroundBrush.NormalValue);
        Assert.True(((IUIFreezable)scene.Top.BackgroundBrush.NormalValue).IsFrozen);
    }

    [Fact]
    public void A_StaticResource_Brush_Is_Frozen_And_Shared_By_Two_Consumers()
    {
        MGResources resources = new(new MGTheme("Arial"));
        MGSolidFillBrush brush = new(Color.Aqua);
        Assert.False(brush.IsFrozen);

        resources.AddStaticResource("Accent", brush);

        Assert.True(brush.IsFrozen);
        Assert.True(resources.TryGetStaticResource("Accent", out object first));
        Assert.True(resources.TryGetStaticResource("Accent", out object second));
        Assert.Same(first, second);
        Assert.Same(brush, first);
    }

    #endregion

    #region Notification (ACCEPTANCE 4 and 5)

    /// <summary>Reflects on the compiler-generated backing field of the <see cref="INotifyPropertyChanged.PropertyChanged"/> auto-event
    /// -- declared on <see cref="MGUI.Shared.Helpers.ViewModelBase"/>, so the search walks up from <paramref name="source"/>'s own type
    /// rather than assuming it is the declaring type.</summary>
    private static int CountSubscribers(INotifyPropertyChanged source)
    {
        for (Type type = source.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField("PropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                Delegate handler = (Delegate)field.GetValue(source);
                return handler?.GetInvocationList().Length ?? 0;
            }
        }

        throw new InvalidOperationException($"{source.GetType().Name} has no PropertyChanged backing field.");
    }

    [Fact]
    public void ElementOnThemeDefaults_Has_Zero_Subscribers_On_Every_Background_Slot()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        var slots = new (string Name, IFillBrush Brush)[]
        {
            (nameof(VisualStateFillBrush.NormalValue), scene.Top.BackgroundBrush.NormalValue),
            (nameof(VisualStateFillBrush.SelectedValue), scene.Top.BackgroundBrush.SelectedValue),
            (nameof(VisualStateFillBrush.FocusedValue), scene.Top.BackgroundBrush.FocusedValue),
            (nameof(VisualStateFillBrush.DisabledValue), scene.Top.BackgroundBrush.DisabledValue),
        };

        foreach (var (name, brush) in slots)
        {
            if (brush is INotifyPropertyChanged notifier)
            {
                bool isFrozen = brush is IUIFreezable { IsFrozen: true };
                Assert.True(0 == CountSubscribers(notifier), $"{name} ({brush.GetType().Name}, frozen={isFrozen}) has {CountSubscribers(notifier)} subscriber(s).");
            }
        }
    }

    [Fact]
    public void InlineUnfrozenBackgroundSlot_Has_Exactly_One_Subscriber_And_Zero_After_Replacement()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGSolidFillBrush brush = new(Color.Red);

        scene.Top.BackgroundBrush.NormalValue = brush;
        Assert.Equal(1, CountSubscribers(brush));

        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Blue);
        Assert.Equal(0, CountSubscribers(brush));
    }

    [Fact]
    public void MutatingAnInlineBackgroundSlot_RepaintsWithoutRewritingTheStore()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGSolidFillBrush brush = new(Color.Red);
        scene.Top.BackgroundBrush.NormalValue = brush;
        scene.Frames(1);

        var beforeFrame = scene.Draw();
        Assert.Contains(beforeFrame.FillRectangleCalls, call => call.Color == Color.Red);

        var contributionsBefore = scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count;

        brush.Color = Color.Blue;
        scene.Frames(1);

        var contributionsAfter = scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count;
        Assert.Equal(contributionsBefore, contributionsAfter);

        var afterFrame = scene.Draw();
        Assert.Contains(afterFrame.FillRectangleCalls, call => call.Color == Color.Blue);
    }

    [Fact]
    public void InlineUnfrozenBorderBrush_Subscribes_And_Relays_Mutation_As_BorderBrushChanged()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGBorder borderElement = scene.Top.GetBorder();
        MGDockedBorderBrush border = new(new MGSolidFillBrush(Color.Red), new MGSolidFillBrush(Color.Red), new MGSolidFillBrush(Color.Red), new MGSolidFillBrush(Color.Red));

        borderElement.BorderBrush = border;
        Assert.Equal(1, CountSubscribers(border));

        int notificationCount = 0;
        borderElement.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MGBorder.BorderBrush))
            {
                notificationCount++;
            }
        };

        border.Left = new MGSolidFillBrush(Color.Blue);
        Assert.Equal(1, notificationCount);

        borderElement.BorderBrush = MGUniformBorderBrush.Black; // frozen palette instance
        Assert.Equal(0, CountSubscribers(border));
    }

    #endregion
}
