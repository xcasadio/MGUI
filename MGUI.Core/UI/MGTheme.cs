using MGUI.Core.UI.Brushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Reflection;
using MGUI.Core.UI.Brushes.BorderBrushes;

namespace MGUI.Core.UI;

/// <summary>A wrapper class for the given <typeparamref name="TDataType"/> that controls the getter function, typically to return a copy of the value (to mimic treating class implementations as value-types)</summary>
public class ThemeManagedGetter<TDataType>
    where TDataType : class, ICloneable
{
    private TDataType _Value;
    /// <summary>This property intentionally has no getter (use <see cref="GetValue(bool)"/>).<para/>
    /// Freezing policy (ADR-0009, W4): a brush handed to a theme through this wrapper is frozen in place
    /// (<see cref="MGTheme.FreezeThemeValue(object)"/>) so it becomes safely shareable; a <see cref="VisualStateFillBrush"/>
    /// container stays unfrozen itself (W3, confirmed decision) but each of its non-null slot brushes is frozen instead.</summary>
    public TDataType Value
    {
        set
        {
            MGTheme.FreezeThemeValue(value);
            _Value = value;
        }
    }

    /// <param name="Copy">If true, a copy of the underlying <see cref="Value"/> will be returned. If false, a direct reference to the underlying <see cref="Value"/> will be returned.<para/>
    /// Some implementations of <typeparamref name="TDataType"/> are structs (value-types) rather than classes (reference-types),<br/>
    /// so for consistency, recommended to always retrieve a copy, essentially treating all objects as value-types.<para/>
    /// ADR-0009/W4: when the stored value is itself a frozen brush (<see cref="IUIFreezable"/>), the frozen instance is
    /// returned directly regardless of <paramref name="Copy"/> -- it is immutable and safe to share, cloning it would only
    /// waste an allocation. A <see cref="VisualStateFillBrush"/>/<see cref="VisualStateColorBrush"/> container is never
    /// itself frozen (W3), so it keeps being cloned as before.</param>
    public TDataType GetValue(bool Copy = true)
        => _Value is IUIFreezable { IsFrozen: true } ? _Value : (Copy ? _Value?.Clone() as TDataType : _Value);

    public ThemeManagedGetter() : this(null) { }
    public ThemeManagedGetter(TDataType Value) { this.Value = Value; }
}

public class ThemeManagedFillBrush : ThemeManagedGetter<IFillBrush>
{
    public ThemeManagedFillBrush(IFillBrush Value) : base(Value) { }
}

public class ThemeManagedVisualStateFillBrush : ThemeManagedGetter<VisualStateFillBrush>
{
    public ThemeManagedVisualStateFillBrush(VisualStateFillBrush Value) : base(Value) { }
}

public class ThemeManagedVisualStateColorBrush : ThemeManagedGetter<VisualStateColorBrush>
{
    public ThemeManagedVisualStateColorBrush(VisualStateColorBrush Value) : base(Value) { }
}

