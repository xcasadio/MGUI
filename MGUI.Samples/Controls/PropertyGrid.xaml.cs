using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;
using System;
using System.ComponentModel;

namespace MGUI.Samples.Controls
{
    public class PropertyGridSamples : SampleBase
    {
        private readonly SampleInspectableEntity Player;
        private readonly SampleInspectableEntity Light;
        private SampleInspectableEntity ActiveEntity;
        private MGTheme DarkBlueTheme { get; }
        private MGTheme DarkTheme { get; }

        private MGPropertyGrid Inspector { get; }
        private MGTextBlock ActiveObjectLabel { get; }
        private MGTextBlock ThemeStatusLabel { get; }

        public PropertyGridSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "PropertyGrid.xaml")
        {
            Player = new("Player", 12, 42, 0.5, true, 0.85f, 0.0);
            Light = new("SpotLight", 240, 96, 25.0, false, 0.45f, 1.7);
            DarkBlueTheme = new(MGTheme.BuiltInTheme.Dark_Blue, Desktop.DefaultFontFamily);
            DarkTheme = new(MGTheme.BuiltInTheme.Dark, Desktop.DefaultFontFamily);

            Inspector = Window.GetElementByName<MGPropertyGrid>("Inspector");
            ActiveObjectLabel = Window.GetElementByName<MGTextBlock>("ActiveObjectLabel");
            ThemeStatusLabel = Window.GetElementByName<MGTextBlock>("ThemeStatusLabel");

            Window.GetElementByName<MGButton>("SwapObjectButton").OnLeftClicked += (sender, e) => SwapObject();
            Window.GetElementByName<MGButton>("MutateObjectButton").OnLeftClicked += (sender, e) => MutateActiveObject();
            Window.GetElementByName<MGButton>("UseDarkBlueThemeButton").OnLeftClicked += (sender, e) => ApplyTheme(DarkBlueTheme, "Dark_Blue");
            Window.GetElementByName<MGButton>("UseDarkThemeButton").OnLeftClicked += (sender, e) => ApplyTheme(DarkTheme, "Dark");
            Window.OnEndUpdate += (sender, e) => HandleEndUpdate(e);

            UpdateLiveValues(0);
            SetActiveObject(Player);
            ApplyTheme(DarkBlueTheme, "Dark_Blue");
        }

        private void SwapObject()
            => SetActiveObject(ReferenceEquals(ActiveEntity, Player) ? Light : Player);

        private void SetActiveObject(SampleInspectableEntity entity)
        {
            ActiveEntity = entity;
            Inspector.SelectedObject = entity;
            UpdateActiveObjectLabel();
        }

        private void MutateActiveObject()
        {
            if (ActiveEntity == null)
            {
                return;
            }

            ActiveEntity.NudgeBaseline();
            UpdateActiveObjectLabel();
        }

        private void HandleEndUpdate(MGElement.ElementUpdateEventArgs e)
        {
            UpdateLiveValues(e.UA.BA.TotalElapsed.TotalSeconds);
            Inspector?.RefreshVisibleValues();
        }

        private void UpdateLiveValues(double totalSeconds)
        {
            Player.UpdateFromTime(totalSeconds);
            Light.UpdateFromTime(totalSeconds);
            UpdateActiveObjectLabel();
        }

        private void UpdateActiveObjectLabel()
        {
            if (ActiveEntity != null)
            {
                ActiveObjectLabel.Text = $"Selected: {ActiveEntity.Name}";
            }
        }

        private void ApplyTheme(MGTheme theme, string themeName)
        {
            if (theme == null)
            {
                return;
            }

            Window.GetResources().DefaultTheme = theme;
            ThemeStatusLabel.Text = $"Theme: {themeName}";
        }

        private sealed class SampleInspectableEntity
        {
            private readonly string BaseName;
            private readonly double PhaseOffset;
            private int BasePositionX;
            private int BasePositionY;
            private double BaseRotation;
            private float BaseOpacity;
            private int VariantIndex;

            public SampleInspectableEntity(string name, int positionX, int positionY, double rotation, bool visible, float opacity, double phaseOffset)
            {
                BaseName = name;
                PhaseOffset = phaseOffset;
                BasePositionX = positionX;
                BasePositionY = positionY;
                BaseRotation = rotation;
                BaseOpacity = opacity;

                PositionX = positionX;
                PositionY = positionY;
                Rotation = rotation;
                Visible = visible;
                Opacity = opacity;
                Name = name;

                ManualOverride = visible;
                Priority = Math.Max(1, positionX / 12);
                ExposureBias = MathF.Round(opacity * 0.5f * 100f) / 100f;
                CalibrationOffset = Math.Round(phaseOffset, 2);
                Notes = $"{name} manual field";
            }

            [Category("Transform")]
            public int PositionX { get; set; }

            [Category("Transform")]
            public int PositionY { get; set; }

            [Category("Transform")]
            public double Rotation { get; set; }

            [Category("Rendering")]
            public bool Visible { get; set; }

            [Category("Rendering")]
            public float Opacity { get; set; }

            [Category("Identity")]
            public string Name { get; set; } = string.Empty;

            [Category("Manual")]
            public bool ManualOverride { get; set; }

            [Category("Manual")]
            public int Priority { get; set; }

            [Category("Manual")]
            public float ExposureBias { get; set; }

            [Category("Manual")]
            public double CalibrationOffset { get; set; }

            [Category("Manual")]
            public string Notes { get; set; } = string.Empty;

            public void UpdateFromTime(double totalSeconds)
            {
                double phase = totalSeconds + PhaseOffset;
                PositionX = BasePositionX + (int)Math.Round(Math.Sin(phase * 1.3d) * 24d);
                PositionY = BasePositionY + (int)Math.Round(Math.Cos(phase * 0.95d) * 18d);
                Rotation = Math.Round(BaseRotation + Math.Sin(phase * 0.8d) * 45d, 2);
                Visible = Math.Cos(phase * 1.15d) >= -0.15d;

                float animatedOpacity = BaseOpacity + (float)(Math.Sin(phase * 1.6d) * 0.25d);
                animatedOpacity = Math.Clamp(animatedOpacity, 0.15f, 1.0f);
                Opacity = MathF.Round(animatedOpacity * 100f) / 100f;

                int phaseIndex = ((int)Math.Floor(totalSeconds * 2d) + VariantIndex) % 3;
                Name = phaseIndex switch
                {
                    0 => $"{BaseName} [Idle]",
                    1 => $"{BaseName} [Live]",
                    _ => $"{BaseName} [Sync]",
                };
            }

            public void NudgeBaseline()
            {
                BasePositionX += 5;
                BasePositionY += 3;
                BaseRotation = Math.Round(BaseRotation + 7.5d, 2);
                BaseOpacity = BaseOpacity >= 1.0f ? 0.25f : MathF.Min(1.0f, BaseOpacity + 0.1f);
                VariantIndex = (VariantIndex + 1) % 3;

                ManualOverride = !ManualOverride;
                Priority += 1;
                ExposureBias = MathF.Round(Math.Clamp(ExposureBias + 0.05f, -1.0f, 1.0f) * 100f) / 100f;
                CalibrationOffset = Math.Round(CalibrationOffset + 0.125d, 3);
                Notes = $"{BaseName} manual update #{Priority}";
            }
        }
    }
}