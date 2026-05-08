using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Core.UI
{
    /// <summary>A wrapper class for the given <typeparamref name="TDataType"/> that controls the getter function, typically to return a copy of the value (to mimic treating class implementations as value-types)</summary>
    public class ThemeManagedGetter<TDataType>
        where TDataType : class, ICloneable
    {
        private TDataType _Value;
        /// <summary>This property intentionally has no getter. To get the value, use <see cref="GetValue(bool)"/></summary>
        public TDataType Value { set => _Value = value; }

        /// <param name="Copy">If true, a copy of the underlying <see cref="Value"/> will be returned. If false, a direct reference to the underlying <see cref="Value"/> will be returned.<para/>
        /// Some implementations of <typeparamref name="TDataType"/> are structs (value-types) rather than classes (reference-types),<br/>
        /// so for consistency, recommended to always retrieve a copy, essentially treating all objects as value-types.</param>
        public TDataType GetValue(bool Copy = true) => Copy ? _Value?.Clone() as TDataType : _Value;

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
        public IBorderBrush BorderBrush { get; set; } = MGUniformBorderBrush.Black;
        public Thickness TitleBarPadding { get; set; } = new(2);
        public int TitleBarMinHeight { get; set; } = 24;
        public VisualStateFillBrush CloseButtonBackground { get; set; } = new(Color.Crimson.AsFillBrush() * 0.5f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
        public IBorderBrush CloseButtonBorderBrush { get; set; } = MGUniformBorderBrush.Black;
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
        public IBorderBrush BorderBrush { get; set; } = MGUniformBorderBrush.Black;
        public VisualStateFillBrush CloseButtonBackground { get; set; } = new(Color.Crimson.AsFillBrush() * 0.8f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
        public IBorderBrush CloseButtonBorderBrush { get; set; } = MGUniformBorderBrush.Black;
        public Thickness CloseButtonBorderThickness { get; set; } = new(1);
        public Thickness CloseButtonPadding { get; set; } = new(0);
        public int CloseButtonMinWidth { get; set; } = 16;
        public int CloseButtonMinHeight { get; set; } = 16;
    }

    public class MGThemeContextMenuSettings
    {
        public Thickness Padding { get; set; } = new(0);
        public IBorderBrush BorderBrush { get; set; } = MGUniformBorderBrush.Gray;
        public Thickness BorderThickness { get; set; } = new(1);
    }

    public class MGThemeContextMenuItemSettings
    {
        public Thickness HeaderMargin { get; set; } = new(0, 0, 5, 0);
        public VisualStateFillBrush HeaderBackground { get; set; } = new(null);
        public Thickness ShortcutMargin { get; set; } = new(18, 0, 0, 0);
        public VisualStateSetting<Color?> ShortcutForeground { get; set; } = new(Color.LightGray, Color.LightGray, Color.LightGray);
        public Thickness SubmenuArrowMargin { get; set; } = new(0, 5, 8, 5);
    }

    public class MGThemeListBoxSettings
    {
        public int MinHeight { get; set; } = 30;
        public VisualStateFillBrush OuterBackground { get; set; } = new(SolidFillBrushes.Black);
        public Thickness TitlePadding { get; set; } = new(6, 3);
        public VisualStateSetting<Color?> TitleForeground { get; set; } = new(Color.White, Color.White, Color.White);
        public IBorderBrush TitleBorderBrush { get; set; } = SolidFillBrushes.Black.AsUniformBorderBrush();
        public Thickness TitleBorderThickness { get; set; } = new(1, 1, 1, 0);
        public IBorderBrush InnerBorderBrush { get; set; } = SolidFillBrushes.Black.AsUniformBorderBrush();
        public Thickness InnerBorderThickness { get; set; } = new(1);
        public Thickness ScrollViewerPadding { get; set; } = new(0);
        public IBorderBrush ItemsPanelBorderBrush { get; set; } = SolidFillBrushes.Black.AsUniformBorderBrush();
        public Thickness ItemsPanelBorderThickness { get; set; } = new(1);
    }

    public class MGThemeListViewSettings
    {
        public VisualStateSetting<Color?> HeaderForeground { get; set; } = new(Color.White, Color.White, Color.White);
        public IFillBrush GridLineBrush { get; set; } = SolidFillBrushes.Black;
    }

    public class MGThemePropertyGridSettings
    {
        public Thickness Padding { get; set; } = new(0);
        public IBorderBrush BorderBrush { get; set; } = MGUniformBorderBrush.Black;
        public Thickness BorderThickness { get; set; } = new(1);
        public Thickness ScrollViewerPadding { get; set; } = new(0);
        public int CategoriesSpacing { get; set; } = 4;
        public VisualStateFillBrush CategoryHeaderBackground { get; set; } = new(new MGSolidFillBrush(Color.Black * 0.35f));
        public VisualStateColorBrush CategoryHeaderForeground { get; set; } = new(Color.White);
        public Thickness CategoryHeaderPadding { get; set; } = new(8, 4);
        public int CategoryHeaderMinHeight { get; set; } = 24;
        public Color CategoryArrowColor { get; set; } = Color.White;
        public Thickness RowPadding { get; set; } = new(8, 4);
        public int RowsSpacing { get; set; } = 0;
        public IFillBrush RowSeparatorBrush { get; set; } = new MGSolidFillBrush(Color.Black * 0.35f);
        public IBorderBrush InvalidEditorBorderBrush { get; set; } = new MGSolidFillBrush(Color.OrangeRed).AsUniformBorderBrush();
    }

    public class MGThemeComboBoxSettings
    {
        public Thickness Padding { get; set; } = new(4, 2, 4, 2);
        public int MinHeight { get; set; } = 26;
        public IBorderBrush BorderBrush { get; set; } = MGUniformBorderBrush.Black;
        public Thickness DropdownArrowMargin { get; set; } = new(6, 0, 4, 0);
        public int DropdownMinWidth { get; set; } = 100;
        public Thickness DropdownBorderThickness { get; set; } = new(1);
        public IBorderBrush DropdownBorderBrush { get; set; } = MGUniformBorderBrush.Gray;
        public Thickness DropdownPadding { get; set; } = new(0);
        public Thickness DropdownScrollViewerPadding { get; set; } = new(0);
        public int DropdownItemsSpacing { get; set; } = 0;
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
        public IBorderBrush BorderBrush { get; set; } = MGUniformBorderBrush.Black;
        public Thickness BorderThickness { get; set; } = new(1);
        public int HeadersSpacing { get; set; } = 0;
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
        public IFillBrush TabNormalBackground { get; set; }
        public IFillBrush TabHoverBackground { get; set; }
        public IFillBrush TabActiveBackground { get; set; }
        public Color TabActiveAccentColor { get; set; }
        public Color TabHoverAccentColor { get; set; }
        public Color TabActiveTextColor { get; set; }
        public Color TabInactiveTextColor { get; set; }
        public Color TabActiveIconColor { get; set; }
        public Color TabInactiveIconColor { get; set; }

        public IFillBrush AutoHideDrawerBackground { get; set; }
        public IFillBrush AutoHideDrawerHeaderBackground { get; set; }
        public VisualStateFillBrush AutoHideButtonBackground { get; set; }
        public Color AutoHideHeaderTextColor { get; set; }
        public Color AutoHideIconColor { get; set; }
        public Color AutoHideBorderColor { get; set; }
        public Color AutoHideGripColor { get; set; }

        public IFillBrush AutoHideStripBackground { get; set; }
        public VisualStateFillBrush AutoHideStripButtonBackground { get; set; }
        public Color AutoHideStripTextColor { get; set; }
        public Color AutoHideStripSeparatorColor { get; set; }

        public IFillBrush SplitterNormalBrush { get; set; }
        public IFillBrush SplitterHoverBrush { get; set; }
        public IFillBrush SplitterPressedBrush { get; set; }
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
    }

    public class MGTheme
    {
        #region Background
        private Dictionary<MGElementType, ThemeManagedVisualStateFillBrush> _Backgrounds { get; }
        private Dictionary<MGElementType, string> _ControlTemplateMappings { get; }
        private Dictionary<Type, string> _ControlTemplateMappingsByType { get; }

        public VisualStateFillBrush GetBackgroundBrush(MGElementType Type)
        {
            if (_Backgrounds.TryGetValue(Type, out ThemeManagedVisualStateFillBrush Value))
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
            if (!_Backgrounds.TryGetValue(Type, out ThemeManagedVisualStateFillBrush ManagedValue))
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
            for (Type currentType = controlType; currentType != null && typeof(MGElement).IsAssignableFrom(currentType); currentType = currentType.BaseType)
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
        public IBorderBrush TreeViewBorderBrush { get; set; }
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
            Docking = new();
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
            string Markup = GeneralUtils.ReadEmbeddedResourceAsString(Assembly.GetExecutingAssembly(), BuiltInThemesResourceName);
            return MGUI.Core.UI.XAML.ThemeDefinitionLoader.ParseDefinitions(MGUI.Core.UI.XAML.XamlDocumentSource.FromString(Markup, BuiltInThemesResourceName));
        }

        private static MGTheme CreateBuiltInTheme(BuiltInTheme ThemeType, string DefaultFontFamily)
        {
            IReadOnlyDictionary<string, MGTheme> Themes = MGUI.Core.UI.XAML.ThemeDefinitionLoader.BuildThemes(BuiltInThemeDefinitions.Value, null, DefaultFontFamily);
            if (!Themes.TryGetValue(ThemeType.ToString(), out MGTheme Theme))
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

        private void ApplyFrom(MGTheme Source)
        {
            foreach (MGElementType Type in Enum.GetValues(typeof(MGElementType)))
            {
                SetBackgroundBrush(Type, Source.GetBackgroundBrush(Type));
            }

            _ControlTemplateMappings.Clear();
            foreach (KeyValuePair<MGElementType, string> item in Source.ControlTemplateMappings)
            {
                _ControlTemplateMappings[item.Key] = item.Value;
            }

            _ControlTemplateMappingsByType.Clear();
            foreach (KeyValuePair<Type, string> item in Source.ControlTemplateTypeMappings)
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
            foreach (ThemeManagedFillBrush Background in Source.ListBoxItemAlternatingRowBackgrounds)
            {
                ListBoxItemAlternatingRowBackgrounds.Add(new ThemeManagedFillBrush(Background.GetValue(true)));
            }

            TreeViewSelectionBackground.Value = Source.TreeViewSelectionBackground.GetValue(true);
            TreeViewSelectionForeground = Source.TreeViewSelectionForeground;
            TreeViewExpanderArrowColor = Source.TreeViewExpanderArrowColor;
            TreeViewBorderBrush = Source.TreeViewBorderBrush?.Copy();
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
            Window.BorderBrush = Source.Window.BorderBrush?.Copy();
            Window.TitleBarPadding = Source.Window.TitleBarPadding;
            Window.TitleBarMinHeight = Source.Window.TitleBarMinHeight;
            Window.CloseButtonBackground = Source.Window.CloseButtonBackground?.Copy();
            Window.CloseButtonBorderBrush = Source.Window.CloseButtonBorderBrush?.Copy();
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
            Overlay.BorderBrush = Source.Overlay.BorderBrush?.Copy();
            Overlay.CloseButtonBackground = Source.Overlay.CloseButtonBackground?.Copy();
            Overlay.CloseButtonBorderBrush = Source.Overlay.CloseButtonBorderBrush?.Copy();
            Overlay.CloseButtonBorderThickness = Source.Overlay.CloseButtonBorderThickness;
            Overlay.CloseButtonPadding = Source.Overlay.CloseButtonPadding;
            Overlay.CloseButtonMinWidth = Source.Overlay.CloseButtonMinWidth;
            Overlay.CloseButtonMinHeight = Source.Overlay.CloseButtonMinHeight;

            ContextMenu.Padding = Source.ContextMenu.Padding;
            ContextMenu.BorderBrush = Source.ContextMenu.BorderBrush?.Copy();
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
            ListBox.TitleBorderBrush = Source.ListBox.TitleBorderBrush?.Copy();
            ListBox.TitleBorderThickness = Source.ListBox.TitleBorderThickness;
            ListBox.InnerBorderBrush = Source.ListBox.InnerBorderBrush?.Copy();
            ListBox.InnerBorderThickness = Source.ListBox.InnerBorderThickness;
            ListBox.ScrollViewerPadding = Source.ListBox.ScrollViewerPadding;
            ListBox.ItemsPanelBorderBrush = Source.ListBox.ItemsPanelBorderBrush?.Copy();
            ListBox.ItemsPanelBorderThickness = Source.ListBox.ItemsPanelBorderThickness;

            ListView.HeaderForeground = Source.ListView.HeaderForeground?.GetCopy();
            ListView.GridLineBrush = Source.ListView.GridLineBrush?.Copy();

            PropertyGrid.Padding = Source.PropertyGrid.Padding;
            PropertyGrid.BorderBrush = Source.PropertyGrid.BorderBrush?.Copy();
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
            PropertyGrid.RowSeparatorBrush = Source.PropertyGrid.RowSeparatorBrush?.Copy();
            PropertyGrid.InvalidEditorBorderBrush = Source.PropertyGrid.InvalidEditorBorderBrush?.Copy();

            ComboBox.Padding = Source.ComboBox.Padding;
            ComboBox.MinHeight = Source.ComboBox.MinHeight;
            ComboBox.BorderBrush = Source.ComboBox.BorderBrush?.Copy();
            ComboBox.DropdownArrowMargin = Source.ComboBox.DropdownArrowMargin;
            ComboBox.DropdownMinWidth = Source.ComboBox.DropdownMinWidth;
            ComboBox.DropdownBorderThickness = Source.ComboBox.DropdownBorderThickness;
            ComboBox.DropdownBorderBrush = Source.ComboBox.DropdownBorderBrush?.Copy();
            ComboBox.DropdownPadding = Source.ComboBox.DropdownPadding;
            ComboBox.DropdownScrollViewerPadding = Source.ComboBox.DropdownScrollViewerPadding;
            ComboBox.DropdownItemsSpacing = Source.ComboBox.DropdownItemsSpacing;

            TreeViewTemplate.ScrollViewerPadding = Source.TreeViewTemplate.ScrollViewerPadding;
            TreeViewTemplate.ItemsPanelPadding = Source.TreeViewTemplate.ItemsPanelPadding;
            TreeViewTemplate.ItemsPanelSpacing = Source.TreeViewTemplate.ItemsPanelSpacing;

            TabControl.Padding = Source.TabControl.Padding;
            TabControl.BorderBrush = Source.TabControl.BorderBrush?.Copy();
            TabControl.BorderThickness = Source.TabControl.BorderThickness;
            TabControl.HeadersSpacing = Source.TabControl.HeadersSpacing;

            Docking.TabNormalBackground = Source.Docking.TabNormalBackground?.Copy();
            Docking.TabHoverBackground = Source.Docking.TabHoverBackground?.Copy();
            Docking.TabActiveBackground = Source.Docking.TabActiveBackground?.Copy();
            Docking.TabActiveAccentColor = Source.Docking.TabActiveAccentColor;
            Docking.TabHoverAccentColor = Source.Docking.TabHoverAccentColor;
            Docking.TabActiveTextColor = Source.Docking.TabActiveTextColor;
            Docking.TabInactiveTextColor = Source.Docking.TabInactiveTextColor;
            Docking.TabActiveIconColor = Source.Docking.TabActiveIconColor;
            Docking.TabInactiveIconColor = Source.Docking.TabInactiveIconColor;
            Docking.AutoHideDrawerBackground = Source.Docking.AutoHideDrawerBackground?.Copy();
            Docking.AutoHideDrawerHeaderBackground = Source.Docking.AutoHideDrawerHeaderBackground?.Copy();
            Docking.AutoHideButtonBackground = Source.Docking.AutoHideButtonBackground?.Copy();
            Docking.AutoHideHeaderTextColor = Source.Docking.AutoHideHeaderTextColor;
            Docking.AutoHideIconColor = Source.Docking.AutoHideIconColor;
            Docking.AutoHideBorderColor = Source.Docking.AutoHideBorderColor;
            Docking.AutoHideGripColor = Source.Docking.AutoHideGripColor;
            Docking.AutoHideStripBackground = Source.Docking.AutoHideStripBackground?.Copy();
            Docking.AutoHideStripButtonBackground = Source.Docking.AutoHideStripButtonBackground?.Copy();
            Docking.AutoHideStripTextColor = Source.Docking.AutoHideStripTextColor;
            Docking.AutoHideStripSeparatorColor = Source.Docking.AutoHideStripSeparatorColor;
            Docking.SplitterNormalBrush = Source.Docking.SplitterNormalBrush?.Copy();
            Docking.SplitterHoverBrush = Source.Docking.SplitterHoverBrush?.Copy();
            Docking.SplitterPressedBrush = Source.Docking.SplitterPressedBrush?.Copy();
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
        }
    }
}