public class MGThemeWindowSettings
{
    public Thickness Padding { get; set; } = new(5);
    public Thickness BorderThickness { get; set; } = new(2);
    public Thickness ChromelessPadding { get; set; } = new(0);
    public Thickness ChromelessBorderThickness { get; set; } = new(0);
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    public Thickness TitleBarPadding { get; set; } = new(2);
    public int TitleBarMinHeight { get; set; } = 24;
    private VisualStateFillBrush _CloseButtonBackground = new(Color.Crimson.AsFillBrush() * 0.5f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
    public VisualStateFillBrush CloseButtonBackground { get => _CloseButtonBackground; set { MGTheme.FreezeThemeValue(value); _CloseButtonBackground = value; } }
    private IBorderBrush _CloseButtonBorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush CloseButtonBorderBrush { get => _CloseButtonBorderBrush; set { MGTheme.FreezeThemeValue(value); _CloseButtonBorderBrush = value; } }
    public Thickness CloseButtonBorderThickness { get; set; } = new(1);
    public Thickness CloseButtonMargin { get; set; } = new(1);
    public Thickness CloseButtonPadding { get; set; } = new(0);
    public int CloseButtonMinWidth { get; set; } = 16;
    public int CloseButtonMinHeight { get; set; } = 16;
    public Thickness TitleTextMargin { get; set; } = new(4, 0);
    public Thickness TitleTextPadding { get; set; } = new(0);
    public VisualStateSetting<Color?> TitleTextForeground { get; set; } = new(Color.White, Color.White, Color.White);
}

public class MGThemeOverlaySettings
{
    public Thickness HostPadding { get; set; } = new(4);
    public Thickness Padding { get; set; } = new(5);
    public Thickness BorderThickness { get; set; } = new(1);
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    private VisualStateFillBrush _CloseButtonBackground = new(Color.Crimson.AsFillBrush() * 0.8f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
    public VisualStateFillBrush CloseButtonBackground { get => _CloseButtonBackground; set { MGTheme.FreezeThemeValue(value); _CloseButtonBackground = value; } }
    private IBorderBrush _CloseButtonBorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush CloseButtonBorderBrush { get => _CloseButtonBorderBrush; set { MGTheme.FreezeThemeValue(value); _CloseButtonBorderBrush = value; } }
    public Thickness CloseButtonBorderThickness { get; set; } = new(1);
    public Thickness CloseButtonPadding { get; set; } = new(0);
    public int CloseButtonMinWidth { get; set; } = 16;
    public int CloseButtonMinHeight { get; set; } = 16;
}

public class MGThemeContextMenuSettings
{
    public Thickness Padding { get; set; } = new(0);
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Gray;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    public Thickness BorderThickness { get; set; } = new(1);
}

public class MGThemeContextMenuItemSettings
{
    public Thickness HeaderMargin { get; set; } = new(0, 0, 5, 0);
    private VisualStateFillBrush _HeaderBackground = new(null);
    public VisualStateFillBrush HeaderBackground { get => _HeaderBackground; set { MGTheme.FreezeThemeValue(value); _HeaderBackground = value; } }
    public Thickness ShortcutMargin { get; set; } = new(18, 0, 0, 0);
    public VisualStateSetting<Color?> ShortcutForeground { get; set; } = new(Color.LightGray, Color.LightGray, Color.LightGray);
    public Thickness SubmenuArrowMargin { get; set; } = new(0, 5, 8, 5);
}

public class MGThemeListBoxSettings
{
    public int MinHeight { get; set; } = 30;
    private VisualStateFillBrush _OuterBackground = new(SolidFillBrushes.Black);
    public VisualStateFillBrush OuterBackground { get => _OuterBackground; set { MGTheme.FreezeThemeValue(value); _OuterBackground = value; } }
    public Thickness TitlePadding { get; set; } = new(6, 3);
    /// <summary>Padding of the border that wraps each item (backlog task 14). Default: <see cref="MGUI.Core.UI.Styling.MGControlTemplateCatalog.DefaultListBoxItemPadding"/>.</summary>
    public Thickness ItemPadding { get; set; } = MGUI.Core.UI.Styling.MGControlTemplateCatalog.DefaultListBoxItemPadding;
    /// <summary>Padding of the default item content, the text block that <see cref="MGUI.Core.UI.Styling.MGControlTemplateCatalog.CreateDefaultListBoxItemContent{TItemType}(MGWindow, TItemType)"/>
    /// creates (backlog task 14). Default: <see cref="MGUI.Core.UI.Styling.MGControlTemplateCatalog.DefaultListBoxItemContentPadding"/>.</summary>
    public Thickness ItemContentPadding { get; set; } = MGUI.Core.UI.Styling.MGControlTemplateCatalog.DefaultListBoxItemContentPadding;
    public VisualStateSetting<Color?> TitleForeground { get; set; } = new(Color.White, Color.White, Color.White);
    private IBorderBrush _TitleBorderBrush = SolidFillBrushes.Black.AsUniformBorderBrush();
    public IBorderBrush TitleBorderBrush { get => _TitleBorderBrush; set { MGTheme.FreezeThemeValue(value); _TitleBorderBrush = value; } }
    public Thickness TitleBorderThickness { get; set; } = new(1, 1, 1, 0);
    private IBorderBrush _InnerBorderBrush = SolidFillBrushes.Black.AsUniformBorderBrush();
    public IBorderBrush InnerBorderBrush { get => _InnerBorderBrush; set { MGTheme.FreezeThemeValue(value); _InnerBorderBrush = value; } }
    public Thickness InnerBorderThickness { get; set; } = new(1);
    public Thickness ScrollViewerPadding { get; set; } = new(0);
    private IBorderBrush _ItemsPanelBorderBrush = SolidFillBrushes.Black.AsUniformBorderBrush();
    public IBorderBrush ItemsPanelBorderBrush { get => _ItemsPanelBorderBrush; set { MGTheme.FreezeThemeValue(value); _ItemsPanelBorderBrush = value; } }
    public Thickness ItemsPanelBorderThickness { get; set; } = new(1);
}

public class MGThemeListViewSettings
{
    public VisualStateSetting<Color?> HeaderForeground { get; set; } = new(Color.White, Color.White, Color.White);
    private IFillBrush _GridLineBrush = SolidFillBrushes.Black;
    public IFillBrush GridLineBrush { get => _GridLineBrush; set { MGTheme.FreezeThemeValue(value); _GridLineBrush = value; } }
}

public class MGThemePropertyGridSettings
{
    public Thickness Padding { get; set; } = new(0);
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    public Thickness BorderThickness { get; set; } = new(1);
    public Thickness ScrollViewerPadding { get; set; } = new(0);
    public int CategoriesSpacing { get; set; } = 4;
    private VisualStateFillBrush _CategoryHeaderBackground = new(new MGSolidFillBrush(Color.Black * 0.35f));
    public VisualStateFillBrush CategoryHeaderBackground { get => _CategoryHeaderBackground; set { MGTheme.FreezeThemeValue(value); _CategoryHeaderBackground = value; } }
    public VisualStateColorBrush CategoryHeaderForeground { get; set; } = new(Color.White);
    public Thickness CategoryHeaderPadding { get; set; } = new(8, 4);
    public int CategoryHeaderMinHeight { get; set; } = 24;
    public Color CategoryArrowColor { get; set; } = Color.White;
    public Thickness RowPadding { get; set; } = new(8, 4);
    public int RowsSpacing { get; set; } = 0;
    private IFillBrush _RowSeparatorBrush = new MGSolidFillBrush(Color.Black * 0.35f);
    public IFillBrush RowSeparatorBrush { get => _RowSeparatorBrush; set { MGTheme.FreezeThemeValue(value); _RowSeparatorBrush = value; } }
    private IBorderBrush _InvalidEditorBorderBrush = new MGSolidFillBrush(Color.OrangeRed).AsUniformBorderBrush();
    public IBorderBrush InvalidEditorBorderBrush { get => _InvalidEditorBorderBrush; set { MGTheme.FreezeThemeValue(value); _InvalidEditorBorderBrush = value; } }
}

public class MGThemeComboBoxSettings
{
    public Thickness Padding { get; set; } = new(4, 2, 4, 2);
    public int MinHeight { get; set; } = 26;
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    public Thickness DropdownArrowMargin { get; set; } = new(6, 0, 4, 0);
    public int DropdownMinWidth { get; set; } = 100;
    public Thickness DropdownBorderThickness { get; set; } = new(1);
    private IBorderBrush _DropdownBorderBrush = MGUniformBorderBrush.Gray;
    public IBorderBrush DropdownBorderBrush { get => _DropdownBorderBrush; set { MGTheme.FreezeThemeValue(value); _DropdownBorderBrush = value; } }
    public Thickness DropdownPadding { get; set; } = new(0);
    public Thickness DropdownScrollViewerPadding { get; set; } = new(0);
    public int DropdownItemsSpacing { get; set; } = 0;
    /// <summary>Padding of each row of the dropdown (backlog task 14). Default: <see cref="MGUI.Core.UI.Styling.MGControlTemplateCatalog.DefaultComboBoxDropdownItemPadding"/>.</summary>
    public Thickness DropdownItemPadding { get; set; } = MGUI.Core.UI.Styling.MGControlTemplateCatalog.DefaultComboBoxDropdownItemPadding;
}

/// <summary>The density of <see cref="MGToolTip"/>s (backlog task 14): the defaults the <c>ToolTip.Default</c> template used to hard-code.
/// The draw offset and the text foreground stay on <see cref="MGTheme.ToolTipOffset"/> and <see cref="MGTheme.ToolTipTextForeground"/>.</summary>
public class MGThemeToolTipSettings
{
    public Thickness Padding { get; set; } = new(6, 3);
    public Thickness BorderThickness { get; set; } = new(2);
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    public int MinWidth { get; set; } = 10;
    public int MinHeight { get; set; } = 10;
}

/// <summary>The density of <see cref="MGTextBox"/>es (backlog task 14): the defaults the <c>TextBox.Default</c> template used to hard-code.
/// The selection colors stay on the <c>MGTheme.TextBox*Selection*</c> properties.</summary>
public class MGThemeTextBoxSettings
{
    public Thickness Padding { get; set; } = new(6, 1, 6, 1);
    public int MinHeight { get; set; } = 24;
}

/// <summary>The density of <see cref="MGNumericUpDown"/>s (backlog task 14): the defaults the <c>NumericUpDown.Default</c> template used to hard-code.</summary>
public class MGThemeNumericUpDownSettings
{
    public Thickness Padding { get; set; } = new(6, 2, 6, 2);
    public int MinHeight { get; set; } = 28;
    /// <summary>Width of the column that holds the two spinner buttons.</summary>
    public int SpinnerWidth { get; set; } = 24;
    /// <summary>Minimum width of each spinner button.</summary>
    public int SpinnerMinWidth { get; set; } = 22;
}

/// <summary>The interaction timings of a theme (ADR-0007, decision 5): the transitions the controls that opt in (<see cref="MGButton"/>,
/// <see cref="MGToggleButton"/>) attach on themselves, <c>RenderScale</c> over the hover timing and <c>Background.Overlay</c> over the press timing.
/// Easings are names known to <see cref="Animation.Easing.UIEasing"/>. The focus timing is reserved: no control reads it yet.
/// A change is <c>RenderOnly</c> (<see cref="Styling.UIThemeValueInvalidation"/>): a duration never touches the layout.</summary>
public class MGThemeAnimationSettings
{
    /// <summary>When false (the default of every built-in theme, so an untouched button keeps costing nothing, ADR-0006), the controls that opt in
    /// attach no theme transition and remove the ones they attached; a transition the application attached itself is never touched.</summary>
    public bool Enabled { get; set; } = false;
    public TimeSpan HoverDuration { get; set; } = TimeSpan.FromMilliseconds(120);
    public TimeSpan PressDuration { get; set; } = TimeSpan.FromMilliseconds(80);
    public TimeSpan FocusDuration { get; set; } = TimeSpan.FromMilliseconds(120);
    public string HoverEasing { get; set; } = "CubicOut";
    public string PressEasing { get; set; } = "CubicOut";
    public string FocusEasing { get; set; } = "CubicOut";
}

public class MGThemeTreeViewTemplateSettings
{
    public Thickness ScrollViewerPadding { get; set; } = new(0);
    public Thickness ItemsPanelPadding { get; set; } = new(0);
    public int ItemsPanelSpacing { get; set; } = 0;
}

public class MGThemeTabControlSettings
{
    public Thickness Padding { get; set; } = new(12);
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    public Thickness BorderThickness { get; set; } = new(1);
    public int HeadersSpacing { get; set; } = 0;
    /// <summary>Padding of the selected tab header when the headers are on top or bottom (backlog task 14).</summary>
    public Thickness SelectedHeaderPadding { get; set; } = new(8, 5, 8, 5);
    /// <summary>Padding of an unselected tab header when the headers are on top or bottom (backlog task 14).</summary>
    public Thickness UnselectedHeaderPadding { get; set; } = new(8, 3, 8, 3);
    /// <summary>Padding of every tab header, selected or not, when the headers are on the left or the right (backlog task 14).</summary>
    public Thickness SideHeaderPadding { get; set; } = new(6, 5, 6, 5);
}

public class MGThemeGraphSettings
{
    public Thickness Padding { get; set; } = new(0);
    private IBorderBrush _BorderBrush = MGUniformBorderBrush.Black;
    public IBorderBrush BorderBrush { get => _BorderBrush; set { MGTheme.FreezeThemeValue(value); _BorderBrush = value; } }
    public Thickness BorderThickness { get; set; } = new(1);
    private VisualStateFillBrush _CanvasBackground = new(new MGSolidFillBrush(new Color(18, 22, 26)));
    public VisualStateFillBrush CanvasBackground { get => _CanvasBackground; set { MGTheme.FreezeThemeValue(value); _CanvasBackground = value; } }
    private IFillBrush _GridLineBrush = new MGSolidFillBrush(Color.White * 0.08f);
    public IFillBrush GridLineBrush { get => _GridLineBrush; set { MGTheme.FreezeThemeValue(value); _GridLineBrush = value; } }
    private IFillBrush _MajorGridLineBrush;
    public IFillBrush MajorGridLineBrush { get => _MajorGridLineBrush; set { MGTheme.FreezeThemeValue(value); _MajorGridLineBrush = value; } }
    private IFillBrush _EdgeBrush = new MGSolidFillBrush(new Color(128, 180, 255));
    public IFillBrush EdgeBrush { get => _EdgeBrush; set { MGTheme.FreezeThemeValue(value); _EdgeBrush = value; } }
    private IFillBrush _SelectedEdgeBrush;
    public IFillBrush SelectedEdgeBrush { get => _SelectedEdgeBrush; set { MGTheme.FreezeThemeValue(value); _SelectedEdgeBrush = value; } }
    private IBorderBrush _NodeBorderBrush = new MGSolidFillBrush(Color.Black * 0.7f).AsUniformBorderBrush();
    public IBorderBrush NodeBorderBrush { get => _NodeBorderBrush; set { MGTheme.FreezeThemeValue(value); _NodeBorderBrush = value; } }
    public Thickness NodeBorderThickness { get; set; } = new(1);
    private IBorderBrush _NodeSelectedBorderBrush = new MGSolidFillBrush(new Color(94, 170, 255)).AsUniformBorderBrush();
    public IBorderBrush NodeSelectedBorderBrush { get => _NodeSelectedBorderBrush; set { MGTheme.FreezeThemeValue(value); _NodeSelectedBorderBrush = value; } }
    public Thickness NodeSelectedBorderThickness { get; set; } = new(2);
    private VisualStateFillBrush _NodeHeaderBackground = new(new MGSolidFillBrush(new Color(36, 46, 60)));
    public VisualStateFillBrush NodeHeaderBackground { get => _NodeHeaderBackground; set { MGTheme.FreezeThemeValue(value); _NodeHeaderBackground = value; } }
    public VisualStateColorBrush NodeHeaderForeground { get; set; } = new(Color.White);
    private VisualStateFillBrush _NodeBodyBackground = new(new MGSolidFillBrush(new Color(26, 31, 38)));
    public VisualStateFillBrush NodeBodyBackground { get => _NodeBodyBackground; set { MGTheme.FreezeThemeValue(value); _NodeBodyBackground = value; } }
    private IFillBrush _PortBackground = new MGSolidFillBrush(new Color(92, 140, 210));
    public IFillBrush PortBackground { get => _PortBackground; set { MGTheme.FreezeThemeValue(value); _PortBackground = value; } }
    public VisualStateColorBrush PortForeground { get; set; } = new(Color.White);
    private VisualStateFillBrush _CommentBackground = new(new MGSolidFillBrush(new Color(62, 54, 30) * 0.9f));
    public VisualStateFillBrush CommentBackground { get => _CommentBackground; set { MGTheme.FreezeThemeValue(value); _CommentBackground = value; } }
    private IBorderBrush _CommentBorderBrush = new MGSolidFillBrush(new Color(222, 180, 80)).AsUniformBorderBrush();
    public IBorderBrush CommentBorderBrush { get => _CommentBorderBrush; set { MGTheme.FreezeThemeValue(value); _CommentBorderBrush = value; } }
}

public class ThemeFontSettings
{
    /// <summary>The default fontsize for content inside an <see cref="MGContextMenu"/>, such as <see cref="MGContextMenuButton"/> and <see cref="MGContextMenuToggle"/></summary>
    public int ContextMenuFontSize { get; set; } = 10;

