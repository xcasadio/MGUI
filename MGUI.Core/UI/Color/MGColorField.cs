using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Input.Mouse;
using System;
using System.Diagnostics;

namespace MGUI.Core.UI
{
    public class MGColorField : MGElement
    {
        public MGColorFieldModel Model { get; }
        public MGColorPickerPopup Popup { get; }

        public ColorValue? Value
        {
            get => Model.Value;
            set
            {
                _ = Model.TrySetValue(value);
                NPC(nameof(Value));
            }
        }

        public ColorValue? DefaultValue
        {
            get => Model.DefaultValue;
            set
            {
                if (Model.DefaultValue != value)
                {
                    Model.DefaultValue = value;
                    NPC(nameof(DefaultValue));
                }
            }
        }

        public bool AllowNull
        {
            get => Model.AllowNull;
            set
            {
                if (Model.AllowNull != value)
                {
                    Model.AllowNull = value;
                    NPC(nameof(AllowNull));
                }
            }
        }

        public bool IsMixed
        {
            get => Model.IsMixed;
            set
            {
                if (Model.IsMixed != value)
                {
                    Model.IsMixed = value;
                    NPC(nameof(IsMixed));
                }
            }
        }

        public bool IsReadOnly
        {
            get => Model.IsReadOnly;
            set
            {
                if (Model.IsReadOnly != value)
                {
                    Model.IsReadOnly = value;
                    NPC(nameof(IsReadOnly));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ShowTextInput;
        public bool ShowTextInput
        {
            get => _ShowTextInput;
            set
            {
                if (_ShowTextInput != value)
                {
                    _ShowTextInput = value;
                    LayoutChanged(this, true);
                    NPC(nameof(ShowTextInput));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ShowAlpha;
        public bool ShowAlpha
        {
            get => _ShowAlpha;
            set
            {
                if (_ShowAlpha != value)
                {
                    _ShowAlpha = value;
                    NPC(nameof(ShowAlpha));
                }
            }
        }

        public bool ShowEyeDropper { get; set; }
        public bool IsHdr { get; set; }
        public ColorValueFormat DisplayFormat { get; set; } = ColorValueFormat.HexRgba;
        public ColorEditCommitMode CommitMode { get; set; } = ColorEditCommitMode.ExplicitOkCancel;
        public int FieldWidth { get; set; } = 168;
        public int FieldHeight { get; set; } = 24;
        public int SwatchSize { get; set; } = 18;
        public int ResetButtonWidth { get; set; } = 18;
        public int InnerPadding { get; set; } = 3;
        public int Spacing { get; set; } = 4;
        public int CheckerboardCellSize { get; set; } = 4;
        public Color BorderColor { get; set; } = Color.Black;
        public Color BackgroundColor { get; set; } = Color.White;
        public Color DisabledOverlayColor { get; set; } = new(160, 160, 160, 90);
        public Color NullFillColor { get; set; } = new(245, 245, 245);
        public Color MixedFillColor { get; set; } = new(150, 150, 150);
        public Color CheckerboardLightColor { get; set; } = new(210, 210, 210);
        public Color CheckerboardDarkColor { get; set; } = new(130, 130, 130);

        public event EventHandler<ColorFieldValueChangedEventArgs> ValueChanged;
        public event EventHandler PopupOpened;
        public event EventHandler PopupClosed;

        public MGColorField(MGWindow window)
            : this(window, new ColorValue(1f, 1f, 1f, 1f), new ColorPickerOptions())
        {
        }

        public MGColorField(MGWindow window, ColorValue? value, ColorPickerOptions options)
            : base(window, MGElementType.ColorField)
        {
            options ??= new ColorPickerOptions();
            Model = new MGColorFieldModel(value)
            {
                AllowNull = options.AllowNull,
            };

            Popup = new MGColorPickerPopup(window, options);
            using (BeginInitializing())
            {
                ShowTextInput = options.ShowTextInput;
                ShowAlpha = options.ShowAlpha;
                ShowEyeDropper = options.ShowEyeDropper;
                IsHdr = options.IsHdr;
                DisplayFormat = options.DisplayFormat;
                CommitMode = options.CommitMode;
                IsFocusable = true;
                HorizontalAlignment = HorizontalAlignment.Left;
                VerticalAlignment = VerticalAlignment.Center;

                Model.ValueChanged += (sender, e) =>
                {
                    NPC(nameof(Value));
                    ValueChanged?.Invoke(this, e);
                };
                Popup.EditCommitted += (sender, e) => _ = Model.TrySetValue(e.NewValue);
                Popup.PopupOpened += (sender, e) => PopupOpened?.Invoke(this, e);
                Popup.PopupClosed += (sender, e) => PopupClosed?.Invoke(this, e);
                MouseHandler.LMBReleasedInside += OnReleasedInside;
            }
        }

        public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
        {
            SharedSize = new(0);
            return new(FieldWidth, FieldHeight, 0, 0);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, new Size(FieldWidth, FieldHeight));
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, BackgroundColor * DA.Opacity);
            DrawSwatch(DA, GetSwatchBounds(bounds));
            if (ShowTextInput)
            {
                DrawTextStrip(DA, GetTextBounds(bounds));
            }

            if (DefaultValue.HasValue)
            {
                DrawResetButton(DA, GetResetBounds(bounds));
            }

            DrawRectangleBorder(DA, bounds, BorderColor);
            if (IsReadOnly || !DerivedIsEnabled)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, DisabledOverlayColor * DA.Opacity);
            }
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            if (Popup.IsOpen)
            {
                return Popup.TryHandleNavigationAction(action);
            }

            return action switch
            {
                UINavigationAction.Submit when CanOpenPopup => OpenPopup(),
                UINavigationAction.Cancel when Popup.IsOpen => Popup.CancelAndClose(),
                _ => base.TryHandleNavigationAction(action),
            };
        }

        public string GetDisplayText()
            => Model.GetDisplayText(DisplayFormat);

        public bool ResetToDefault()
            => Model.ResetToDefault();

        public bool OpenPopup()
        {
            if (!CanOpenPopup)
            {
                return false;
            }

            ColorValue startValue = Value ?? DefaultValue ?? new ColorValue(0f, 0f, 0f, ShowAlpha ? 0f : 1f);
            Popup.Picker.ShowAlpha = ShowAlpha;
            Popup.Picker.ShowTextInput = ShowTextInput;
            Popup.Picker.DisplayFormat = DisplayFormat;
            Popup.Picker.CommitMode = ColorEditCommitMode.ExplicitOkCancel;
            Popup.OpenRelativeTo(this, startValue);
            return true;
        }

        private bool CanOpenPopup => !IsReadOnly && DerivedIsEnabled && !Popup.IsOpen;

        private void OnReleasedInside(object sender, BaseMouseReleasedEventArgs e)
        {
            e.SetHandledBy(this, false);
            Point layoutPoint = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
            Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, new Size(FieldWidth, FieldHeight));
            if (DefaultValue.HasValue && GetResetBounds(bounds).Contains(layoutPoint))
            {
                _ = ResetToDefault();
                return;
            }

            _ = OpenPopup();
        }

        private Rectangle GetSwatchBounds(Rectangle bounds)
        {
            int padding = Math.Max(0, InnerPadding);
            int size = Math.Max(0, Math.Min(SwatchSize, bounds.Height - padding * 2));
            return new Rectangle(bounds.X + padding, bounds.Y + (bounds.Height - size) / 2, size, size);
        }

        private Rectangle GetResetBounds(Rectangle bounds)
        {
            int padding = Math.Max(0, InnerPadding);
            return new(bounds.Right - padding - ResetButtonWidth, bounds.Y + padding, ResetButtonWidth, Math.Max(0, bounds.Height - padding * 2));
        }

        private Rectangle GetTextBounds(Rectangle bounds)
        {
            Rectangle swatch = GetSwatchBounds(bounds);
            int padding = Math.Max(0, InnerPadding);
            int left = swatch.Right + Spacing;
            int right = DefaultValue.HasValue ? GetResetBounds(bounds).Left - Spacing : bounds.Right - padding;
            return new Rectangle(left, bounds.Y + padding, Math.Max(0, right - left), Math.Max(0, bounds.Height - padding * 2));
        }

        private void DrawSwatch(ElementDrawArgs DA, Rectangle bounds)
        {
            if (Model.IsMixed)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, MixedFillColor * DA.Opacity);
            }
            else if (!Value.HasValue)
            {
                DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, NullFillColor * DA.Opacity);
            }
            else
            {
                DrawCheckerboard(DA, bounds);
                DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, Value.Value.ToXnaColor() * DA.Opacity);
            }

            DrawRectangleBorder(DA, bounds, BorderColor);
        }

        private void DrawTextStrip(ElementDrawArgs DA, Rectangle bounds)
        {
            Color fill = Popup.Picker.TextInput.HasValidationError ? new Color(255, 225, 225) : new Color(248, 248, 248);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, fill * DA.Opacity);
            DrawRectangleBorder(DA, bounds, Popup.Picker.TextInput.HasValidationError ? Color.Red : new Color(180, 180, 180));
        }

        private void DrawResetButton(ElementDrawArgs DA, Rectangle bounds)
        {
            DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, new Color(235, 235, 235) * DA.Opacity);
            int thickness = 2;
            Rectangle horizontal = new(bounds.X + 4, bounds.Center.Y - thickness / 2, Math.Max(0, bounds.Width - 8), thickness);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), horizontal, BorderColor * DA.Opacity);
            DrawRectangleBorder(DA, bounds, new Color(180, 180, 180));
        }

        private void DrawCheckerboard(ElementDrawArgs DA, Rectangle bounds)
        {
            int cellSize = Math.Max(1, CheckerboardCellSize);
            for (int y = bounds.Top; y < bounds.Bottom; y += cellSize)
            {
                int height = Math.Min(cellSize, bounds.Bottom - y);
                for (int x = bounds.Left; x < bounds.Right; x += cellSize)
                {
                    int width = Math.Min(cellSize, bounds.Right - x);
                    bool light = ((x - bounds.Left) / cellSize + (y - bounds.Top) / cellSize) % 2 == 0;
                    DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, y, width, height), (light ? CheckerboardLightColor : CheckerboardDarkColor) * DA.Opacity);
                }
            }
        }

        private void DrawRectangleBorder(ElementDrawArgs DA, Rectangle bounds, Color color)
        {
            Color actual = color * DA.Opacity;
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), actual);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), actual);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), actual);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), actual);
        }
    }
}