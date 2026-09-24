using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.DataBinding;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Styling;

/// <summary>ADR-0005/S8: maps a dotted CLR path rooted at an <see cref="MGElement"/> (the same shape used by
/// <see cref="Styling.UIResourceReferenceConfig.TargetPath"/> and <see cref="BindingConfig.TargetPath"/>,
/// already passed through <c>XAML.Element.BindingPathMappings</c>) onto one of the eight <see cref="UIPilotProperty"/>
/// keys plus a <see cref="UIValueSlot"/>, so a dynamic resource or a data binding targeting a pilot property can be
/// recorded as a tagged contribution (<see cref="UIValueSourceKind.DynamicResource"/> / <see cref="UIValueSourceKind.LocalBinding"/>)
/// instead of writing the CLR property directly through reflection.<para/>
/// A path that does not name a pilot property (or names one this resolver does not recognize) makes
/// <see cref="TryResolve"/> return <see langword="false"/>; callers fall back to their existing reflection-based
/// write in that case, so this resolver only ever narrows behavior for the eight known paths, never removes
/// support for anything else.</summary>
internal static class UIPilotPropertyResolver
{
    /// <summary>The <see cref="UIInvalidationKind"/> a tagged write of <paramref name="pilot"/> should carry,
    /// matching every other tagged write site for that pilot (see <c>MGElement.SetMargin</c>/<c>SetPadding</c>/
    /// <c>SetMinHeight</c>/<c>MGBorder.SetBorderThickness</c> vs. the Draw-only container/brush pilots).</summary>
    public static UIInvalidationKind KindOf(UIPilotProperty pilot) => pilot switch
    {
        UIPilotProperty.Margin or UIPilotProperty.Padding or UIPilotProperty.MinHeight or UIPilotProperty.BorderThickness
            => UIInvalidationKind.Measure | UIInvalidationKind.Arrange,
        _ => UIInvalidationKind.Draw,
    };

    /// <summary>Resolves <paramref name="dottedPath"/> (1 or 2 segments, e.g. <c>"Padding"</c> or
    /// <c>"BackgroundBrush.NormalValue"</c>) against <paramref name="root"/>. Returns <see langword="false"/>
    /// when <paramref name="root"/> is not an <see cref="MGElement"/>, the path does not name a pilot property,
    /// or (for <c>BorderBrush</c>/<c>BorderThickness</c>) the element has no border (<see cref="MGElement.GetBorder"/>
    /// is null).</summary>
    public static bool TryResolve(object root, string dottedPath, out MGElement owner, out UIPilotProperty pilot, out UIValueSlot slot)
    {
        owner = null;
        pilot = default;
        slot = UIValueSlot.Whole;

        if (root is not MGElement element || string.IsNullOrWhiteSpace(dottedPath))
            return false;

        var segments = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 0 or > 2)
            return false;

        var head = segments[0];
        var sub = segments.Length == 2 ? segments[1] : null;

