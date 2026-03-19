using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Shared.Input.Mouse;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using System.Diagnostics;

namespace MGUI.Core.UI
{
    public class MGToggleButton : MGSingleContentHost
    {
        internal static bool GetNextCheckedState(bool isChecked) => !isChecked;

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

        /// <summary>The background brush to use for this <see cref="MGToggleButton"/> when <see cref="IsChecked"/> is true.<para/>
        /// Equivalent to <see cref="MGElement.BackgroundBrush"/>'s <see cref="VisualStateSetting{TDataType}.SelectedValue"/></summary>
        public IFillBrush CheckedBackgroundBrush
        {
            get => BackgroundBrush.SelectedValue;
            set
            {
                if (BackgroundBrush.SelectedValue != value)
                {
                    BackgroundBrush.SelectedValue = value;
                    NPC(nameof(CheckedBackgroundBrush));
                }
            }
        }

        /// <summary>A foreground color to use on child content of this <see cref="MGToggleButton"/> when <see cref="IsChecked"/> is true.<para/>
        /// Equivalent to <see cref="MGElement.DefaultTextForeground"/>'s <see cref="VisualStateSetting{TDataType}.SelectedValue"/></summary>
        public Color? CheckedTextForeground
        {
            get => DefaultTextForeground.SelectedValue;
            set
            {
                if (DefaultTextForeground.SelectedValue != value)
                {
                    DefaultTextForeground.SelectedValue = value;
                    NPC(nameof(CheckedTextForeground));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsChecked;
        public bool IsChecked
        {
            get => _IsChecked;
            set
            {
                if (_IsChecked != value)
                {
                    bool Previous = IsChecked;
                    _IsChecked = value;
                    NPC(nameof(IsChecked));
                    OnCheckStateChanged?.Invoke(this, new(Previous, IsChecked));

                    IsSelected = IsChecked;
                    if (IsChecked)
                    {
                        OnChecked?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        OnUnchecked?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        /// <summary>Note: This event is invoked before <see cref="OnChecked"/> / <see cref="OnUnchecked"/></summary>
        public event EventHandler<EventArgs<bool>> OnCheckStateChanged;
        public event EventHandler<EventArgs> OnChecked;
        public event EventHandler<EventArgs> OnUnchecked;

        public MGToggleButton(MGWindow Window, bool IsChecked = false)
            : this(Window, new(1), MGUniformBorderBrush.Black, IsChecked) { }

        public MGToggleButton(MGWindow Window, Thickness BorderThickness, IBorderBrush BorderBrush, bool IsChecked)
            : base(Window, MGElementType.ToggleButton)
        {
            using (BeginInitializing())
            {
                IsFocusable = true;
                MinWidth = 16;
                MinHeight = 16;

                BorderElement = new(Window, BorderThickness, BorderBrush);
                BorderComponent = MGComponentBase.Create(BorderElement);
                AddComponent(BorderComponent);
                BorderElement.OnBorderBrushChanged += (sender, e) => { NPC(nameof(BorderBrush)); };
                BorderElement.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(BorderThickness)); };
                BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };

                HorizontalContentAlignment = HorizontalAlignment.Center;
                VerticalContentAlignment = VerticalAlignment.Center;
                Padding = new(4, 2, 4, 2);
                CheckedTextForeground = GetTheme().TextBlockFallbackForeground.GetValue(true).NormalValue;

                MouseHandler.LMBPressedInside += (sender, e) =>
                {
                    PressedArgs = e;
                };
                MouseHandler.LMBReleasedInside += (sender, e) =>
                {
                    if (PressedArgs != null)
                    {
                        this.IsChecked = GetNextCheckedState(this.IsChecked);
                        e.SetHandledBy(this, false);
                        PressedArgs = null;
                    }
                };
                MouseHandler.ReleasedOutside += (sender, e) =>
                {
                    if (PressedArgs != null)
                    {
                        PressedArgs = null;
                    }
                };

                this.IsChecked = IsChecked;
            }
        }

        private BaseMousePressedEventArgs PressedArgs { get; set; }

        protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
        {
            base.OnThemeChanged(PreviousTheme, CurrentTheme);

            if (CurrentTheme != null)
            {
                BackgroundBrush = CurrentTheme.GetBackgroundBrush(MGElementType.ToggleButton);
                CheckedTextForeground = CurrentTheme.TextBlockFallbackForeground.GetValue(true).NormalValue;
            }
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            if (action != UINavigationAction.Submit)
            {
                return false;
            }

            IsChecked = GetNextCheckedState(IsChecked);
            return true;
        }
    }
}