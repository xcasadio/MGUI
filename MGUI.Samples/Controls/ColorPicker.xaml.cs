using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework.Content;
using System;
using System.ComponentModel;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using XnaVector4 = Microsoft.Xna.Framework.Vector4;

namespace MGUI.Samples.Controls
{
    public class ColorPickerSamples : SampleBase
    {
        private readonly MGColorPicker MainPicker;
        private readonly MGColorField CompactField;
        private readonly MGColorField PopupField;
        private readonly MGColorPreview MainPreview;
        private readonly MGColorPaletteView PaletteView;
        private readonly MGColorPicker LightPicker;
        private readonly MGColorPaletteView EnginePaletteView;
        private readonly MGPropertyGrid ColorInspector;
        private readonly MGTextBlock CurrentText;
        private readonly MGTextBlock PreviousText;
        private readonly MGTextBlock SourceText;
        private readonly MGTextBlock LightPresetText;
        private readonly MGTextBlock ThemeStatusLabel;
        private readonly MGColorSlider RedSlider;
        private readonly SampleColorInspectable Inspectable;
        private readonly MGTheme DarkBlueTheme;
        private readonly MGTheme DarkTheme;

        private ColorValue CurrentValue = new(51f / 255f, 102f / 255f, 204f / 255f, 1f);
        private ColorValue PreviousValue = new(204f / 255f, 102f / 255f, 51f / 255f, 1f);
        private XnaColor LastInspectorColor;
        private bool IsSynchronizing;

        public ColorPickerSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "ColorPicker.xaml")
        {
            MainPicker = Window.GetElementByName<MGColorPicker>("MainPicker");
            CompactField = Window.GetElementByName<MGColorField>("CompactField");
            PopupField = Window.GetElementByName<MGColorField>("PopupField");
            MainPreview = Window.GetElementByName<MGColorPreview>("MainPreview");
            PaletteView = Window.GetElementByName<MGColorPaletteView>("PaletteView");
            LightPicker = Window.GetElementByName<MGColorPicker>("LightPicker");
            EnginePaletteView = Window.GetElementByName<MGColorPaletteView>("EnginePaletteView");
            ColorInspector = Window.GetElementByName<MGPropertyGrid>("ColorInspector");
            CurrentText = Window.GetElementByName<MGTextBlock>("CurrentText");
            PreviousText = Window.GetElementByName<MGTextBlock>("PreviousText");
            SourceText = Window.GetElementByName<MGTextBlock>("SourceText");
            LightPresetText = Window.GetElementByName<MGTextBlock>("LightPresetText");
            ThemeStatusLabel = Window.GetElementByName<MGTextBlock>("ThemeStatusLabel");
            Inspectable = new SampleColorInspectable();
            DarkBlueTheme = new(MGTheme.BuiltInTheme.Dark_Blue, Desktop.DefaultFontFamily);
            DarkTheme = new(MGTheme.BuiltInTheme.Dark, Desktop.DefaultFontFamily);

            PaletteView.BindPicker(MainPicker);
            EnginePaletteView.SetEnginePresets();
            EnginePaletteView.BindPicker(LightPicker);
            RedSlider = CreateRedSlider(Window.GetElementByName<MGStackPanel>("SliderHost"));
            ColorInspector.SelectedObject = Inspectable;

            MainPicker.ValueChanging += (sender, e) => ApplyColor(e.PreviewValue, "Picker drag");
            MainPicker.ValueChanged += (sender, e) => ApplyColor(e.NewValue, "Picker commit");
            CompactField.ValueChanged += (sender, e) => ApplyColor(e.NewValue, "Compact field");
            PopupField.ValueChanged += (sender, e) => ApplyColor(e.NewValue, "Popup field");
            LightPicker.ValueChanging += (sender, e) => UpdateLightPresetLabel(e.PreviewValue, "Light picker drag");
            LightPicker.ValueChanged += (sender, e) => UpdateLightPresetLabel(e.NewValue, "Light picker commit");
            RedSlider.ValueChanging += (sender, e) => ApplyRed(e.NewValue, "Red slider drag");
            RedSlider.ValueChanged += (sender, e) => ApplyRed(e.NewValue, "Red slider commit");
            Window.GetElementByName<MGButton>("OpenPopupButton").OnLeftClicked += (sender, e) => PopupField.OpenPopup();
            Window.GetElementByName<MGButton>("UseDarkBlueThemeButton").OnLeftClicked += (sender, e) => ApplyTheme(DarkBlueTheme, "Dark_Blue");
            Window.GetElementByName<MGButton>("UseDarkThemeButton").OnLeftClicked += (sender, e) => ApplyTheme(DarkTheme, "Dark");
            Window.OnEndUpdate += (sender, e) => SyncFromPropertyGridColor();

            ApplyColor(CurrentValue, "Initial", rememberPrevious: false);
            UpdateLightPresetLabel(LightPicker.Value, "Initial light");
            ApplyTheme(DarkBlueTheme, "Dark_Blue");
            Window.WindowDataContext = this;
        }