    public int SmallFontSize { get; set; } = 10;
    public int MediumFontSize { get; set; } = 12;
    public int LargeFontSize { get; set; } = 14;

    public int DefaultFontSize { get; set; } = 11;

    /// <summary>Changes all font sizes by the given <paramref name="Offset"/></summary>
    public void AdjustAllFontSizes(int Offset)
    {
        ContextMenuFontSize += Offset;
        SmallFontSize += Offset;
        MediumFontSize += Offset;
        LargeFontSize += Offset;
        DefaultFontSize += Offset;
    }

    /// <summary>If true, <see cref="MGTextBlock"/> will attempt to draw text with a scale that most closely results in the desired font size.<br/>
    /// If false, <see cref="MGTextBlock"/> may choose a slightly different font size that approximates the exact size, but results in better scaling results.<para/>
    /// For example, if you have SpriteFonts for these font sizes: 8, 10, 12, and you wanted to use font size = 19<br/>
    /// If <see cref="UseExactScale"/> is true: <see cref="MGTextBlock"/> would choose size=10, scale=1.9<br/>
    /// If <see cref="UseExactScale"/> is false: <see cref="MGTextBlock"/> would choose size=10, scale=2.0, preferring to scale by values such as 0.25, 0.5, 1.0, 2.0 etc<para/>
    /// Default value: false</summary>
    public bool UseExactScale { get; set; } = false;

    /// <summary>The name of the font that should be used by default in <see cref="MGTextBlock"/>s when no font family is explicitly specified.<para/>
    /// If null, uses <see cref="FontManager.DefaultFontFamily"/> instead.<para/>
    /// EX: "Arial". If not null, the <see cref="FontManager"/> must contain a <see cref="FontSet"/> with <see cref="FontSet.Name"/> that matches this value.</summary>
    public string DefaultFontFamily { get; set; }

    /// <summary>The fallback value to use for <see cref="MGTextBlock.ShadowOffset"/> when <see cref="MGTextBlock.ShadowOffset"/> is null.</summary>
    public Point DefaultFontShadowOffset { get; set; } = new(1, 1);
    /// <summary>The fallback value to use for <see cref="MGTextBlock.ShadowColor"/> when <see cref="MGTextBlock.ShadowColor"/> is null.</summary>
    public Color DefaultFontShadowColor { get; set; } = Color.Black;

    public ThemeFontSettings(string FontFamily)
    {
        DefaultFontFamily = FontFamily;
    }
}

public class MGThemeDockingSettings
{
    private IFillBrush _TabNormalBackground;
    public IFillBrush TabNormalBackground { get => _TabNormalBackground; set { MGTheme.FreezeThemeValue(value); _TabNormalBackground = value; } }
    private IFillBrush _TabHoverBackground;
    public IFillBrush TabHoverBackground { get => _TabHoverBackground; set { MGTheme.FreezeThemeValue(value); _TabHoverBackground = value; } }
    private IFillBrush _TabActiveBackground;
    public IFillBrush TabActiveBackground { get => _TabActiveBackground; set { MGTheme.FreezeThemeValue(value); _TabActiveBackground = value; } }
    public Color TabActiveAccentColor { get; set; }
    public Color TabHoverAccentColor { get; set; }
    public Color TabActiveTextColor { get; set; }
    public Color TabInactiveTextColor { get; set; }
    public Color TabActiveIconColor { get; set; }
    public Color TabInactiveIconColor { get; set; }

    private IFillBrush _AutoHideDrawerBackground;
    public IFillBrush AutoHideDrawerBackground { get => _AutoHideDrawerBackground; set { MGTheme.FreezeThemeValue(value); _AutoHideDrawerBackground = value; } }
    private IFillBrush _AutoHideDrawerHeaderBackground;
    public IFillBrush AutoHideDrawerHeaderBackground { get => _AutoHideDrawerHeaderBackground; set { MGTheme.FreezeThemeValue(value); _AutoHideDrawerHeaderBackground = value; } }
    private VisualStateFillBrush _AutoHideButtonBackground;
    public VisualStateFillBrush AutoHideButtonBackground { get => _AutoHideButtonBackground; set { MGTheme.FreezeThemeValue(value); _AutoHideButtonBackground = value; } }
    public Color AutoHideHeaderTextColor { get; set; }
    public Color AutoHideIconColor { get; set; }
    public Color AutoHideBorderColor { get; set; }
    public Color AutoHideGripColor { get; set; }

    private IFillBrush _AutoHideStripBackground;
    public IFillBrush AutoHideStripBackground { get => _AutoHideStripBackground; set { MGTheme.FreezeThemeValue(value); _AutoHideStripBackground = value; } }
    private VisualStateFillBrush _AutoHideStripButtonBackground;
    public VisualStateFillBrush AutoHideStripButtonBackground { get => _AutoHideStripButtonBackground; set { MGTheme.FreezeThemeValue(value); _AutoHideStripButtonBackground = value; } }
    public Color AutoHideStripTextColor { get; set; }
    public Color AutoHideStripSeparatorColor { get; set; }

    private IFillBrush _SplitterNormalBrush;
    public IFillBrush SplitterNormalBrush { get => _SplitterNormalBrush; set { MGTheme.FreezeThemeValue(value); _SplitterNormalBrush = value; } }
    private IFillBrush _SplitterHoverBrush;
    public IFillBrush SplitterHoverBrush { get => _SplitterHoverBrush; set { MGTheme.FreezeThemeValue(value); _SplitterHoverBrush = value; } }
    private IFillBrush _SplitterPressedBrush;
    public IFillBrush SplitterPressedBrush { get => _SplitterPressedBrush; set { MGTheme.FreezeThemeValue(value); _SplitterPressedBrush = value; } }
    public Color SplitterHoverOverlayColor { get; set; }
    public Color SplitterPressedOverlayColor { get; set; }

    public Color DropIndicatorInactiveColor { get; set; }
    public Color DropIndicatorActiveColor { get; set; }
    public Color DropIndicatorBorderColor { get; set; }
    public Color DropIndicatorHostInactiveColor { get; set; }
    public Color DropIndicatorHostActiveColor { get; set; }
    public Color DropIndicatorDisabledColor { get; set; }
    public Color DropIndicatorDisabledBorderColor { get; set; }
    public Color DropIndicatorSymbolColor { get; set; }
    public Color DropIndicatorDisabledSymbolColor { get; set; }

    //  The two preview overlay colors default to the values MGDockPreviewOverlay used to hard-code, so a theme definition written before
    //  these settings existed keeps the same drop preview.
    /// <summary>Fill of the <c>MGDockPreviewOverlay</c> that shows where a dragged panel will dock.</summary>
    public Color PreviewOverlayFillColor { get; set; } = new(0, 122, 204, 100);
    /// <summary>Border of the <c>MGDockPreviewOverlay</c> that shows where a dragged panel will dock.</summary>
    public Color PreviewOverlayBorderColor { get; set; } = new(0, 122, 204, 200);

