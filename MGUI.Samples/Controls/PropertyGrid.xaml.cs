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

        private MGPropertyGrid Inspector { get; }
        private MGTextBlock ActiveObjectLabel { get; }

        public PropertyGridSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "PropertyGrid.xaml")
        {
            Player = new SampleInspectableEntity
            {
                Name = "Player",
                PositionX = 12,
                PositionY = 42,
                Rotation = 0.5,
                Visible = true,
                Opacity = 0.85f,
            };

            Light = new SampleInspectableEntity
            {
                Name = "SpotLight",
                PositionX = 240,
                PositionY = 96,
                Rotation = 25.0,
                Visible = false,
                Opacity = 0.45f,
            };

            Inspector = Window.GetElementByName<MGPropertyGrid>("Inspector");
            ActiveObjectLabel = Window.GetElementByName<MGTextBlock>("ActiveObjectLabel");

            Window.GetElementByName<MGButton>("SwapObjectButton").OnLeftClicked += (sender, e) => SwapObject();
            Window.GetElementByName<MGButton>("MutateObjectButton").OnLeftClicked += (sender, e) => MutateActiveObject();
            Window.OnEndUpdate += (sender, e) => Inspector?.RefreshVisibleValues();

            SetActiveObject(Player);
        }

        private void SwapObject()
            => SetActiveObject(ReferenceEquals(ActiveEntity, Player) ? Light : Player);

        private void SetActiveObject(SampleInspectableEntity entity)
        {
            ActiveEntity = entity;
            Inspector.SelectedObject = entity;
            ActiveObjectLabel.Text = $"Selected: {entity.Name}";
        }

        private void MutateActiveObject()
        {
            if (ActiveEntity == null)
            {
                return;
            }

            ActiveEntity.PositionX += 5;
            ActiveEntity.PositionY += 3;
            ActiveEntity.Rotation = Math.Round(ActiveEntity.Rotation + 7.5, 2);
            ActiveEntity.Visible = !ActiveEntity.Visible;
            ActiveEntity.Opacity = ActiveEntity.Opacity >= 1.0f ? 0.25f : MathF.Min(1.0f, ActiveEntity.Opacity + 0.1f);
            ActiveEntity.Name = ActiveEntity.Name.EndsWith("*", StringComparison.Ordinal) ? ActiveEntity.Name.TrimEnd('*') : ActiveEntity.Name + "*";
            ActiveObjectLabel.Text = $"Selected: {ActiveEntity.Name}";
        }

        private sealed class SampleInspectableEntity
        {
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
        }
    }
}