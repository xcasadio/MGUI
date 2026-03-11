using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Core.UI
{
    /// <summary>Visual effect types that can be applied to an <see cref="MGTimer"/> when a duration threshold is reached.</summary>
    public enum MGTimerEffect
    {
        /// <summary>Tints the timer's background with <see cref="MGTimerThresholdEffect.EffectColor"/>.</summary>
        Highlight,
        /// <summary>Toggles the timer's visibility on and off at <see cref="MGTimerThresholdEffect.FlickerRate"/> Hz.</summary>
        Flicker,
        /// <summary>Shakes the timer horizontally.</summary>
        Shake
    }

    /// <summary>Defines a visual effect that activates on an <see cref="MGTimer"/> when <see cref="MGTimer.RemainingDuration"/> reaches a threshold.</summary>
    public class MGTimerThresholdEffect
    {
        /// <summary>The effect activates when <see cref="MGTimer.RemainingDuration"/> is at or below this value.</summary>
        public TimeSpan Threshold { get; set; }
        /// <summary>The type of visual effect to apply.</summary>
        public MGTimerEffect Effect { get; set; }
        /// <summary>Color used by <see cref="MGTimerEffect.Highlight"/>. For best results, include transparency via the alpha channel.<para/>
        /// Default value: semi-transparent red</summary>
        public Color EffectColor { get; set; } = new Color(255, 0, 0, 100);
        /// <summary>Only relevant if <see cref="Effect"/> is <see cref="MGTimerEffect.Flicker"/>.<br/>
        /// Number of on/off cycles per second.<para/>
        /// Default value: 2</summary>
        public float FlickerRate { get; set; } = 2.0f;

        public MGTimerThresholdEffect(TimeSpan threshold, MGTimerEffect effect)
        {
            Threshold = threshold;
            Effect = effect;
        }

        public MGTimerThresholdEffect(TimeSpan threshold, MGTimerEffect effect, Color effectColor)
        {
            Threshold = threshold;
            Effect = effect;
            EffectColor = effectColor;
        }
    }

    public class MGTimer : MGElement
    {
        #region Border
        /// <summary>Provides direct access to this element's border.</summary>
        public MGComponent<MGBorder> BorderComponent { get; }
        private MGBorder BorderElement { get; }
        public override MGBorder GetBorder() => BorderElement;

        public IBorderBrush BorderBrush
        {
            get => BorderElement.BorderBrush;
            set => BorderElement.BorderBrush = value;
        }

        public Thickness BorderThickness
        {
            get => BorderElement.BorderThickness;
            set => BorderElement.BorderThickness = value;
        }

        public MGCornerRadius CornerRadius
        {
            get => BorderElement.CornerRadius;
            set => BorderElement.CornerRadius = value;
        }
        #endregion Border

        /// <summary>Provides direct access to the textblock component that displays this timer's <see cref="RemainingDuration"/> time.</summary>
        public MGComponent<MGTextBlock> ValueComponent { get; }
        private MGTextBlock ValueElement { get; }

        public bool TrySetFont(string FontFamily, int FontSize) => ValueElement.TrySetFont(FontFamily, FontSize);

        private void UpdateDisplayedValue(bool ForceLayoutRefresh)
        {
            if (ValueElement == null || ValueDisplayFormat == null || RemainingDurationToString == null)
            {
                return;
            }

            string RemainingDurationDisplayString = RemainingDurationToString(RemainingDuration);
            string ValueDisplayString = ValueDisplayFormat.Replace($"{{{{{nameof(RemainingDuration)}}}}}", RemainingDurationDisplayString);

            //  Assume the required size of this element hasn't changed if the text length stayed the same
            //  This assumption may be incorrect for non-monospaced font
            ValueElement.SetText(ValueDisplayString, !ForceLayoutRefresh && (ValueElement.Text?.Length ?? 0) == ValueDisplayString.Length);
        }

        public const string DefaultValueDisplayFormat = $"[b][shadow=Black 1 1]{{{{{nameof(RemainingDuration)}}}}}[/shadow][/b]";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _ValueDisplayFormat;
        /// <summary>A format string to use when computing the text to display.<br/>
        /// "{{RemainingDuration}}" will be replaced with the value retrieved via <see cref="RemainingDurationToString"/>.<para/>
        /// Default value: <see cref="DefaultValueDisplayFormat"/><para/>
        /// This value supports some basic text markdown, such as "[b]" for bold text, "[fg=Red]" to set the text foreground color to a given value, "[opacity=0.5]" etc.</summary>
        public string ValueDisplayFormat
        {
            get => _ValueDisplayFormat;
            set
            {
                if (_ValueDisplayFormat != value)
                {
                    _ValueDisplayFormat = value;
                    UpdateDisplayedValue(true);
                    NPC(nameof(ValueDisplayFormat));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimeSpan _RemainingDuration;
        public TimeSpan RemainingDuration
        {
            get => _RemainingDuration;
            set
            {
                TimeSpan ActualValue = AllowsNegativeDuration || RemainingDuration.TotalSeconds >= 0 ? value : TimeSpan.Zero;
                if (_RemainingDuration != ActualValue)
                {
                    TimeSpan Previous = RemainingDuration;
                    _RemainingDuration = ActualValue;
                    UpdateDisplayedValue(false);
                    NPC(nameof(RemainingDuration));
                    RemainingDurationChanged?.Invoke(this, new(Previous, RemainingDuration));

                    if (RemainingDuration.TotalSeconds <= 0)
                    {
                        TimeUp?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>Invoked when <see cref="RemainingDuration"/> changes.</summary>
        public event EventHandler<EventArgs<TimeSpan>> RemainingDurationChanged;

        /// <summary>Invoked when <see cref="RemainingDuration"/> hits zero (or negative if <see cref="AllowsNegativeDuration"/> is true).</summary>
        public event EventHandler<EventArgs> TimeUp;

        public bool AllowsNegativeDuration { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double _TimeScale = 1.0;
        /// <summary>Determines the rate at which <see cref="RemainingDuration"/> changes.<para/>
        /// Default value: 1.0, which means <see cref="RemainingDuration"/> is decremented by 1 second per 1 real-life second.<para/>
        /// For example, if the player has an ability that slows time to 0.50x, you may wish to temporarily set <see cref="TimeScale"/> to 0.50 while the ability is active,<br/>
        /// unless you still wanted the timer to track real time</summary>
        public double TimeScale
        {
            get => _TimeScale;
            set
            {
                if (_TimeScale != value)
                {
                    _TimeScale = value;
                    NPC(nameof(TimeScale));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Func<TimeSpan, string> _RemainingDurationToString;
        /// <summary>A function whose input is the <see cref="RemainingDuration"/> time, and returns the string value that should be displayed by this <see cref="MGTimer"/>.<para/>
        /// Default value: A function that returns:
        /// <code>TimeSpan.ToString(@"m\:ss\.%f")</code>
        /// For example, a value of 13566 seconds would be formatted as: "46:06.0"<para/>
        /// More info: <see href="https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings"/></summary>
        public Func<TimeSpan, string> RemainingDurationToString
        {
            get => _RemainingDurationToString;
            set
            {
                if (_RemainingDurationToString != value)
                {
                    _RemainingDurationToString = value;
                    UpdateDisplayedValue(true);
                    NPC(nameof(RemainingDurationToString));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsPaused;
        /// <summary>See also: <see cref="Pause"/>, <see cref="Resume"/>, <see cref="Paused"/>, <see cref="Resumed"/></summary>
        public bool IsPaused
        {
            get => _IsPaused;
            set
            {
                if (_IsPaused != value)
                {
                    _IsPaused = value;
                    NPC(nameof(IsPaused));
                    if (IsPaused)
                    {
                        Paused?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        Resumed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>Invoked when this <see cref="MGTimer"/> is paused (<see cref="IsPaused"/> set to true)</summary>
        public event EventHandler<EventArgs> Paused;
        /// <summary>Invoked when this <see cref="MGTimer"/> is resumed (<see cref="IsPaused"/> set to false)</summary>
        public event EventHandler<EventArgs> Resumed;

        /// <summary>Pause the <see cref="MGTimer"/> if not already paused.<br/>
        /// See also: <see cref="Resume"/>, <see cref="IsPaused"/></summary>
        public void Pause() => IsPaused = true;
        /// <summary>Resume the <see cref="MGTimer"/> if paused.<br/>
        /// See also: <see cref="Pause"/>, <see cref="IsPaused"/></summary>
        public void Resume() => IsPaused = false;

        #region Threshold Effects
        /// <summary>A list of visual effects that activate when <see cref="RemainingDuration"/> drops to or below a defined threshold.<para/>
        /// When multiple thresholds are exceeded simultaneously, the effect with the smallest (most severe) threshold takes priority.<para/>
        /// See also: <see cref="MGTimerThresholdEffect"/>, <see cref="MGTimerEffect"/></summary>
        public List<MGTimerThresholdEffect> ThresholdEffects { get; } = new();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGTimerThresholdEffect _PreviousActiveEffect = null;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double _EffectAccumulator = 0.0;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _ShakeOffsetX = 0;

        private void ApplyThresholdEffects(double ElapsedSeconds)
        {
            MGTimerThresholdEffect Active = ThresholdEffects
                .Where(e => RemainingDuration <= e.Threshold)
                .OrderBy(e => e.Threshold)
                .FirstOrDefault();

            if (Active != _PreviousActiveEffect)
            {
                if (_PreviousActiveEffect != null)
                {
                    ResetEffects();
                }

                _EffectAccumulator = 0.0;
                _PreviousActiveEffect = Active;
            }

            if (Active == null)
            {
                return;
            }

            _EffectAccumulator += ElapsedSeconds;

            switch (Active.Effect)
            {
                case MGTimerEffect.Flicker:
                    Opacity = (int)(_EffectAccumulator * Active.FlickerRate * 2) % 2 == 0 ? 1.0f : 0.0f;
                    break;
                case MGTimerEffect.Highlight:
                    OverlayBrush = new MGSolidFillBrush(Active.EffectColor);
                    break;
                case MGTimerEffect.Shake:
                    _ShakeOffsetX = (int)(Math.Sin(_EffectAccumulator * 30.0) * 3.0);
                    break;
            }
        }

        private void ResetEffects()
        {
            Opacity = 1.0f;
            OverlayBrush = null;
            _ShakeOffsetX = 0;
        }
        #endregion Threshold Effects

        /// <param name="IsPaused">If true, <see cref="RemainingDuration"/> will not decrement until <see cref="Resume"/> is called.</param>
        /// <param name="AllowsNegativeDuration">If true, <see cref="RemainingDuration"/> will continue decreasing even after hitting 0.</param>
        public MGTimer(MGWindow Window, TimeSpan Duration, bool IsPaused = true, bool AllowsNegativeDuration = false)
            : base(Window, MGElementType.Timer)
        {
            using (BeginInitializing())
            {
                this.AllowsNegativeDuration = AllowsNegativeDuration;
                this.IsPaused = IsPaused;
                RemainingDuration = Duration;

                BorderElement = new(Window);
                BorderComponent = MGComponentBase.Create(BorderElement);
                AddComponent(BorderComponent);
                BorderElement.OnBorderBrushChanged += (sender, e) => { NPC(nameof(BorderBrush)); };
                BorderElement.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(BorderThickness)); };
                BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };

                ValueElement = new(Window, "", Color.White, GetTheme().FontSettings.MediumFontSize);
                ValueComponent = new(ValueElement, false, false, true, true, false, false, true,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds.GetCompressed(Padding), HorizontalContentAlignment, VerticalContentAlignment, ComponentSize.Size));
                AddComponent(ValueComponent);

                HorizontalContentAlignment = HorizontalAlignment.Center;
                VerticalContentAlignment = VerticalAlignment.Center;
                Padding = new(4, 2, 4, 2);

                RemainingDurationToString = (TimeSpan Elapsed) => Elapsed.ToString(@"m\:ss\.%f");
                ValueDisplayFormat = DefaultValueDisplayFormat;
            }
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            base.UpdateSelf(UA);
            if (!IsPaused)
            {
                RemainingDuration -= UA.BA.FrameElapsed * TimeScale;
            }
            ApplyThresholdEffects(UA.BA.FrameElapsed.TotalSeconds);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            if (_ShakeOffsetX != 0)
            {
                LayoutBounds = new Rectangle(LayoutBounds.X + _ShakeOffsetX, LayoutBounds.Y, LayoutBounds.Width, LayoutBounds.Height);
            }

            base.DrawSelf(DA, LayoutBounds);
        }
    }
}