        private MGColorSlider CreateRedSlider(MGStackPanel host)
        {
            host.TryAddChild(new MGTextBlock(Window, "Red channel slider"));
            MGColorSlider slider = new(Window, ColorSliderChannel.Red)
            {
                SliderWidth = 230,
                SliderHeight = 18,
                Value = CurrentValue.R,
                BaseColor = CurrentValue,
            };
            host.TryAddChild(slider);
            return slider;
        }

        private void ApplyRed(float red, string source)
        {
            ColorValue value = new(
                Math.Clamp(red, 0f, 1f),
                CurrentValue.G,
                CurrentValue.B,
                CurrentValue.A,
                CurrentValue.ColorSpace,
                CurrentValue.IsHdr);
            ApplyColor(value, source);
        }

        private void ApplyColor(ColorValue? value, string source, bool rememberPrevious = true)
        {
            if (!value.HasValue || IsSynchronizing)
            {
                return;
            }

            IsSynchronizing = true;
            ColorValue newValue = value.Value;
            if (rememberPrevious && newValue != CurrentValue)
            {
                PreviousValue = CurrentValue;
            }

            CurrentValue = newValue;
            MainPicker.Value = newValue;
            MainPicker.PreviousValue = PreviousValue;
            CompactField.Value = newValue;
            PopupField.Value = newValue;
            MainPreview.CurrentValue = newValue;
            MainPreview.PreviousValue = PreviousValue;
            RedSlider.BaseColor = newValue;
            RedSlider.Value = newValue.R;
            UpdateInspectable(newValue);
            UpdateLabels(source);
            ColorInspector.RefreshVisibleValues();
            IsSynchronizing = false;
        }

        private void UpdateInspectable(ColorValue value)
        {
            Inspectable.XnaColor = value.ToXnaColor();
            Inspectable.RgbVector3 = new XnaVector3(value.R, value.G, value.B);
            Inspectable.RgbaVector4 = new XnaVector4(value.R, value.G, value.B, value.A);
            Inspectable.SystemVector4 = value.ToSystemVector4();
            Inspectable.HexRgba = ColorFormatter.Format(value, ColorValueFormat.HexRgba);
            LastInspectorColor = Inspectable.XnaColor;
        }

        private void SyncFromPropertyGridColor()
        {
            if (IsSynchronizing || Inspectable.XnaColor == LastInspectorColor)
            {
                return;
            }

            ApplyColor(ColorValue.FromXnaColor(Inspectable.XnaColor), "PropertyGrid Color");
        }

        private void UpdateLabels(string source)
        {
            CurrentText.Text = $"Current: {ColorFormatter.Format(CurrentValue, ColorValueFormat.HexRgba)}  {ColorFormatter.Format(CurrentValue, ColorValueFormat.RgbaByte)}";
            PreviousText.Text = $"Previous: {ColorFormatter.Format(PreviousValue, ColorValueFormat.HexRgba)}";
            SourceText.Text = $"Last source: {source}";
        }

        private void UpdateLightPresetLabel(ColorValue value, string source)
        {
            LightPresetText.Text = $"Light/emissive: {ColorFormatter.Format(value, ColorValueFormat.HexRgba)}  Intensity {ColorHdrHelper.GetIntensity(value):0.##}  {source}";
        }

        private void ApplyTheme(MGTheme theme, string themeName)
        {
            Window.GetResources().DefaultTheme = theme;
            ThemeStatusLabel.Text = $"Theme: {themeName}";
        }

        private sealed class SampleColorInspectable
        {
            [Category("Color")]
            public XnaColor XnaColor { get; set; }

            [Category("Color")]
            public XnaVector3 RgbVector3 { get; set; }

            [Category("Color")]
            public XnaVector4 RgbaVector4 { get; set; }

            [Category("Color")]
            public System.Numerics.Vector4 SystemVector4 { get; set; }

            [Category("Text")]
            [ReadOnly(true)]
            public string HexRgba { get; set; } = string.Empty;
        }
    }
}