    //  The two tab group settings default to the colors MGDockTabGroup used to hard-code, so a theme definition written before these
    //  settings existed keeps the same header strip.
    /// <summary>Hover overlay of the overflow and maximize/restore buttons in the header strip of an <c>MGDockTabGroup</c>.</summary>
    public Color TabGroupButtonHoverColor { get; set; } = new(70, 70, 74);
    /// <summary>Color of the overflow and window-state icons in the header strip of an <c>MGDockTabGroup</c>.</summary>
    public Color TabGroupIconColor { get; set; } = new(200, 200, 200);

    //  Docking densities (backlog task 14): the sizes the docking controls used to hard-code, so a theme definition written before these
    //  settings existed keeps the same layout.
    /// <summary>Height of the tab header strip of an <c>MGDockTabGroup</c> and of each <c>MGDockTabItem</c>.</summary>
    public int TabHeaderHeight { get; set; } = 30;
    /// <summary>Width reserved for the close and pin buttons of an <c>MGDockTabItem</c>.</summary>
    public int TabButtonSize { get; set; } = 22;
    /// <summary>Padding of the title text of an <c>MGDockTabItem</c>.</summary>
    public Thickness TabTitlePadding { get; set; } = new(8, 4, 4, 4);
    /// <summary>Height of the header of an <c>MGDockAutoHideDrawer</c>.</summary>
    public int AutoHideDrawerHeaderHeight { get; set; } = 28;
    /// <summary>Size of the square pin and close buttons in the header of an <c>MGDockAutoHideDrawer</c>.</summary>
    public int AutoHideDrawerButtonSize { get; set; } = 22;
    /// <summary>Thickness of an <c>MGDockAutoHideStrip</c> perpendicular to its edge, also the inset the <c>MGDockHost</c> reserves for it.</summary>
    public int AutoHideStripThickness { get; set; } = 24;
    /// <summary>Size of each square drop zone of the <c>MGDockDropIndicators</c>.</summary>
    public int DropIndicatorZoneSize { get; set; } = 40;
}

public class MGTheme
{
    #region Background
    private Dictionary<MGElementType, ThemeManagedVisualStateFillBrush> _Backgrounds { get; }
    private Dictionary<MGElementType, string> _ControlTemplateMappings { get; }
    private Dictionary<Type, string> _ControlTemplateMappingsByType { get; }

    public VisualStateFillBrush GetBackgroundBrush(MGElementType Type)
    {
        if (_Backgrounds.TryGetValue(Type, out var Value))
        {
            return Value.GetValue(true);
        }
        else
        {
            return new VisualStateFillBrush(null);
        }
    }

    public void SetBackgroundBrush(MGElementType Type, VisualStateFillBrush Value)
    {
        if (!_Backgrounds.TryGetValue(Type, out var ManagedValue))
        {
            ManagedValue = new(Value);
            _Backgrounds.Add(Type, ManagedValue);
        }
        else
        {
            ManagedValue.Value = Value;
        }
    }
    #endregion Background

    public IReadOnlyDictionary<MGElementType, string> ControlTemplateMappings => _ControlTemplateMappings;
    public IReadOnlyDictionary<Type, string> ControlTemplateTypeMappings => _ControlTemplateMappingsByType;

    public void SetControlTemplateMapping(MGElementType elementType, string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            _ControlTemplateMappings.Remove(elementType);
        }
        else
        {
            _ControlTemplateMappings[elementType] = templateName;
        }
    }

    public bool TryGetControlTemplateMapping(MGElementType elementType, out string templateName)
        => _ControlTemplateMappings.TryGetValue(elementType, out templateName);

    public void SetControlTemplateMapping(Type controlType, string templateName)
    {
        if (controlType == null)
        {
            throw new ArgumentNullException(nameof(controlType));
        }

        if (!typeof(MGElement).IsAssignableFrom(controlType))
        {
            throw new ArgumentException($"{nameof(controlType)} must derive from {nameof(MGElement)}.", nameof(controlType));
        }

        if (string.IsNullOrWhiteSpace(templateName))
        {
            _ControlTemplateMappingsByType.Remove(controlType);
        }
        else
        {
            _ControlTemplateMappingsByType[controlType] = templateName;
        }
    }

    public bool TryGetControlTemplateMapping(Type controlType, out string templateName)
    {
        for (var currentType = controlType; currentType != null && typeof(MGElement).IsAssignableFrom(currentType); currentType = currentType.BaseType)
        {
            if (_ControlTemplateMappingsByType.TryGetValue(currentType, out templateName))
            {
                return true;
            }
        }

        templateName = null;
        return false;
    }

    public ThemeManagedVisualStateFillBrush ComboBoxDropdownBackground { get; }
    /// <summary>The default background brush to use on items in an <see cref="MGComboBox{TItemType}"/>'s dropdown.</summary>
    public ThemeManagedVisualStateFillBrush ComboBoxDropdownItemBackground { get; }

    /// <summary>The default value to use for <see cref="MGCheckBox.CheckBoxComponentSize"/></summary>
    public int CheckBoxComponentSize { get; set; }
    /// <summary>The default value to use for <see cref="MGCheckBox.CheckMarkColor"/></summary>
    public Color CheckMarkColor { get; set; }
    /// <summary>The default value to use for <see cref="MGCheckBox.CheckedIndicatorStyle"/></summary>
    public CheckIndicatorStyle CheckBoxCheckedIndicatorStyle { get; set; }

    public Color DropdownArrowColor { get; set; }

    public ThemeManagedVisualStateFillBrush GridSplitterForeground { get; }

    /// <summary>The default background brush to use on items in an <see cref="MGListBox{TItemType}"/>.</summary>
    public ThemeManagedVisualStateFillBrush ListBoxItemBackground { get; }
    /// <summary>The default value to use for <see cref="MGListBox{TItemType}.AlternatingRowBackgrounds"/></summary>
    public List<ThemeManagedFillBrush> ListBoxItemAlternatingRowBackgrounds { get; }

    /// <summary>The default value to use for <see cref="MGTreeView.SelectionBackgroundBrush"/>.</summary>
    public ThemeManagedVisualStateFillBrush TreeViewSelectionBackground { get; }
    /// <summary>The default value to use for <see cref="MGTreeView.SelectionForeground"/>.</summary>
    public Color TreeViewSelectionForeground { get; set; }
    /// <summary>The default color to use for the expander arrow in <see cref="MGTreeViewItem"/>.</summary>
    public Color TreeViewExpanderArrowColor { get; set; }
    /// <summary>The default value to use for <see cref="MGTreeView.BorderBrush"/>.</summary>
    private IBorderBrush _TreeViewBorderBrush;
    public IBorderBrush TreeViewBorderBrush { get => _TreeViewBorderBrush; set { FreezeThemeValue(value); _TreeViewBorderBrush = value; } }
    /// <summary>The default value to use for <see cref="MGTreeView.BorderThickness"/>.</summary>
    public MonoGame.Extended.Thickness TreeViewBorderThickness { get; set; }
    /// <summary>The default value to use for <see cref="MGTreeView.IndentSize"/>.</summary>
    public int TreeViewIndentSize { get; set; }
    /// <summary>The default size of the expander button in <see cref="MGTreeViewItem"/>.</summary>
    public int TreeViewExpanderButtonSize { get; set; }

    /// <summary>The default value to use for <see cref="MGProgressButton.ProgressBarForeground"/>.</summary>
    public ThemeManagedFillBrush ProgressButtonForeground { get; }
    public ThemeManagedVisualStateFillBrush ProgressBarCompletedBrush { get; }
    public ThemeManagedVisualStateFillBrush ProgressBarIncompleteBrush { get; }

    public ThemeManagedVisualStateColorBrush RadioButtonBubbleBackground { get; set; }
    /// <summary>The default value to use for <see cref="MGRadioButton.BubbleCheckedColor"/></summary>
    public Color RadioButtonCheckedFillColor { get; set; }

    public ThemeManagedVisualStateColorBrush ResizeGripForeground { get; }

    public ThemeManagedVisualStateFillBrush ScrollBarOuterBrush { get; }
    public ThemeManagedVisualStateFillBrush ScrollBarInnerBrush { get; }

    public ThemeManagedFillBrush SliderForeground { get; }
    public ThemeManagedFillBrush SliderThumbFillBrush { get; }
    public ThemeManagedVisualStateFillBrush SliderOverlay { get; }

    public ThemeManagedVisualStateFillBrush SpoilerUnspoiledBackground { get; }

    public ThemeManagedVisualStateFillBrush SelectedTabHeaderBackground { get; }
    public ThemeManagedVisualStateFillBrush UnselectedTabHeaderBackground { get; }

