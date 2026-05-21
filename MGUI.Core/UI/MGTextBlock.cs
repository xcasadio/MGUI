using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Helpers;
using MGUI.Core.UI.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using MGUI.Shared.Rendering;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Input.Mouse;
using MGUI.Core.UI.Responsive;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI
{
    public class MGTextBlock : MGElement, ITextMeasurer
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _AutoWidthFromContent;
        public bool AutoWidthFromContent
        {
            get => _AutoWidthFromContent;
            set
            {
                if (_AutoWidthFromContent != value)
                {
                    _AutoWidthFromContent = value;
                    InvokeLayoutChanged();
                    NPC(nameof(AutoWidthFromContent));
                }
            }
        }

        protected internal override bool IgnorePreferredWidthDuringMeasure => AutoWidthFromContent;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _FontFamily;
        /// <summary>To set this value, use <see cref="TrySetFont(string, int)"/></summary>
        public string FontFamily { get => _FontFamily; set => _ = TrySetFont(value, FontSize); }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _FontSize;
        /// <summary>To set this value, use <see cref="TrySetFont(string, int)"/> or <see cref="TrySetFontSize(int)"/></summary>
        public int FontSize { get => _FontSize; set => _ = TrySetFontSize(value); }

        /// <summary>
        /// Cached space-character width from <see cref="RF_Regular"/>.
        /// Consumed by <see cref="MGUI.Core.UI.Text.TextRenderInfo"/> for default caret sizing.
        /// </summary>
        internal float SpaceWidth { get; private set; }

        // ── ITextMeasurementEngine-backed resolved fonts (one per style variant) ─────────────
        /// <summary>Shortcut to the active <see cref="ITextMeasurementEngine"/> from the parent Desktop.</summary>
        private ITextMeasurementEngine TextEngine => GetTextEngine();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool? _UseResponsiveTextScale;
        public bool? UseResponsiveTextScale
        {
            get => _UseResponsiveTextScale;
            set
            {
                if (_UseResponsiveTextScale != value)
                {
                    _UseResponsiveTextScale = value;
                    RefreshTextEngine();
                    NPC(nameof(UseResponsiveTextScale));
                }
            }
        }

        private bool IsResponsiveTextScaleEnabled => UseResponsiveTextScale ?? IsResponsiveLayoutEnabled;
        private float ResponsiveTextScaleFactor => IsResponsiveTextScaleEnabled ? GetDesktop().ResponsiveMetrics.TextScaleFactor : 1.0f;
        private float EffectiveLinePadding => IsResponsiveTextScaleEnabled ? LinePadding * ResponsiveTextScaleFactor : LinePadding;
        private int EffectiveFontSize => Math.Max(1, UIResponsiveMath.ScaleInt(_FontSize, ResponsiveTextScaleFactor));

        internal ResolvedFont RF_Regular    { get; private set; }
        internal ResolvedFont RF_Bold       { get; private set; }
        internal ResolvedFont RF_Italic     { get; private set; }
        internal ResolvedFont RF_BoldItalic { get; private set; }

        /// <summary>Returns the <see cref="ResolvedFont"/> corresponding to the given style flags.</summary>
        internal ResolvedFont GetResolvedFont(bool IsBold, bool IsItalic)
        {
            if (!IsBold && !IsItalic)
            {
                return RF_Regular;
            }

            if (IsBold && IsItalic)
            {
                return RF_BoldItalic;
            }

            if (IsBold)
            {
                return RF_Bold;
            }

            return RF_Italic;
        }

        public bool TrySetFontSize(int FontSize) => TrySetFont(FontFamily, FontSize);
        public bool TrySetFont(string FontFamily, int FontSize)
        {
            if (this.FontFamily != FontFamily || this.FontSize != FontSize)
            {
                string PreviousFontFamily = FontFamily;
                int PreviousFontSize = FontSize;

                // Validate that the requested font exists before committing the change
                int effectiveFontSize = Math.Max(1, UIResponsiveMath.ScaleInt(FontSize, ResponsiveTextScaleFactor));
                ResolvedFont validationFont = TextEngine.ResolveFont(new FontSpec(FontFamily, effectiveFontSize, CustomFontStyles.Normal));
                if (!validationFont.IsAvailable || validationFont.IsFallback)
                {
                    return false;
                }

                _FontFamily = FontFamily;
                _FontSize = FontSize;

                // Resolve text-measurement handles for all 4 style variants
                ITextMeasurementEngine engine = TextEngine;
                RF_Regular    = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Normal));
                RF_Bold       = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Bold));
                RF_Italic     = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Italic));
                RF_BoldItalic = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Bold | CustomFontStyles.Italic));

                SpaceWidth = RF_Regular.SpaceWidth;

                InvokeLayoutChanged();

                if (PreviousFontFamily != this.FontFamily)
                {
                    NPC(nameof(FontFamily));
                }

                if (PreviousFontSize != this.FontSize)
                {
                    NPC(nameof(FontSize));
                }

                return true;
            }
            else
            {
                return true;
            }
        }

        protected internal override UIInvalidationKind GetThemeInvalidation(MGTheme PreviousTheme, MGTheme CurrentTheme)
        {
            string PreviousDefaultFontFamily = PreviousTheme?.FontSettings.DefaultFontFamily ?? GetDesktop().DefaultFontFamily;
            int PreviousDefaultFontSize = PreviousTheme?.FontSettings.DefaultFontSize ?? FontSize;
            bool UsesThemeFontFamily = string.Equals(FontFamily, PreviousDefaultFontFamily, StringComparison.Ordinal);
            bool UsesThemeFontSize = FontSize == PreviousDefaultFontSize;
            return UsesThemeFontFamily || UsesThemeFontSize
                ? UIInvalidationKind.Measure | UIInvalidationKind.Arrange | UIInvalidationKind.Draw
                : UIInvalidationKind.Draw;
        }

        protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
        {
            string PreviousDefaultFontFamily = PreviousTheme?.FontSettings.DefaultFontFamily ?? GetDesktop().DefaultFontFamily;
            string CurrentDefaultFontFamily = CurrentTheme?.FontSettings.DefaultFontFamily ?? GetDesktop().DefaultFontFamily;
            int PreviousDefaultFontSize = PreviousTheme?.FontSettings.DefaultFontSize ?? FontSize;
            int CurrentDefaultFontSize = CurrentTheme?.FontSettings.DefaultFontSize ?? FontSize;

            bool UsesThemeFontFamily = string.Equals(FontFamily, PreviousDefaultFontFamily, StringComparison.Ordinal);
            bool UsesThemeFontSize = FontSize == PreviousDefaultFontSize;
            if (UsesThemeFontFamily || UsesThemeFontSize)
            {
                _ = TrySetFont(UsesThemeFontFamily ? CurrentDefaultFontFamily : FontFamily, UsesThemeFontSize ? CurrentDefaultFontSize : FontSize);
            }

            NPC(nameof(ActualForeground));
        }

        /// <summary>
        /// Re-resolves all four font-style handles from the currently active <see cref="ITextMeasurementEngine"/>
        /// and invalidates both the layout and the self-measurement cache.<para/>
        /// Called by <see cref="MGDesktop.RecalculateTextLayouts"/> after a runtime engine switch so
        /// that new engine metrics (e.g. different scale factors) are reflected immediately.
        /// </summary>
        internal void RefreshTextEngine()
        {
            ITextMeasurementEngine engine = TextEngine;
            RF_Regular    = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Normal));
            RF_Bold       = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Bold));
            RF_Italic     = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Italic));
            RF_BoldItalic = engine.ResolveFont(new FontSpec(_FontFamily, EffectiveFontSize, CustomFontStyles.Bold | CustomFontStyles.Italic));
            SpaceWidth    = RF_Regular.SpaceWidth;
            InvokeLayoutChanged(); // clears RecentSelfMeasurements + fires LayoutChanged
        }

        #region Font Style
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsBold;
        public bool IsBold
        {
            get => _IsBold;
            set
            {
                if (_IsBold != value)
                {
                    _IsBold = value;
                    if (!string.IsNullOrEmpty(Text))
                    {
                        UpdateRuns();
                        InvokeLayoutChanged();
                    }
                    NPC(nameof(IsBold));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsItalic;
        public bool IsItalic
        {
            get => _IsItalic;
            set
            {
                if (_IsItalic != value)
                {
                    _IsItalic = value;
                    if (!string.IsNullOrEmpty(Text))
                    {
                        UpdateRuns();
                        InvokeLayoutChanged();
                    }
                    NPC(nameof(IsItalic));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsUnderlined;
        public bool IsUnderlined
        {
            get => _IsUnderlined;
            set
            {
                if (_IsUnderlined != value)
                {
                    _IsUnderlined = value;
                    if (!string.IsNullOrEmpty(Text))
                    {
                        UpdateRuns();
                        UpdateLines();
                    }
                    NPC(nameof(IsUnderlined));
                }
            }
        }

        private MGTextRunUnderlineConfig DefaultUnderlineSettings => new MGTextRunUnderlineConfig(IsUnderlined);
        private MGTextRunShadowConfig DefaultShadowSettings => 
            IsShadowed ? new MGTextRunShadowConfig(ShadowColor ?? GetTheme().FontSettings.DefaultFontShadowColor, ShadowOffset?.ToVector2() ?? GetTheme().FontSettings.DefaultFontShadowOffset.ToVector2()) : default;

        private MGTextRunConfig DefaultTextRunSettings => new(IsBold, IsItalic, 1, null, DefaultUnderlineSettings, default, DefaultShadowSettings);
        #endregion Font Style

        #region Shadow
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsShadowed;
        public bool IsShadowed
        {
            get => _IsShadowed;
            set
            {
                if (_IsShadowed != value)
                {
                    _IsShadowed = value;
                    if (!string.IsNullOrEmpty(Text))
                    {
                        UpdateRuns();
                        UpdateLines();
                    }
                    NPC(nameof(IsShadowed));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Point? _ShadowOffset;
        /// <summary>Only relevant if <see cref="IsShadowed"/> is true.<br/>
        /// Determines the offset applied to <see cref="Text"/> when drawing the shadow.<br/>
        /// If null, uses <see cref="ThemeFontSettings.DefaultFontShadowOffset"/> from <see cref="MGTheme.FontSettings"/><para/>
        /// Warning - shadowed text does not affect the layout bounds of this <see cref="MGTextBlock"/>.<br/>
        /// Using a large <see cref="ShadowOffset"/> value may result in parts of the shadow being clipped to <see cref="MGElement.ActualLayoutBounds"/>.</summary>
        public Point? ShadowOffset
        {
            get => _ShadowOffset;
            set
            {
                if (_ShadowOffset != value)
                {
                    _ShadowOffset = value;
                    if (!string.IsNullOrEmpty(Text))
                    {
                        UpdateRuns();
                        UpdateLines();
                    }
                    NPC(nameof(ShadowOffset));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color? _ShadowColor;
        /// <summary>Only relevant if <see cref="IsShadowed"/> is true.<br/>
        /// If null, uses <see cref="ThemeFontSettings.DefaultFontShadowColor"/> from <see cref="MGTheme.FontSettings"/></summary>
        public Color? ShadowColor
        {
            get => _ShadowColor;
            set
            {
                if (_ShadowColor != value)
                {
                    _ShadowColor = value;
                    if (!string.IsNullOrEmpty(Text))
                    {
                        UpdateRuns();
                        UpdateLines();
                    }
                    NPC(nameof(ShadowColor));
                }
            }
        }
        #endregion Shadow

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private VisualStateSetting<Color?> _Foreground;
        /// <summary>The foreground color to use when rendering the text.<br/>
        /// If the text is formatted with color codes (such as '[color=Red]Hello World[/color]'), the color specified in the <see cref="MGTextRun"/> will take precedence.<para/>
        /// If the value for the current <see cref="MGElement.VisualState"/> is null, will attempt to resolve the value from <see cref="MGElement.DerivedDefaultTextForeground"/>, or <see cref="MGTheme.TextBlockFallbackForeground"/> if no value is specified.<para/>
        /// See also:<br/><see cref="MGElement.DefaultTextForeground"/><br/><see cref="MGElement.DerivedDefaultTextForeground"/><br/><see cref="ActualForeground"/><br/>
        /// <see cref="MGTheme.TextBlockFallbackForeground"/><br/><see cref="MGWindow.Theme"/><br/><see cref="MGDesktop.Theme"/></summary>
        public VisualStateSetting<Color?> Foreground
        {
            get => _Foreground;
            set
            {
                if (_Foreground != value)
                {
                    _Foreground = value;
                    NPC(nameof(Foreground));
                    NPC(nameof(ActualForeground));
                }
            }
        }

        public Color ActualForeground => Foreground.GetValue(VisualState.Primary) ?? DerivedDefaultTextForeground ?? GetTheme().TextBlockFallbackForeground.GetValue(false).GetValue(VisualState.Primary);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double? _TextProgress;
        /// <summary>Determines what percentage of the <see cref="Text"/> is currently being rendered.<br/>
        /// If <see langword="null"/>, all <see cref="Text"/> will be drawn.<para/>
        /// Min value: 0.0 (0%)<br/>Max value: 1.0 (100%)<para/>
        /// Default value: <see langword="null"/><para/>
        /// See also: <see cref="TextCharactersPerSecond"/></summary>
        public double? TextProgress
        {
            get => _TextProgress;
            set
            {
                if (_TextProgress != value)
                {
                    _TextProgress = value;
                    NPC(nameof(TextProgress));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double? _TextCharactersPerSecond;
        /// <summary>Determines how many characters in this <see cref="MGTextBlock"/>'s <see cref="Text"/> should appear per second.<para/>
        /// If not <see langword="null"/>, this will automatically update <see cref="TextProgress"/>.<br/>
        /// This property is typically used to make text appear slowly over time, such as to mimic an NPC speaking. (Note: People typically speak at a rate of about 17 CPS)<para/>
        /// Default value: <see langword="null"/><para/>
        /// See also: <see cref="TextProgress"/></summary>
        public double? TextCharactersPerSecond
        {
            get => _TextCharactersPerSecond;
            set
            {
                if (_TextCharactersPerSecond != value)
                {
                    _TextCharactersPerSecond = value;
                    NPC(nameof(TextCharactersPerSecond));
                    TextProgress = TextCharactersPerSecond.HasValue ? 0.0 : null;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _AllowsInlineFormatting = true;
        /// <summary>If true, <see cref="Text"/> can contain formatting codes such as "[bold]...[/bold]" or "[color=green]...[/color]" etc.<br/>
        /// If false, all formatting codes within <see cref="Text"/> will be treated as literal strings instead of affecting how the text is rendered.<para/>
        /// Default value: true</summary>
        public bool AllowsInlineFormatting
        {
            get => _AllowsInlineFormatting;
            set
            {
                if (_AllowsInlineFormatting != value)
                {
                    _AllowsInlineFormatting = value;
                    UpdateRuns();
                    InvokeLayoutChanged();
                    NPC(nameof(AllowsInlineFormatting));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ReadOnlyCollection<MGTextRun> _ExplicitRuns;
        /// <summary>If set, these runs are rendered directly instead of parsing <see cref="Text"/>.
        /// This provides a compact programmatic seam for annotated text without requiring a document model.</summary>
        public ReadOnlyCollection<MGTextRun> ExplicitRuns
        {
            get => _ExplicitRuns;
            private set
            {
                if (!ReferenceEquals(_ExplicitRuns, value))
                {
                    _ExplicitRuns = value;
                    NPC(nameof(ExplicitRuns));
                }
            }
        }

        public bool HasExplicitRuns => ExplicitRuns != null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _Text;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _HasStableTextFootprint;
        /// <summary>
        /// True when callers have explicitly reserved enough layout space for expected text updates.
        /// Stable footprint text may use <see cref="MGTextInvalidationMode.ContentOnly"/> or
        /// <see cref="MGTextInvalidationMode.ReflowLocal"/> without forcing parent layout; ordinary labels should keep the default false value.
        /// </summary>
        public bool HasStableTextFootprint
        {
            get => _HasStableTextFootprint;
            set
            {
                if (_HasStableTextFootprint != value)
                {
                    _HasStableTextFootprint = value;
                    NPC(nameof(HasStableTextFootprint));
                }
            }
        }

        public string Text
        {
            get => _Text;
            set => SetText(value, MGTextInvalidationMode.RelayoutParent);
        }

        /// <param name="SuppressLayoutChanged">If true, <see cref="Text"/> will be set without calling <see cref="InvokeLayoutChanged"/><para/>
        /// Intended to be used for performance purposes when changing the text value, without actually changing the text layout.<br/>
        /// For example, changing text from: "Hello World" to "Hello [bg=Red]World[/bg]" does not affect the rendered text's layout/size.</param>
        public void SetText(string Value, bool SuppressLayoutChanged = false)
            => SetTextCore(Value, GetLegacyTextInvalidationMode(SuppressLayoutChanged), true);

        /// <summary>Sets <see cref="Text"/> and applies the requested text invalidation contract.</summary>
        public void SetText(string Value, MGTextInvalidationMode InvalidationMode)
            => SetTextCore(Value, InvalidationMode, false);

        private void SetTextCore(string Value, MGTextInvalidationMode RequestedInvalidationMode, bool AllowLegacyLocalInvalidation)
        {
            bool HadExplicitRuns = HasExplicitRuns;
            if (_Text != Value || HadExplicitRuns)
            {
                _Text = Value;
                if (HadExplicitRuns)
                {
                    ExplicitRuns = null;
                }

                ApplyTextMutation(RequestedInvalidationMode, AllowLegacyLocalInvalidation);

                NPC(nameof(Text));
            }
        }

        /// <summary>Replaces parsed <see cref="Text"/> content with explicit runs.
        /// This is intended for compact chat/log/debug annotations without introducing a full document editor.</summary>
        public void SetTextRuns(IEnumerable<MGTextRun> Value, bool SuppressLayoutChanged = false)
            => SetTextRunsCore(Value, GetLegacyTextInvalidationMode(SuppressLayoutChanged), true);

        /// <summary>Replaces parsed <see cref="Text"/> content with explicit runs and applies the requested text invalidation contract.</summary>
        public void SetTextRuns(IEnumerable<MGTextRun> Value, MGTextInvalidationMode InvalidationMode)
            => SetTextRunsCore(Value, InvalidationMode, false);

        private void SetTextRunsCore(IEnumerable<MGTextRun> Value, MGTextInvalidationMode RequestedInvalidationMode, bool AllowLegacyLocalInvalidation)
        {
            ExplicitRuns = Value?.ToList().AsReadOnly();
            ApplyTextMutation(RequestedInvalidationMode, AllowLegacyLocalInvalidation);
        }

        public void ClearTextRuns(bool SuppressLayoutChanged = false)
            => ClearTextRunsCore(GetLegacyTextInvalidationMode(SuppressLayoutChanged), true);

        public void ClearTextRuns(MGTextInvalidationMode InvalidationMode)
            => ClearTextRunsCore(InvalidationMode, false);

        private void ClearTextRunsCore(MGTextInvalidationMode RequestedInvalidationMode, bool AllowLegacyLocalInvalidation)
        {
            if (!HasExplicitRuns)
            {
                return;
            }

            ExplicitRuns = null;
            ApplyTextMutation(RequestedInvalidationMode, AllowLegacyLocalInvalidation);
        }

        private static MGTextInvalidationMode GetLegacyTextInvalidationMode(bool SuppressLayoutChanged)
            => SuppressLayoutChanged ? MGTextInvalidationMode.ReflowLocal : MGTextInvalidationMode.RelayoutParent;

        private void ApplyTextMutation(MGTextInvalidationMode RequestedInvalidationMode, bool AllowLegacyLocalInvalidation)
        {
            UpdateRuns();
            MGTextInvalidationMode ResolvedInvalidationMode = ResolveTextInvalidationMode(RequestedInvalidationMode, AllowLegacyLocalInvalidation);
            ApplyResolvedTextInvalidation(ResolvedInvalidationMode);
        }

        private MGTextInvalidationMode ResolveTextInvalidationMode(MGTextInvalidationMode RequestedInvalidationMode, bool AllowLegacyLocalInvalidation)
        {
            if (RequestedInvalidationMode == MGTextInvalidationMode.RelayoutParent)
            {
                return MGTextInvalidationMode.RelayoutParent;
            }

            if (!AllowLegacyLocalInvalidation && !HasStableTextFootprint)
            {
                return MGTextInvalidationMode.RelayoutParent;
            }

            if (!HasKnownTextLayoutWidth())
            {
                return MGTextInvalidationMode.RelayoutParent;
            }

            return RequestedInvalidationMode;
        }

        private bool HasKnownTextLayoutWidth()
            => LayoutBounds.Width > 0;

        private void ApplyResolvedTextInvalidation(MGTextInvalidationMode InvalidationMode)
        {
            switch (InvalidationMode)
            {
                case MGTextInvalidationMode.ContentOnly:
                case MGTextInvalidationMode.ReflowLocal:
                    UpdateLines();
                    break;

                case MGTextInvalidationMode.RelayoutParent:
                    InvokeLayoutChanged();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(InvalidationMode), InvalidationMode, null);
            }
        }

        private bool _IsTrackingMouseClicks;
        private bool IsTrackingMouseClicks
        {
            get => _IsTrackingMouseClicks;
            set
            {
                if (_IsTrackingMouseClicks != value)
                {
                    _IsTrackingMouseClicks = value;

                    if (IsTrackingMouseClicks)
                    {
                        MouseHandler.ReleasedInside += Mouse_ReleasedInside;
                    }
                    else if (_MouseHandler != null)
                    {
                        MouseHandler.ReleasedInside -= Mouse_ReleasedInside;
                    }
                }
            }
        }

        private void Mouse_ReleasedInside(object sender, BaseMouseReleasedEventArgs e)
        {
            if (e.IsLMB)
            {
                //  Handle the '[Action]' inlined formatting code
                //  EX: Text="[Action=Action1]Click here[/Action] but not here"
                //  Then clicking the substring "Click here" should invoke the command named "Action1" (in MGResources.Commands)
                if (ActionBounds.Any())
                {
                    Point MousePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, InputTracker.Mouse.CurrentPosition);

                    foreach (var KVP in ActionBounds)
                    {
                        string CommandName = KVP.Key;
                        if (GetResources().TryGetCommand(CommandName, out Action<MGElement> Command))
                        {
                            foreach (Rectangle Bounds in KVP.Value)
                            {
                                if (Bounds.Contains(MousePosition))
                                {
                                    Command(this);
                                    e.SetHandledBy(this, false);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private ReadOnlyCollection<MGTextRun> SanitizeRuns(IEnumerable<MGTextRun> SourceRuns)
        {
            MGDesktop Desktop = GetDesktop();
            List<MGTextRun> Temp = new();

            foreach (MGTextRun Run in SourceRuns ?? Enumerable.Empty<MGTextRun>())
            {
                MGTextRun CurrentRun = Run;

                //  If a TextRunImage didn't specify destination dimensions, use the default size of the image
                if (Run.RunType == TextRunType.Image && Run is MGTextRunImage ImageRun &&
                    ImageRun.TargetWidth <= 0 && ImageRun.TargetHeight <= 0)
                {
                    (int? DefaultWidth, int? DefaultHeight) = Desktop.Resources.GetTextureDimensions(ImageRun.SourceName);
                    CurrentRun = new MGTextRunImage(ImageRun.SourceName, DefaultWidth ?? 0, DefaultHeight ?? 0, ImageRun.ToolTipId, ImageRun.ActionId);
                }

                Temp.Add(CurrentRun);
            }

            return Temp.AsReadOnly();
        }

        private void UpdateRuns()
        {
            if (HasExplicitRuns)
            {
                Runs = SanitizeRuns(ExplicitRuns);
            }
            else if (AllowsInlineFormatting)
            {
                IEnumerable<MGTextRun> ParsedRuns = MGTextRun.ParseRuns(Text, DefaultTextRunSettings);
                Runs = SanitizeRuns(ParsedRuns);
            }
            else
            {
                List<FTTokenMatch> Tokens = FTTokenizer.TokenizeLineBreaks(Text, true).ToList();
                Runs = MGTextRun.ParseRuns(Tokens, DefaultTextRunSettings).ToList().AsReadOnly();
            }

            IsTrackingMouseClicks = Runs.Any(x => x.HasAction);

            NPC(nameof(Runs));
            NumCharacters = Runs.Where(x => x is MGTextRunText).Cast<MGTextRunText>().Sum(x => x.Text.Length);
        }

        public ReadOnlyCollection<MGTextRun> Runs { get; private set; }
        public ReadOnlyCollection<MGTextLine> Lines { get; private set; }
        private int NumCharacters { get; set; }

        internal void UpdateLines()
        {
            Lines = MGTextLine.ParseLines(this, Math.Max(0, LayoutBounds.Width - HorizontalPadding), WrapText, Runs, IgnoreEmptySpaceLines).ToList().AsReadOnly();
            NPC(nameof(Lines));
        }

        internal float GetRenderedTextHeight(IReadOnlyList<MGTextLine> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return 0;
            }

            return lines.Sum(x => x.LineTotalHeight) + EffectiveLinePadding * Math.Max(0, lines.Count - 1);
        }

        internal Rectangle GetPaddedLayoutBounds(Rectangle layoutBounds)
        {
            Thickness padding = ResolvedPadding;
            return new Rectangle(layoutBounds.Left + padding.Left, layoutBounds.Top + padding.Top,
                Math.Max(0, layoutBounds.Width - padding.Left - padding.Right),
                Math.Max(0, layoutBounds.Height - padding.Top - padding.Bottom));
        }

        internal float GetRenderedTextStartY(Rectangle layoutBounds, IReadOnlyList<MGTextLine> lines)
        {
            return GetRenderedTextStartY(layoutBounds, GetRenderedTextHeight(lines));
        }

        internal float GetRenderedTextStartY(Rectangle layoutBounds, float renderedTextHeight)
        {
            Rectangle paddedBounds = GetPaddedLayoutBounds(layoutBounds);
            if (renderedTextHeight <= 0)
            {
                return paddedBounds.Top;
            }

            Rectangle alignedBounds = ApplyAlignment(paddedBounds, HorizontalAlignment.Stretch, VerticalContentAlignment,
                new Size(paddedBounds.Width, Math.Min(paddedBounds.Height, (int)Math.Ceiling(renderedTextHeight))));
            return alignedBounds.Top;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _WrapText;
        public bool WrapText
        {
            get => _WrapText;
            set
            {
                if (_WrapText != value)
                {
                    _WrapText = value;
                    InvokeLayoutChanged();
                    NPC(nameof(WrapText));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private float _LinePadding;
        /// <summary>Additional vertical space between each line of text</summary>
        public float LinePadding
        {
            get => _LinePadding;
            set
            {
                if (_LinePadding != value)
                {
                    _LinePadding = value;
                    InvokeLayoutChanged();
                    NPC(nameof(LinePadding));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _MinLines;
        /// <summary>The minimum # of lines to display, regardless of how many lines the actual text content requires.<para/>
        /// Default value: 0<para/>
        /// See also: <see cref="MaxLines"/></summary>
        public int MinLines
        {
            get => _MinLines;
            set
            {
                if (_MinLines != value)
                {
                    _MinLines = value;
                    LayoutChanged(this, true);
                    NPC(nameof(MinLines));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _MaxLines;
        /// <summary>The maximum # of lines to display, regardless of how many lines the actual text content requires.<br/>
        /// Use null to indicate there is no maximum.<para/>
        /// Default value: null<para/>
        /// See also: <see cref="MinLines"/></summary>
        public int? MaxLines
        {
            get => _MaxLines;
            set
            {
                if (_MaxLines != value)
                {
                    _MaxLines = value;
                    LayoutChanged(this, true);
                    NPC(nameof(MaxLines));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private HorizontalAlignment _TextAlignment;
        public HorizontalAlignment TextAlignment
        {
            get => _TextAlignment;
            set
            {
                if (_TextAlignment != value)
                {
                    _TextAlignment = value;
                    NPC(nameof(TextAlignment));
                }
            }
        }

        /// <param name="FontSize">If null, uses the font size specified by <see cref="ThemeFontSettings.DefaultFontSize"/>.<para/>
        /// See also:<br/><see cref="MGWindow.Theme"/><br/><see cref="MGDesktop.Theme"/><br/><see cref="MGTheme.FontSettings"/></param>
        public MGTextBlock(MGWindow Window, string Text, Color? Foreground = null, int? FontSize = null, bool AllowsInlineFormatting = true)
            : base(Window, MGElementType.TextBlock)
        {
            using (BeginInitializing())
            {
                MGDesktop Desktop = GetDesktop();
                MGTheme Theme = GetTheme();
                WrapText = Theme.DefaultTextBlockWrapText;
                AutoWidthFromContent = Theme.DefaultTextBlockAutoWidthFromContent;
                if (!TrySetFont(Theme.FontSettings.DefaultFontFamily ?? Desktop.DefaultFontFamily, FontSize ?? GetTheme().FontSettings.DefaultFontSize))
                {
                    throw new ArgumentException("Default font not found.");
                }

                this.AllowsInlineFormatting = AllowsInlineFormatting;
                IsBold = false;
                IsItalic = false;
                IsUnderlined = false;
                this.Text = Text;
                MinLines = 0;
                MaxLines = null;
                this.Foreground = new VisualStateSetting<Color?>(Foreground, Foreground, Foreground);
                LinePadding = 2;
                TextAlignment = HorizontalAlignment.Left;
                Padding = new(1,1,1,1);
                VerticalContentAlignment = VerticalAlignment.Center;
                TextProgress = null;
                TextCharactersPerSecond = null;

                OnLayoutUpdated += (sender, e) => { UpdateLines(); };
            }
        }

        public override string ToString() => $"{base.ToString()}: \"{Text?.Truncate(100)}\"";

        /// <summary>
        /// Measures the rendered size of <paramref name="Text"/> using the active <see cref="ITextMeasurementEngine"/>.
        /// Delegates to <see cref="ITextMeasurementEngine.MeasureText"/> (whole-string) so that kerning is accounted
        /// for correctly.
        /// </summary>
        public Vector2 MeasureText(string Text, bool IsBold, bool IsItalic)
        {
            if (string.IsNullOrEmpty(Text))
            {
                return Vector2.Zero;
            }

            ResolvedFont resolved = GetResolvedFont(IsBold, IsItalic);
            if (resolved?.NativeFont == null)
            {
                return Vector2.Zero;
            }

            Vector2 measured = TextEngine.MeasureText(resolved, Text);
            // LineHeight from the engine may be 0 for some backends; fall back to resolved value.
            return new Vector2(measured.X, measured.Y > 0 ? measured.Y : resolved.LineHeight);
        }

        private readonly List<ElementMeasurement> RecentSelfMeasurements = new();
        private void CacheSelfMeasurement(ElementMeasurement Value)
        {
            while (RecentSelfMeasurements.Count > MeasurementCacheSize)
                RecentSelfMeasurements.RemoveAt(RecentSelfMeasurements.Count - 1);
            if (!RecentSelfMeasurements.Contains(Value))
            {
                RecentSelfMeasurements.Insert(0, Value);
            }
        }

        private bool TryGetCachedSelfMeasurement(Size AvailableSize, out Thickness? Result)
        {
            foreach (ElementMeasurement Measurement in RecentSelfMeasurements)
            {
                //  Not sure what logic is appropriate for this
                //  The thought process is, if we measured this textblock already with a larger available size,
                //      we should be able to re-use the measurement as long as the new available size is still >= whatever was previously requested.
                bool IsMatch = false;
                if (Measurement.AvailableSize == AvailableSize)
                {
                    IsMatch = true;
                }
                else if (Measurement.AvailableSize.Width == AvailableSize.Width)
                {
                    IsMatch = AvailableSize.Height >= Measurement.RequestedSize.Height;
                }
                else if (Measurement.IsAvailableSizeGreaterThanOrEqual(AvailableSize))
                {
                    IsMatch = Measurement.IsRequestedSizeLessThanOrEqual(AvailableSize);
                }

                if (IsMatch)
                {
                    Result = Measurement.RequestedSize;
                    return true;
                }
            }

            Result = null;
            return false;
        }

        private void InvokeLayoutChanged()
        {
            RecentSelfMeasurements.Clear();
            LayoutChanged(this, true);
        }

        /// <summary>If true, lines that consist of only a single whitespace character will be ignored when rendering wrapped text content.</summary>
        private const bool IgnoreEmptySpaceLines = true;

        public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
        {
            Size PaddedSize = AvailableSize.Subtract(PaddingSize, 0, 0);

            Size RemainingSize = new(
                Math.Max(0, PreferredWidth.HasValue ? PreferredWidth.Value - HorizontalPadding : PaddedSize.Width),
                Math.Max(0, PreferredHeight.HasValue ? PreferredHeight.Value - VerticalPadding : PaddedSize.Height)
            );

            SharedSize = new(0);
            if (TryGetCachedSelfMeasurement(RemainingSize, out Thickness? CachedMeasurement))
            {
                return CachedMeasurement.Value;
            }

            List<MGTextLine> Lines = MGTextLine.ParseLines(this, RemainingSize.Width, WrapText, Runs, IgnoreEmptySpaceLines).ToList();
            List<MGTextLine> MeasuredLines = Lines;
            if (MaxLines.HasValue && MeasuredLines.Count > MaxLines.Value)
            {
                MeasuredLines = MeasuredLines.Take(MaxLines.Value).ToList();
            }

            Vector2 Size = new(MeasuredLines.Select(x => x.LineWidth).DefaultIfEmpty(0).Max(), MeasuredLines.Sum(x => x.LineTotalHeight) + EffectiveLinePadding * Math.Max(0, MeasuredLines.Count - 1));
            if (MinLines > MeasuredLines.Count)
            {
                Size = Size.SetY(Size.Y + (MinLines - MeasuredLines.Count) * (RF_Regular.LineHeight + EffectiveLinePadding));
            }

            Thickness Measurement = new((int)Math.Ceiling(Size.X), (int)Math.Ceiling(Size.Y), 0, 0);

            ElementMeasurement SelfMeasurement = new(RemainingSize, Measurement, SharedSize, new(0));
            CacheSelfMeasurement(SelfMeasurement);
            return Measurement;
        }

        /// <summary>
        /// Key = the name of a named delegate to invoke when clicking within any of the given bounds.<br/>
        /// Value = the bounds of the text content that the named action corresponds to. Usually a list of 1 rectangle, but might be multiple if the bound text spans multiple lines.
        /// </summary>
        private readonly Dictionary<string, List<Rectangle>> ActionBounds = new();

        /// <summary>
        /// Key = the name of a ToolTip to reference,<br/>
        /// Value = the bounds of the text content that the tooltip is applied to. Usually a list of 1 rectangle, but might be multiple if the text spans multiple lines.
        /// </summary>
        private readonly Dictionary<string, List<Rectangle>> ToolTipBounds = new();

        protected override bool TryGetToolTip(out MGToolTip ToolTip)
        {
            if (ToolTipBounds.Any())
            {
                try
                {
                    Point MousePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, InputTracker.Mouse.CurrentPosition);

                    foreach (var KVP in ToolTipBounds)
                    {
                        string ToolTipName = KVP.Key;
                        if (ParentWindow.TryGetNamedToolTip(ToolTipName, out ToolTip))
                        {
                            foreach (Rectangle Bounds in KVP.Value)
                            {
                                if (Bounds.Contains(MousePosition))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                finally { ToolTipBounds.Clear(); }
            }

            ToolTip = null;
            return false;
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            base.UpdateSelf(UA);

            if (ActionBounds.Any())
            {
                ActionBounds.Clear();
            }

            //  Update TextProgress (makes the Text appear slowly over time instead of all at once)
            if (TextCharactersPerSecond.HasValue && NumCharacters > 0 && (!TextProgress.HasValue || TextProgress.Value < 1.0))
            {
                double ElapsedCharacters = UA.BA.FrameElapsed.TotalSeconds * TextCharactersPerSecond.Value;
                double ElapsedProgress = ElapsedCharacters / NumCharacters;
                TextProgress = TextProgress.HasValue ? TextProgress.Value + ElapsedProgress : ElapsedProgress;
            }
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            MGDesktop Desktop = GetDesktop();
            IUIDrawContext DT = DA.DT;
            float Opacity = DA.Opacity;
            Color DefaultForeground = ActualForeground;

            Matrix Transform = Matrix.CreateTranslation(new Vector3(DA.Offset.ToVector2(), 0));
            float ImageSizeScalar = 1.0f;

            ActionBounds.Clear();
            ToolTipBounds.Clear();

            int RemainingCharacters = TextProgress.HasValue ? (int)(TextProgress.Value * NumCharacters) : NumCharacters;

            Thickness padding = ResolvedPadding;
            Rectangle paddedBounds = GetPaddedLayoutBounds(LayoutBounds);
            float CurrentY = GetRenderedTextStartY(LayoutBounds, Lines);

            foreach (MGTextLine Line in Lines)
            {
                Rectangle LineBounds = new(paddedBounds.Left, (int)CurrentY, paddedBounds.Width, (int)Line.LineTotalHeight);
                float CurrentX = ApplyAlignment(LineBounds, TextAlignment, VerticalContentAlignment, new Size((int)Line.LineWidth, (int)Line.LineTotalHeight)).Left;
                float TextYPosition = ApplyAlignment(LineBounds, TextAlignment, VerticalContentAlignment, new Size((int)Line.LineWidth, (int)Line.LineTextHeight)).Y;

                foreach (MGTextRun Run in Line.Runs)
                {
                    RectangleF RunBounds; // The bounds of the MGTextRun, in CoordinateSpace.Layout space

                    if (Run.RunType == TextRunType.Image && Run is MGTextRunImage ImageRun)
                    {
                        int ImgWidth = ImageRun.TargetWidth;
                        int ImgHeight = ImageRun.TargetHeight;
                        int YPosition = ApplyAlignment(LineBounds, HorizontalAlignment.Center, VerticalContentAlignment, new Size(ImgWidth, ImgHeight)).Top;
                        Point Position = new Vector2((int)CurrentX, YPosition).TransformBy(Transform).ToPoint();
                        Desktop.Resources.TryDrawTexture(DT, ImageRun.SourceName, Position, (int)(ImgWidth * ImageSizeScalar), (int)(ImgHeight * ImageSizeScalar), DA.Opacity);
                        RunBounds = new(CurrentX, YPosition, ImgWidth, ImgHeight);
                        CurrentX += ImgWidth;
                    }
                    else if (Run.RunType == TextRunType.Text && Run is MGTextRunText TextRun)
                    {
                        bool IsBold   = TextRun.Settings.IsBold;
                        bool IsItalic = TextRun.Settings.IsItalic;
                        ResolvedFont resolved   = GetResolvedFont(IsBold, IsItalic);
                        float drawScale = GetTheme().FontSettings.UseExactScale
                            ? resolved.ExactScale
                            : resolved.SuggestedScale;

                        float ActualOpacity = Opacity * TextRun.Settings.Opacity;
                        Color Foreground = (TextRun.Settings.Foreground ?? DefaultForeground) * ActualOpacity;

                        string ActualText = TextRun.Text;
                        if (TextProgress.HasValue && RemainingCharacters < TextRun.Text.Length)
                        {
                            ActualText = TextRun.Text.Substring(0, RemainingCharacters);
                        }

                        Vector2 TextSize = MeasureText(ActualText, IsBold, IsItalic);
                        Vector2 visualDrawPosition = new(CurrentX, TextYPosition);
                        Vector2 engineAdjustedDrawPosition = visualDrawPosition + (resolved.DrawOrigin * drawScale);

                        //  Draw background
                        if (TextRun.Settings.HasBackground)
                        {
                            IFillBrush BackgroundBrush = TextRun.Settings.Background.Brush;
                            Thickness BackgroundPadding = TextRun.Settings.Background.Padding;

                            Rectangle BackgroundDestination = new Rectangle((int)CurrentX, (int)TextYPosition, (int)TextSize.X, (int)Line.LineTextHeight)
                                .GetExpanded(BackgroundPadding);
                            BackgroundBrush.Draw(DA.SetOpacity(ActualOpacity), this, BackgroundDestination);
                        }

                        //  Draw underline
                        MGTextRunUnderlineConfig UnderlineSettings = TextRun.Settings.Underline;
                        if (TextRun.Settings.Underline.IsEnabled)
                        {
                            int UnderlineHeight = UnderlineSettings.Height;
                            int UnderlineYOffset = UnderlineSettings.VerticalOffset;
                            IFillBrush UnderlineBrush = TextRun.Settings.Underline.Brush ?? Foreground.AsFillBrush();

                            RectangleF Destination = new RectangleF(CurrentX, TextYPosition + Line.LineTextHeight - 2 + UnderlineYOffset, TextSize.X, UnderlineHeight);
                                //.CreateTransformedF(Transform); // IFillBrush.Draw will already account for ElementDrawArgs.Offset
                            UnderlineBrush.Draw(DA.SetOpacity(ActualOpacity), this, Destination.RoundUp());
                            //DT.FillRectangle(Vector2.Zero, Destination, Foreground);
                        }

                        Vector2 Position = engineAdjustedDrawPosition.TransformBy(Transform);
                        if (TextRun.Settings.IsShadowed)
                        {
                            //  Draw text twice, once for the shadow, then again for itself
                            Color ShadowColor = (TextRun.Settings.Shadow.ShadowColor ?? DefaultForeground) * ActualOpacity;
                            Vector2 ShadowOffset = TextRun.Settings.Shadow.ShadowOffset ?? new(1, 1);

                            DT.DrawTextViaEngine(resolved, ActualText, Position + ShadowOffset, ShadowColor, resolved.DrawOrigin, drawScale);
                            DT.DrawTextViaEngine(resolved, ActualText, Position,               Foreground,  resolved.DrawOrigin, drawScale);
                        }
                        else
                        {
                            DT.DrawTextViaEngine(resolved, ActualText, Position, Foreground, resolved.DrawOrigin, drawScale);
                        }

                        RunBounds = new(CurrentX, TextYPosition, TextSize.X, TextSize.Y);
                        CurrentX += TextSize.X;

                        RemainingCharacters -= ActualText.Length;
                        if (TextProgress.HasValue && RemainingCharacters <= 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        throw new NotImplementedException($"{nameof(MGTextBlock)}.{nameof(DrawSelf)} does not support rendering {nameof(MGTextRun)}s of type={nameof(TextRunType)}.{Run.RunType}");
                    }

                    //  Keep track of which parts of the textblock content have their own tooltip or delegate to invoke when clicking in the bounds
                    if (Run.HasToolTip || Run.HasAction)
                    {
                        Rectangle RoundedRunBounds = RunBounds.RoundUp();

                        if (Run.HasToolTip)
                        {
                            string ToolTipName = Run.ToolTipId;
                            if (!ToolTipBounds.TryGetValue(ToolTipName, out List<Rectangle> Bounds))
                            {
                                Bounds = new();
                                ToolTipBounds.Add(ToolTipName, Bounds);
                            }

                            Bounds.Add(RoundedRunBounds);
                        }

                        if (Run.HasAction)
                        {
                            string ActionName = Run.ActionId;
                            if (!ActionBounds.TryGetValue(ActionName, out List<Rectangle> Bounds))
                            {
                                Bounds = new();
                                ActionBounds.Add(ActionName, Bounds);
                            }

                            Bounds.Add(RoundedRunBounds);
                        }
                    }
                }

                CurrentY += Line.LineTotalHeight + EffectiveLinePadding;

                if (TextProgress.HasValue && RemainingCharacters <= 0)
                {
                    break;
                }
            }
        }
    }
}