        if (sub == null)
        {
            if (head == nameof(MGElement.Padding)) { owner = element; pilot = UIPilotProperty.Padding; return true; }
            if (head == nameof(MGElement.Margin)) { owner = element; pilot = UIPilotProperty.Margin; return true; }
            if (head == nameof(MGElement.MinHeight)) { owner = element; pilot = UIPilotProperty.MinHeight; return true; }
            if (head == "BorderBrush") { owner = ResolveBorder(element); pilot = UIPilotProperty.BorderBrush; return owner != null; }
            if (head == "BorderThickness") { owner = ResolveBorder(element); pilot = UIPilotProperty.BorderThickness; return owner != null; }
            if (head == nameof(MGElement.BackgroundBrush)) { owner = element; pilot = UIPilotProperty.Background; return true; }
            if (head == nameof(MGElement.DefaultTextForeground)) { owner = element; pilot = UIPilotProperty.DefaultTextForeground; return true; }
            if (head == "Foreground" && element is MGTextBlock) { owner = element; pilot = UIPilotProperty.Foreground; return true; }
            return false;
        }
        else
        {
            if (head == nameof(MGElement.BackgroundBrush) && TryParseSlot(sub, true, out slot)) { owner = element; pilot = UIPilotProperty.Background; return true; }
            if (head == nameof(MGElement.DefaultTextForeground) && TryParseSlot(sub, false, out slot)) { owner = element; pilot = UIPilotProperty.DefaultTextForeground; return true; }
            if (head == "Foreground" && element is MGTextBlock && TryParseSlot(sub, false, out slot)) { owner = element; pilot = UIPilotProperty.Foreground; return true; }
            return false;
        }
    }

    private static MGBorder ResolveBorder(MGElement element) => element is MGBorder border ? border : element.GetBorder();

    /// <summary><paramref name="allowFocusedColor"/> distinguishes the Background container (which has a
    /// <c>FocusedColor</c> sub-slot) from the two text-foreground containers (which do not).</summary>
    private static bool TryParseSlot(string sub, bool allowFocusedColor, out UIValueSlot slot)
    {
        switch (sub)
        {
            case "NormalValue": slot = UIValueSlot.Normal; return true;
            case "SelectedValue": slot = UIValueSlot.Selected; return true;
            case "DisabledValue": slot = UIValueSlot.Disabled; return true;
            case "FocusedValue": slot = UIValueSlot.Focused; return true;
            case "FocusedColor" when allowFocusedColor: slot = UIValueSlot.FocusedColor; return true;
            default: slot = UIValueSlot.Whole; return false;
        }
    }

    /// <summary>Attempts a tagged write of <paramref name="value"/> to (<paramref name="owner"/>, <paramref name="pilot"/>,
    /// <paramref name="slot"/>) via the matching internal setter (<c>SetMargin</c>, <c>SetBackgroundSlot</c>, ...).
    /// Returns <see langword="false"/> when <paramref name="value"/>'s runtime type does not match what that slot's
    /// setter accepts (or <paramref name="owner"/> is null / the wrong CLR type for the pilot, e.g. <c>Foreground</c>
    /// off a non-<see cref="MGTextBlock"/>) -- the caller should fall back to reflection in that case.</summary>
    public static bool TrySetTagged(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, object value, UIValueResolutionSource source)
    {
        if (owner == null)
            return false;

        switch (pilot)
        {
            case UIPilotProperty.Margin when value is Thickness marginValue:
                owner.SetMargin(marginValue, source);
                return true;
            case UIPilotProperty.Padding when value is Thickness paddingValue:
                owner.SetPadding(paddingValue, source);
                return true;
            case UIPilotProperty.MinHeight when value is int or null:
                owner.SetMinHeight((int?)value, source);
                return true;
            case UIPilotProperty.BorderBrush when value is IBorderBrush or null:
                owner.SetBorderBrushTagged((IBorderBrush)value, source);
                return true;
            case UIPilotProperty.BorderThickness when value is Thickness borderThicknessValue:
                owner.SetBorderThicknessTagged(borderThicknessValue, source);
                return true;

            case UIPilotProperty.Background when slot == UIValueSlot.Whole && value is VisualStateFillBrush wholeBrush:
                owner.SetBackground(wholeBrush, source);
                return true;
            case UIPilotProperty.Background when slot == UIValueSlot.FocusedColor && value is Color or null:
                owner.SetBackgroundFocusedColor((Color?)value, source);
                return true;
            case UIPilotProperty.Background when slot is UIValueSlot.Normal or UIValueSlot.Selected or UIValueSlot.Disabled or UIValueSlot.Focused
                                                 && value is IFillBrush or null:
                owner.SetBackgroundSlot(slot, (IFillBrush)value, source);
                return true;

            case UIPilotProperty.DefaultTextForeground when slot == UIValueSlot.Whole && value is VisualStateSetting<Color?> wholeDefaultForeground:
                owner.SetDefaultTextForeground(wholeDefaultForeground, source);
                return true;
            case UIPilotProperty.DefaultTextForeground when slot is UIValueSlot.Normal or UIValueSlot.Selected or UIValueSlot.Disabled or UIValueSlot.Focused
                                                            && value is Color or null:
                owner.SetDefaultTextForegroundSlot(slot, (Color?)value, source);
                return true;

            case UIPilotProperty.Foreground when owner is MGTextBlock wholeForegroundOwner && slot == UIValueSlot.Whole && value is VisualStateSetting<Color?> wholeForeground:
                wholeForegroundOwner.SetForeground(wholeForeground, source);
                return true;
            case UIPilotProperty.Foreground when owner is MGTextBlock slotForegroundOwner
                                                 && slot is UIValueSlot.Normal or UIValueSlot.Selected or UIValueSlot.Disabled or UIValueSlot.Focused
                                                 && value is Color or null:
                slotForegroundOwner.SetForegroundSlot(slot, (Color?)value, source);
                return true;

            default:
                return false;
        }
    }

    /// <summary>ADR-0016: typed overload of <see cref="TrySetTagged(MGElement, UIPilotProperty, UIValueSlot, object, UIValueResolutionSource)"/>
    /// for the three pilots whose tagged value is a value type (<see cref="Thickness"/>): <c>Margin</c>, <c>Padding</c>
    /// and <c>BorderThickness</c>. Taking <paramref name="value"/> as a plain <see cref="Thickness"/> instead of
    /// <see cref="object"/> lets a typed binding push (<see cref="DataBinding.TypedAccessorCache"/>) call this without
    /// boxing the value first. Same slot/owner rules as the <see cref="object"/> overload.</summary>
    /// <remarks>See <c>MGUI.Core.UI.DataBinding.TypedAccessorCache</c> for the compiled, boxing-free source read
    /// that supplies this overload's <paramref name="value"/>.</remarks>
    public static bool TrySetTagged(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, Thickness value, UIValueResolutionSource source)
    {
        if (owner == null)
            return false;

        switch (pilot)
        {
            case UIPilotProperty.Margin:
                owner.SetMargin(value, source);
                return true;
            case UIPilotProperty.Padding:
                owner.SetPadding(value, source);
                return true;
            case UIPilotProperty.BorderThickness:
                owner.SetBorderThicknessTagged(value, source);
                return true;
            default:
                return false;
        }
    }

    /// <summary>ADR-0016: typed overload for the one pilot whose tagged value is <see cref="int"/>?: <c>MinHeight</c>.
    /// See the <see cref="Thickness"/> overload's remarks.</summary>
    public static bool TrySetTagged(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, int? value, UIValueResolutionSource source)
    {
        if (owner == null || pilot != UIPilotProperty.MinHeight)
        {
            return false;
        }

        owner.SetMinHeight(value, source);
        return true;
    }

    /// <summary>ADR-0016: typed overload for the pilots whose tagged value is a <see cref="Color"/>?: the Background
    /// container's <c>FocusedColor</c> sub-slot, and the <c>Normal</c>/<c>Selected</c>/<c>Disabled</c>/<c>Focused</c>
    /// sub-slots of <c>DefaultTextForeground</c> and (on an <see cref="MGTextBlock"/>) <c>Foreground</c>. See the
    /// <see cref="Thickness"/> overload's remarks.</summary>
    public static bool TrySetTagged(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, Color? value, UIValueResolutionSource source)
    {
        if (owner == null)
        {
            return false;
        }

        switch (pilot)
        {
            case UIPilotProperty.Background when slot == UIValueSlot.FocusedColor:
                owner.SetBackgroundFocusedColor(value, source);
                return true;
            case UIPilotProperty.DefaultTextForeground when slot is UIValueSlot.Normal or UIValueSlot.Selected or UIValueSlot.Disabled or UIValueSlot.Focused:
                owner.SetDefaultTextForegroundSlot(slot, value, source);
                return true;
            case UIPilotProperty.Foreground when owner is MGTextBlock textBlockOwner
                                                 && slot is UIValueSlot.Normal or UIValueSlot.Selected or UIValueSlot.Disabled or UIValueSlot.Focused:
                textBlockOwner.SetForegroundSlot(slot, value, source);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Clears the contribution of <paramref name="kind"/> for (<paramref name="owner"/>, <paramref name="pilot"/>,
    /// <paramref name="slot"/>), falling back to the next-highest-precedence contribution (or the current CLR value,
    /// unnotified, if none remains) -- see <see cref="MGElement.ClearPilotSource"/>.</summary>
    public static void Clear(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, UIValueSourceKind kind)
        => owner?.ClearPilotSource(pilot, slot, kind);
}