    /// <summary>Default value to use for <see cref="MGTextBox.FocusedSelectionForegroundColor"/></summary>
    public Color TextBoxFocusedSelectionForeground { get; set; }
    /// <summary>Default value to use for <see cref="MGTextBox.FocusedSelectionBackgroundColor"/></summary>
    public Color TextBoxFocusedSelectionBackground { get; set; }
    /// <summary>Default value to use for <see cref="MGTextBox.UnfocusedSelectionForegroundColor"/></summary>
    public Color TextBoxUnfocusedSelectionForeground { get; set; }
    /// <summary>Default value to use for <see cref="MGTextBox.UnfocusedSelectionBackgroundColor"/></summary>
    public Color TextBoxUnfocusedSelectionBackground { get; set; }

    public ThemeManagedVisualStateFillBrush TitleBackground { get; }

    /// <summary>The fallback value to use for <see cref="MGTextBlock.ActualForeground"/> when there is no foreground color applied to the <see cref="MGTextBlock"/> or its parents.</summary>
    public ThemeManagedVisualStateColorBrush TextBlockFallbackForeground { get; }

    /// <summary>The default value to use for <see cref="MGTextBlock.WrapText"/> when a text block is created without an explicit wrapping preference.</summary>
    public bool DefaultTextBlockWrapText { get; set; }

    /// <summary>If true, editor-created <see cref="MGTextBlock"/> controls size horizontally from their content instead of honoring a preferred width.</summary>
    public bool DefaultTextBlockAutoWidthFromContent { get; set; }

    /// <summary>If true, <see cref="MGButton"/> controls size horizontally from their content instead of honoring a preferred width.</summary>
    public bool DefaultButtonAutoWidthFromContent { get; set; }

    /// <summary>If true, <see cref="MGComboBox{TItemType}"/> controls size horizontally from their selected content instead of honoring a preferred width.</summary>
    public bool DefaultComboBoxAutoWidthFromContent { get; set; }

    /// <summary>The default offset from the current mouse position to draw <see cref="MGToolTip"/>s at.<br/>
    /// This value is used to initialize <see cref="MGToolTip.DrawOffset"/><para/>
    /// Default value: (6, 6)</summary>
    public Point ToolTipOffset { get; set; }

    public VisualStateSetting<Color?> ToolTipTextForeground { get; set; }

    /// <summary>Density of the tooltips (backlog task 14).</summary>
    public MGThemeToolTipSettings ToolTip { get; }
    /// <summary>Density of the text boxes (backlog task 14).</summary>
    public MGThemeTextBoxSettings TextBox { get; }
    /// <summary>Density of the numeric up/down controls (backlog task 14).</summary>
    public MGThemeNumericUpDownSettings NumericUpDown { get; }
    /// <summary>Interaction timings of the controls that opt in (ADR-0007, decision 5).</summary>
    public MGThemeAnimationSettings Animation { get; }

    public ThemeFontSettings FontSettings { get; }
    public MGThemeWindowSettings Window { get; }
    public MGThemeOverlaySettings Overlay { get; }
    public MGThemeContextMenuSettings ContextMenu { get; }
    public MGThemeContextMenuItemSettings ContextMenuItem { get; }
    public MGThemeListBoxSettings ListBox { get; }
    public MGThemeListViewSettings ListView { get; }
    public MGThemePropertyGridSettings PropertyGrid { get; }
    public MGThemeComboBoxSettings ComboBox { get; }
    public MGThemeTreeViewTemplateSettings TreeViewTemplate { get; }
    public MGThemeTabControlSettings TabControl { get; }
    public MGThemeGraphSettings Graph { get; }
    public MGThemeDockingSettings Docking { get; }

    public enum BuiltInTheme
    {
        Light_Gray,
        Dark_Blue,
        Dark,
    }

    public MGTheme(string DefaultFontFamily)
        : this(BuiltInTheme.Dark_Blue, DefaultFontFamily) { }

    private const string BuiltInThemesResourceName = "MGUI.Core.UI.Themes.BuiltInThemes.xaml";
    private static readonly Lazy<IReadOnlyList<MGUI.Core.UI.XAML.ThemeDefinition>> BuiltInThemeDefinitions = new(LoadBuiltInThemeDefinitions);

    public MGTheme(BuiltInTheme ThemeType, string DefaultFontFamily)
        : this(DefaultFontFamily, true)
    {
        ApplyFrom(CreateBuiltInTheme(ThemeType, DefaultFontFamily));
    }

    private MGTheme(string DefaultFontFamily, bool InitializeOnly)
    {
        if (string.IsNullOrWhiteSpace(DefaultFontFamily))
        {
            throw new ArgumentNullException(nameof(DefaultFontFamily));
        }

        FontSettings = new(DefaultFontFamily);
        Window = new();
        Overlay = new();
        ContextMenu = new();
        ContextMenuItem = new();
        ListBox = new();
        ListView = new();
        PropertyGrid = new();
        ComboBox = new();
        TreeViewTemplate = new();
        TabControl = new();
        Graph = new();
        Docking = new();
        ToolTip = new();
        TextBox = new();
        NumericUpDown = new();
        Animation = new();
        ToolTipOffset = new(6, 6);
        ToolTipTextForeground = new(null, null, null, null);

        _Backgrounds = new();
        _ControlTemplateMappings = new();
        _ControlTemplateMappingsByType = new();
        foreach (MGElementType Type in Enum.GetValues(typeof(MGElementType)))
        {
            _Backgrounds[Type] = new ThemeManagedVisualStateFillBrush(new VisualStateFillBrush((IFillBrush)null));
        }

        ComboBoxDropdownBackground = new(new VisualStateFillBrush((IFillBrush)null));
        ComboBoxDropdownItemBackground = new(new VisualStateFillBrush((IFillBrush)null));
        CheckBoxComponentSize = MGCheckBox.DefaultCheckBoxSize;
        CheckBoxCheckedIndicatorStyle = CheckIndicatorStyle.CheckMark;
        GridSplitterForeground = new(new VisualStateFillBrush((IFillBrush)null));
        ListBoxItemBackground = new(new VisualStateFillBrush((IFillBrush)null));
        ListBoxItemAlternatingRowBackgrounds = new();
        TreeViewSelectionBackground = new(new VisualStateFillBrush((IFillBrush)null));
        ProgressButtonForeground = new(null);
        ProgressBarCompletedBrush = new(new VisualStateFillBrush((IFillBrush)null));
        ProgressBarIncompleteBrush = new(new VisualStateFillBrush((IFillBrush)null));
        RadioButtonBubbleBackground = new(new VisualStateColorBrush(default(Color)));
        ResizeGripForeground = new(new VisualStateColorBrush(default(Color)));
        ScrollBarOuterBrush = new(new VisualStateFillBrush((IFillBrush)null));
        ScrollBarInnerBrush = new(new VisualStateFillBrush((IFillBrush)null));
        SliderForeground = new(null);
        SliderThumbFillBrush = new(null);
        SliderOverlay = new(new VisualStateFillBrush((IFillBrush)null));
        SpoilerUnspoiledBackground = new(new VisualStateFillBrush((IFillBrush)null));
        SelectedTabHeaderBackground = new(new VisualStateFillBrush((IFillBrush)null));
        UnselectedTabHeaderBackground = new(new VisualStateFillBrush((IFillBrush)null));
        TitleBackground = new(new VisualStateFillBrush((IFillBrush)null));
        TextBlockFallbackForeground = new(new VisualStateColorBrush(default(Color)));
        DefaultTextBlockWrapText = true;
        DefaultTextBlockAutoWidthFromContent = false;
        DefaultButtonAutoWidthFromContent = false;
        DefaultComboBoxAutoWidthFromContent = false;
    }

    internal static MGTheme CreateEmpty(string DefaultFontFamily) => new(DefaultFontFamily, true);

    internal static bool TryCreateBuiltInTheme(string Name, string DefaultFontFamily, out MGTheme Theme)
    {
        if (!string.IsNullOrWhiteSpace(Name) && Enum.TryParse(Name, true, out BuiltInTheme ThemeType))
        {
            Theme = CreateBuiltInTheme(ThemeType, DefaultFontFamily);
            return true;
        }

        Theme = null;
        return false;
    }

    private static IReadOnlyList<MGUI.Core.UI.XAML.ThemeDefinition> LoadBuiltInThemeDefinitions()
    {
        var Markup = GeneralUtils.ReadEmbeddedResourceAsString(Assembly.GetExecutingAssembly(), BuiltInThemesResourceName);
        return MGUI.Core.UI.XAML.ThemeDefinitionLoader.ParseDefinitions(MGUI.Core.UI.XAML.XamlDocumentSource.FromString(Markup, BuiltInThemesResourceName));
    }

    private static MGTheme CreateBuiltInTheme(BuiltInTheme ThemeType, string DefaultFontFamily)
    {
        var Themes = MGUI.Core.UI.XAML.ThemeDefinitionLoader.BuildThemes(BuiltInThemeDefinitions.Value, null, DefaultFontFamily);
        if (!Themes.TryGetValue(ThemeType.ToString(), out var Theme))
        {
            throw new InvalidOperationException($"No built-in theme definition was found for '{ThemeType}'.");
        }
        return Theme;
    }

    private MGTheme(MGTheme Source)
        : this(Source?.FontSettings?.DefaultFontFamily ?? throw new ArgumentNullException(nameof(Source)), true)
    {
        ApplyFrom(Source);
    }

    public MGTheme Copy() => new(this);

    /// <summary>Freezes every brush this theme owns (ADR-0009, W4): the ten value brushes and every composite/highlight
    /// brush reachable from a public brush-typed property of <see cref="MGTheme"/> or one of its settings groups
    /// (<see cref="Window"/>, <see cref="Overlay"/>, ...), plus <see cref="_Backgrounds"/> (private, walked directly since
    /// reflection over public members does not reach it). Called once, at the end of <see cref="XAML.ThemeDefinitionBuilder.Build"/>,
    /// so every theme this framework produces -- built-in or from a XAML definition -- ends up fully frozen regardless of
    /// which of its properties a definition happened to touch. Reflection-driven rather than a hand-written list of the
    /// 78 properties: a future theme property is frozen automatically, and the same walk is what
    /// <c>BrushFreezingPolicyTests</c> uses to assert completeness.</summary>
    internal void FreezeBrushes()
    {
        foreach (var Background in _Backgrounds.Values)
        {
            FreezeThemeValue(Background.GetValue(false));
        }

        FreezeThemeObjectGraph(this);
    }

    /// <summary>Reflects over every public instance property of <paramref name="Owner"/> and freezes each brush-shaped
    /// value found (<see cref="FreezeThemeValue(object)"/>), recursing into the nested settings-group instances
    /// (<see cref="MGThemeWindowSettings"/>, <see cref="MGThemeGraphSettings"/>, ...) that hold brush properties of
    /// their own. A property with no public getter (none exist on <see cref="MGTheme"/> or a settings group) would throw;
    /// none does, so no guard is needed.</summary>
    private static void FreezeThemeObjectGraph(object Owner)
    {
        if (Owner == null)
        {
            return;
        }

        foreach (var Property in Owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            FreezeThemeValue(Property.GetValue(Owner));
        }
    }

    /// <summary>Freezes <paramref name="Value"/> when it is a brush (<see cref="IUIFreezable"/>, skipped when
    /// <see cref="IUIFreezable.CanFreeze"/> is false -- e.g. a <see cref="MGUI.Core.UI.Brushes.FillBrushes.MGProgressBarGradientBrush"/>
    /// bound to a live <see cref="MGProgressBar"/> is not expected to be assigned to a theme, and is left unfrozen rather
    /// than throwing), recurses into a <see cref="VisualStateFillBrush"/> container's non-null slots (the container itself
    /// stays unfrozen, W3), unwraps a <see cref="ThemeManagedGetter{TDataType}"/> without cloning it, walks a collection of
    /// <see cref="ThemeManagedFillBrush"/> (<see cref="ListBoxItemAlternatingRowBackgrounds"/>), and recurses into a nested
    /// settings-group instance; anything else (a scalar, a <see cref="VisualStateColorBrush"/> whose slots are
    /// <see cref="Color"/>, not brushes, a dictionary of control-template mappings, ...) is left alone.</summary>
    internal static void FreezeThemeValue(object Value)
    {
        switch (Value)
        {
            case null:
                return;
            case IUIFreezable Freezable:
                if (Freezable.CanFreeze)
                {
                    Freezable.Freeze();
                }
                return;
            case VisualStateFillBrush Container:
                FreezeThemeValue(Container.NormalValue);
                FreezeThemeValue(Container.SelectedValue);
                FreezeThemeValue(Container.FocusedValue);
                FreezeThemeValue(Container.DisabledValue);
                if (Container.HasCheckedValue)
                {
                    FreezeThemeValue(Container.CheckedValue);
                }
                //  A slot assigned before this sweep (every theme background starts as an unfrozen XAML brush) may have
                //  subscribed the container to it while it was still unfrozen; freezing it above does not itself notify
                //  that subscriber, so the container must be told to re-check now that its slots just froze.
                Container.ResyncSlotSubscriptionsAfterExternalFreeze();
                return;
            case ThemeManagedFillBrush ManagedFillBrush:
                FreezeThemeValue(ManagedFillBrush.GetValue(false));
                return;
            case ThemeManagedVisualStateFillBrush ManagedVisualStateFillBrush:
                FreezeThemeValue(ManagedVisualStateFillBrush.GetValue(false));
                return;
            case ThemeManagedVisualStateColorBrush:
                //  VisualStateColorBrush's slots are System.Drawing-style Colors, not brushes: nothing to freeze.
                return;
            case System.Collections.IEnumerable Sequence and not string:
                foreach (var Item in Sequence)
                {
                    FreezeThemeValue(Item);
                }
                return;
            case MGThemeWindowSettings or MGThemeOverlaySettings or MGThemeContextMenuSettings or MGThemeContextMenuItemSettings
                 or MGThemeListBoxSettings or MGThemeListViewSettings or MGThemePropertyGridSettings or MGThemeComboBoxSettings
                 or MGThemeToolTipSettings or MGThemeTabControlSettings or MGThemeGraphSettings or MGThemeDockingSettings:
                FreezeThemeObjectGraph(Value);
                return;
            default:
                //  A scalar (Color, Thickness, Point, int, bool, string, an enum, a TimeSpan, ...) or a settings group
                //  that owns no brush property (ThemeFontSettings, MGThemeAnimationSettings, ...): nothing to do.
                return;
        }
    }

    /// <summary>ADR-0009, W4: returns <paramref name="Brush"/> unchanged (shared by reference) when it is already frozen,
    /// otherwise an unfrozen deep copy via <see cref="ICloneable.Clone"/> (<c>Copy()</c> under the hood) -- same rule
    /// <see cref="VisualStateFillBrush"/>'s own copy constructor already applies to its slots since W3. Used by
    /// <see cref="ApplyFrom"/> for the plain <see cref="MGUI.Core.UI.Brushes.FillBrushes.IFillBrush"/>/
    /// <see cref="MGUI.Core.UI.Brushes.BorderBrushes.IBorderBrush"/>-typed theme properties (a
    /// <see cref="VisualStateFillBrush"/>/<see cref="VisualStateColorBrush"/>-typed property keeps calling its own
    /// <c>Copy()</c>/<c>GetCopy()</c>, which already shares frozen slots).</summary>
    private static T ShareOrCopy<T>(T Brush) where T : class, IUIFreezable, ICloneable
        => Brush == null ? null : Brush.IsFrozen ? Brush : (T)Brush.Clone();

    private void ApplyFrom(MGTheme Source)
    {
        foreach (MGElementType Type in Enum.GetValues(typeof(MGElementType)))
        {
            SetBackgroundBrush(Type, Source.GetBackgroundBrush(Type));
        }

        _ControlTemplateMappings.Clear();
        foreach (var item in Source.ControlTemplateMappings)
        {
            _ControlTemplateMappings[item.Key] = item.Value;
        }

        _ControlTemplateMappingsByType.Clear();
        foreach (var item in Source.ControlTemplateTypeMappings)
        {
            _ControlTemplateMappingsByType[item.Key] = item.Value;
        }

        ComboBoxDropdownBackground.Value = Source.ComboBoxDropdownBackground.GetValue(true);
        ComboBoxDropdownItemBackground.Value = Source.ComboBoxDropdownItemBackground.GetValue(true);
        CheckBoxComponentSize = Source.CheckBoxComponentSize;
        CheckMarkColor = Source.CheckMarkColor;
        CheckBoxCheckedIndicatorStyle = Source.CheckBoxCheckedIndicatorStyle;
        DropdownArrowColor = Source.DropdownArrowColor;
        GridSplitterForeground.Value = Source.GridSplitterForeground.GetValue(true);
        ListBoxItemBackground.Value = Source.ListBoxItemBackground.GetValue(true);

        ListBoxItemAlternatingRowBackgrounds.Clear();
        foreach (var Background in Source.ListBoxItemAlternatingRowBackgrounds)
        {
            ListBoxItemAlternatingRowBackgrounds.Add(new ThemeManagedFillBrush(Background.GetValue(true)));
        }

        TreeViewSelectionBackground.Value = Source.TreeViewSelectionBackground.GetValue(true);
        TreeViewSelectionForeground = Source.TreeViewSelectionForeground;
        TreeViewExpanderArrowColor = Source.TreeViewExpanderArrowColor;
        TreeViewBorderBrush = ShareOrCopy(Source.TreeViewBorderBrush);
        TreeViewBorderThickness = Source.TreeViewBorderThickness;
        TreeViewIndentSize = Source.TreeViewIndentSize;
        TreeViewExpanderButtonSize = Source.TreeViewExpanderButtonSize;
        ProgressButtonForeground.Value = Source.ProgressButtonForeground.GetValue(true);
        ProgressBarCompletedBrush.Value = Source.ProgressBarCompletedBrush.GetValue(true);
        ProgressBarIncompleteBrush.Value = Source.ProgressBarIncompleteBrush.GetValue(true);
        RadioButtonBubbleBackground = new ThemeManagedVisualStateColorBrush(Source.RadioButtonBubbleBackground.GetValue(true));
        RadioButtonCheckedFillColor = Source.RadioButtonCheckedFillColor;
        ResizeGripForeground.Value = Source.ResizeGripForeground.GetValue(true);
        ScrollBarOuterBrush.Value = Source.ScrollBarOuterBrush.GetValue(true);
        ScrollBarInnerBrush.Value = Source.ScrollBarInnerBrush.GetValue(true);
        SliderForeground.Value = Source.SliderForeground.GetValue(true);
        SliderThumbFillBrush.Value = Source.SliderThumbFillBrush.GetValue(true);
        SliderOverlay.Value = Source.SliderOverlay.GetValue(true);
        SpoilerUnspoiledBackground.Value = Source.SpoilerUnspoiledBackground.GetValue(true);
        SelectedTabHeaderBackground.Value = Source.SelectedTabHeaderBackground.GetValue(true);
        UnselectedTabHeaderBackground.Value = Source.UnselectedTabHeaderBackground.GetValue(true);
        TextBoxFocusedSelectionForeground = Source.TextBoxFocusedSelectionForeground;
        TextBoxFocusedSelectionBackground = Source.TextBoxFocusedSelectionBackground;
        TextBoxUnfocusedSelectionForeground = Source.TextBoxUnfocusedSelectionForeground;
        TextBoxUnfocusedSelectionBackground = Source.TextBoxUnfocusedSelectionBackground;
        TitleBackground.Value = Source.TitleBackground.GetValue(true);
        TextBlockFallbackForeground.Value = Source.TextBlockFallbackForeground.GetValue(true);
        DefaultTextBlockWrapText = Source.DefaultTextBlockWrapText;
        DefaultTextBlockAutoWidthFromContent = Source.DefaultTextBlockAutoWidthFromContent;
        DefaultButtonAutoWidthFromContent = Source.DefaultButtonAutoWidthFromContent;
        DefaultComboBoxAutoWidthFromContent = Source.DefaultComboBoxAutoWidthFromContent;
        ToolTipOffset = Source.ToolTipOffset;
        ToolTipTextForeground = Source.ToolTipTextForeground?.GetCopy();

        ToolTip.Padding = Source.ToolTip.Padding;
        ToolTip.BorderThickness = Source.ToolTip.BorderThickness;
        ToolTip.BorderBrush = ShareOrCopy(Source.ToolTip.BorderBrush);
        ToolTip.MinWidth = Source.ToolTip.MinWidth;
        ToolTip.MinHeight = Source.ToolTip.MinHeight;

        TextBox.Padding = Source.TextBox.Padding;
        TextBox.MinHeight = Source.TextBox.MinHeight;

        NumericUpDown.Padding = Source.NumericUpDown.Padding;
        NumericUpDown.MinHeight = Source.NumericUpDown.MinHeight;
        NumericUpDown.SpinnerWidth = Source.NumericUpDown.SpinnerWidth;
        NumericUpDown.SpinnerMinWidth = Source.NumericUpDown.SpinnerMinWidth;

        Animation.Enabled = Source.Animation.Enabled;
        Animation.HoverDuration = Source.Animation.HoverDuration;
        Animation.PressDuration = Source.Animation.PressDuration;
        Animation.FocusDuration = Source.Animation.FocusDuration;
        Animation.HoverEasing = Source.Animation.HoverEasing;
        Animation.PressEasing = Source.Animation.PressEasing;
        Animation.FocusEasing = Source.Animation.FocusEasing;

        FontSettings.ContextMenuFontSize = Source.FontSettings.ContextMenuFontSize;
        FontSettings.SmallFontSize = Source.FontSettings.SmallFontSize;
        FontSettings.MediumFontSize = Source.FontSettings.MediumFontSize;
        FontSettings.LargeFontSize = Source.FontSettings.LargeFontSize;
        FontSettings.DefaultFontSize = Source.FontSettings.DefaultFontSize;
        FontSettings.UseExactScale = Source.FontSettings.UseExactScale;
        FontSettings.DefaultFontFamily = Source.FontSettings.DefaultFontFamily;
        FontSettings.DefaultFontShadowOffset = Source.FontSettings.DefaultFontShadowOffset;
        FontSettings.DefaultFontShadowColor = Source.FontSettings.DefaultFontShadowColor;

        Window.Padding = Source.Window.Padding;
        Window.BorderThickness = Source.Window.BorderThickness;
        Window.ChromelessPadding = Source.Window.ChromelessPadding;
        Window.ChromelessBorderThickness = Source.Window.ChromelessBorderThickness;
        Window.BorderBrush = ShareOrCopy(Source.Window.BorderBrush);
        Window.TitleBarPadding = Source.Window.TitleBarPadding;
        Window.TitleBarMinHeight = Source.Window.TitleBarMinHeight;
        Window.CloseButtonBackground = Source.Window.CloseButtonBackground?.Copy();
        Window.CloseButtonBorderBrush = ShareOrCopy(Source.Window.CloseButtonBorderBrush);
        Window.CloseButtonBorderThickness = Source.Window.CloseButtonBorderThickness;
        Window.CloseButtonMargin = Source.Window.CloseButtonMargin;
        Window.CloseButtonPadding = Source.Window.CloseButtonPadding;
        Window.CloseButtonMinWidth = Source.Window.CloseButtonMinWidth;
        Window.CloseButtonMinHeight = Source.Window.CloseButtonMinHeight;
        Window.TitleTextMargin = Source.Window.TitleTextMargin;
        Window.TitleTextPadding = Source.Window.TitleTextPadding;
        Window.TitleTextForeground = Source.Window.TitleTextForeground?.GetCopy();

        Overlay.HostPadding = Source.Overlay.HostPadding;
        Overlay.Padding = Source.Overlay.Padding;
        Overlay.BorderThickness = Source.Overlay.BorderThickness;
        Overlay.BorderBrush = ShareOrCopy(Source.Overlay.BorderBrush);
        Overlay.CloseButtonBackground = Source.Overlay.CloseButtonBackground?.Copy();
        Overlay.CloseButtonBorderBrush = ShareOrCopy(Source.Overlay.CloseButtonBorderBrush);
        Overlay.CloseButtonBorderThickness = Source.Overlay.CloseButtonBorderThickness;
        Overlay.CloseButtonPadding = Source.Overlay.CloseButtonPadding;
        Overlay.CloseButtonMinWidth = Source.Overlay.CloseButtonMinWidth;
        Overlay.CloseButtonMinHeight = Source.Overlay.CloseButtonMinHeight;

        ContextMenu.Padding = Source.ContextMenu.Padding;
        ContextMenu.BorderBrush = ShareOrCopy(Source.ContextMenu.BorderBrush);
        ContextMenu.BorderThickness = Source.ContextMenu.BorderThickness;

        ContextMenuItem.HeaderMargin = Source.ContextMenuItem.HeaderMargin;
        ContextMenuItem.HeaderBackground = Source.ContextMenuItem.HeaderBackground?.Copy();
        ContextMenuItem.ShortcutMargin = Source.ContextMenuItem.ShortcutMargin;
        ContextMenuItem.ShortcutForeground = Source.ContextMenuItem.ShortcutForeground?.GetCopy();
        ContextMenuItem.SubmenuArrowMargin = Source.ContextMenuItem.SubmenuArrowMargin;

        ListBox.MinHeight = Source.ListBox.MinHeight;
        ListBox.OuterBackground = Source.ListBox.OuterBackground?.Copy();
        ListBox.TitlePadding = Source.ListBox.TitlePadding;
        ListBox.TitleForeground = Source.ListBox.TitleForeground?.GetCopy();
        ListBox.TitleBorderBrush = ShareOrCopy(Source.ListBox.TitleBorderBrush);
        ListBox.TitleBorderThickness = Source.ListBox.TitleBorderThickness;
        ListBox.InnerBorderBrush = ShareOrCopy(Source.ListBox.InnerBorderBrush);
        ListBox.InnerBorderThickness = Source.ListBox.InnerBorderThickness;
        ListBox.ScrollViewerPadding = Source.ListBox.ScrollViewerPadding;
        ListBox.ItemsPanelBorderBrush = ShareOrCopy(Source.ListBox.ItemsPanelBorderBrush);
        ListBox.ItemsPanelBorderThickness = Source.ListBox.ItemsPanelBorderThickness;
        ListBox.ItemPadding = Source.ListBox.ItemPadding;
        ListBox.ItemContentPadding = Source.ListBox.ItemContentPadding;

        ListView.HeaderForeground = Source.ListView.HeaderForeground?.GetCopy();
        ListView.GridLineBrush = ShareOrCopy(Source.ListView.GridLineBrush);

        PropertyGrid.Padding = Source.PropertyGrid.Padding;
        PropertyGrid.BorderBrush = ShareOrCopy(Source.PropertyGrid.BorderBrush);
        PropertyGrid.BorderThickness = Source.PropertyGrid.BorderThickness;
        PropertyGrid.ScrollViewerPadding = Source.PropertyGrid.ScrollViewerPadding;
        PropertyGrid.CategoriesSpacing = Source.PropertyGrid.CategoriesSpacing;
        PropertyGrid.CategoryHeaderBackground = Source.PropertyGrid.CategoryHeaderBackground?.Copy();
        PropertyGrid.CategoryHeaderForeground = Source.PropertyGrid.CategoryHeaderForeground?.Copy();
        PropertyGrid.CategoryHeaderPadding = Source.PropertyGrid.CategoryHeaderPadding;
        PropertyGrid.CategoryHeaderMinHeight = Source.PropertyGrid.CategoryHeaderMinHeight;
        PropertyGrid.CategoryArrowColor = Source.PropertyGrid.CategoryArrowColor;
        PropertyGrid.RowPadding = Source.PropertyGrid.RowPadding;
        PropertyGrid.RowsSpacing = Source.PropertyGrid.RowsSpacing;
        PropertyGrid.RowSeparatorBrush = ShareOrCopy(Source.PropertyGrid.RowSeparatorBrush);
        PropertyGrid.InvalidEditorBorderBrush = ShareOrCopy(Source.PropertyGrid.InvalidEditorBorderBrush);

        ComboBox.Padding = Source.ComboBox.Padding;
        ComboBox.MinHeight = Source.ComboBox.MinHeight;
        ComboBox.BorderBrush = ShareOrCopy(Source.ComboBox.BorderBrush);
        ComboBox.DropdownArrowMargin = Source.ComboBox.DropdownArrowMargin;
        ComboBox.DropdownMinWidth = Source.ComboBox.DropdownMinWidth;
        ComboBox.DropdownBorderThickness = Source.ComboBox.DropdownBorderThickness;
        ComboBox.DropdownBorderBrush = ShareOrCopy(Source.ComboBox.DropdownBorderBrush);
        ComboBox.DropdownPadding = Source.ComboBox.DropdownPadding;
        ComboBox.DropdownScrollViewerPadding = Source.ComboBox.DropdownScrollViewerPadding;
        ComboBox.DropdownItemsSpacing = Source.ComboBox.DropdownItemsSpacing;
        ComboBox.DropdownItemPadding = Source.ComboBox.DropdownItemPadding;

        TreeViewTemplate.ScrollViewerPadding = Source.TreeViewTemplate.ScrollViewerPadding;
        TreeViewTemplate.ItemsPanelPadding = Source.TreeViewTemplate.ItemsPanelPadding;
        TreeViewTemplate.ItemsPanelSpacing = Source.TreeViewTemplate.ItemsPanelSpacing;

        TabControl.Padding = Source.TabControl.Padding;
        TabControl.BorderBrush = ShareOrCopy(Source.TabControl.BorderBrush);
        TabControl.BorderThickness = Source.TabControl.BorderThickness;
        TabControl.HeadersSpacing = Source.TabControl.HeadersSpacing;
        TabControl.SelectedHeaderPadding = Source.TabControl.SelectedHeaderPadding;
        TabControl.UnselectedHeaderPadding = Source.TabControl.UnselectedHeaderPadding;
        TabControl.SideHeaderPadding = Source.TabControl.SideHeaderPadding;

        Graph.Padding = Source.Graph.Padding;
        Graph.BorderBrush = ShareOrCopy(Source.Graph.BorderBrush);
        Graph.BorderThickness = Source.Graph.BorderThickness;
        Graph.CanvasBackground = Source.Graph.CanvasBackground?.Copy();
        Graph.GridLineBrush = ShareOrCopy(Source.Graph.GridLineBrush);
        Graph.MajorGridLineBrush = ShareOrCopy(Source.Graph.MajorGridLineBrush);
        Graph.EdgeBrush = ShareOrCopy(Source.Graph.EdgeBrush);
        Graph.SelectedEdgeBrush = ShareOrCopy(Source.Graph.SelectedEdgeBrush);
        Graph.NodeBorderBrush = ShareOrCopy(Source.Graph.NodeBorderBrush);
        Graph.NodeBorderThickness = Source.Graph.NodeBorderThickness;
        Graph.NodeSelectedBorderBrush = ShareOrCopy(Source.Graph.NodeSelectedBorderBrush);
        Graph.NodeSelectedBorderThickness = Source.Graph.NodeSelectedBorderThickness;
        Graph.NodeHeaderBackground = Source.Graph.NodeHeaderBackground?.Copy();
        Graph.NodeHeaderForeground = Source.Graph.NodeHeaderForeground?.Copy();
        Graph.NodeBodyBackground = Source.Graph.NodeBodyBackground?.Copy();
        Graph.PortBackground = ShareOrCopy(Source.Graph.PortBackground);
        Graph.PortForeground = Source.Graph.PortForeground?.Copy();
        Graph.CommentBackground = Source.Graph.CommentBackground?.Copy();
        Graph.CommentBorderBrush = ShareOrCopy(Source.Graph.CommentBorderBrush);

        Docking.TabNormalBackground = ShareOrCopy(Source.Docking.TabNormalBackground);
        Docking.TabHoverBackground = ShareOrCopy(Source.Docking.TabHoverBackground);
        Docking.TabActiveBackground = ShareOrCopy(Source.Docking.TabActiveBackground);
        Docking.TabActiveAccentColor = Source.Docking.TabActiveAccentColor;
        Docking.TabHoverAccentColor = Source.Docking.TabHoverAccentColor;
        Docking.TabActiveTextColor = Source.Docking.TabActiveTextColor;
        Docking.TabInactiveTextColor = Source.Docking.TabInactiveTextColor;
        Docking.TabActiveIconColor = Source.Docking.TabActiveIconColor;
        Docking.TabInactiveIconColor = Source.Docking.TabInactiveIconColor;
        Docking.AutoHideDrawerBackground = ShareOrCopy(Source.Docking.AutoHideDrawerBackground);
        Docking.AutoHideDrawerHeaderBackground = ShareOrCopy(Source.Docking.AutoHideDrawerHeaderBackground);
        Docking.AutoHideButtonBackground = Source.Docking.AutoHideButtonBackground?.Copy();
        Docking.AutoHideHeaderTextColor = Source.Docking.AutoHideHeaderTextColor;
        Docking.AutoHideIconColor = Source.Docking.AutoHideIconColor;
        Docking.AutoHideBorderColor = Source.Docking.AutoHideBorderColor;
        Docking.AutoHideGripColor = Source.Docking.AutoHideGripColor;
        Docking.AutoHideStripBackground = ShareOrCopy(Source.Docking.AutoHideStripBackground);
        Docking.AutoHideStripButtonBackground = Source.Docking.AutoHideStripButtonBackground?.Copy();
        Docking.AutoHideStripTextColor = Source.Docking.AutoHideStripTextColor;
        Docking.AutoHideStripSeparatorColor = Source.Docking.AutoHideStripSeparatorColor;
        Docking.SplitterNormalBrush = ShareOrCopy(Source.Docking.SplitterNormalBrush);
        Docking.SplitterHoverBrush = ShareOrCopy(Source.Docking.SplitterHoverBrush);
        Docking.SplitterPressedBrush = ShareOrCopy(Source.Docking.SplitterPressedBrush);
        Docking.SplitterHoverOverlayColor = Source.Docking.SplitterHoverOverlayColor;
        Docking.SplitterPressedOverlayColor = Source.Docking.SplitterPressedOverlayColor;
        Docking.DropIndicatorInactiveColor = Source.Docking.DropIndicatorInactiveColor;
        Docking.DropIndicatorActiveColor = Source.Docking.DropIndicatorActiveColor;
        Docking.DropIndicatorBorderColor = Source.Docking.DropIndicatorBorderColor;
        Docking.DropIndicatorHostInactiveColor = Source.Docking.DropIndicatorHostInactiveColor;
        Docking.DropIndicatorHostActiveColor = Source.Docking.DropIndicatorHostActiveColor;
        Docking.DropIndicatorDisabledColor = Source.Docking.DropIndicatorDisabledColor;
        Docking.DropIndicatorDisabledBorderColor = Source.Docking.DropIndicatorDisabledBorderColor;
        Docking.DropIndicatorSymbolColor = Source.Docking.DropIndicatorSymbolColor;
        Docking.DropIndicatorDisabledSymbolColor = Source.Docking.DropIndicatorDisabledSymbolColor;
        Docking.PreviewOverlayFillColor = Source.Docking.PreviewOverlayFillColor;
        Docking.PreviewOverlayBorderColor = Source.Docking.PreviewOverlayBorderColor;
        Docking.TabGroupButtonHoverColor = Source.Docking.TabGroupButtonHoverColor;
        Docking.TabGroupIconColor = Source.Docking.TabGroupIconColor;
        Docking.TabHeaderHeight = Source.Docking.TabHeaderHeight;
        Docking.TabButtonSize = Source.Docking.TabButtonSize;
        Docking.TabTitlePadding = Source.Docking.TabTitlePadding;
        Docking.AutoHideDrawerHeaderHeight = Source.Docking.AutoHideDrawerHeaderHeight;
        Docking.AutoHideDrawerButtonSize = Source.Docking.AutoHideDrawerButtonSize;
        Docking.AutoHideStripThickness = Source.Docking.AutoHideStripThickness;
        Docking.DropIndicatorZoneSize = Source.Docking.DropIndicatorZoneSize;
    }
}