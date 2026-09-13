using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Shared.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thickness = MonoGame.Extended.Thickness;

namespace MGUI.Samples.Features
{
    public class IFillBrushSamples : SampleBase
    {
        #region MGHighlightFillBrush samples
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _HighlightBrushFocusCheckBox;
        public bool HighlightBrushFocusCheckBox
        {
            get => _HighlightBrushFocusCheckBox;
            set
            {
                if (_HighlightBrushFocusCheckBox != value)
                {
                    _HighlightBrushFocusCheckBox = value;
                    NotifyPropertyChanged(nameof(HighlightBrushFocusCheckBox));
                    UpdateHighlightBrushFocusedElements();
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _HighlightBrushFocusButton;
        public bool HighlightBrushFocusButton
        {
            get => _HighlightBrushFocusButton;
            set
            {
                if (_HighlightBrushFocusButton != value)
                {
                    _HighlightBrushFocusButton = value;
                    NotifyPropertyChanged(nameof(HighlightBrushFocusButton));
                    UpdateHighlightBrushFocusedElements();
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _HighlightBrushFocusRadioButtons;
        public bool HighlightBrushFocusRadioButtons
        {
            get => _HighlightBrushFocusRadioButtons;
            set
            {
                if (_HighlightBrushFocusRadioButtons != value)
                {
                    _HighlightBrushFocusRadioButtons = value;
                    NotifyPropertyChanged(nameof(HighlightBrushFocusRadioButtons));
                    UpdateHighlightBrushFocusedElements();
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IReadOnlyList<MGElement> _HighlightBrushFocusedElements;
        public IReadOnlyList<MGElement> HighlightBrushFocusedElements
        {
            get => _HighlightBrushFocusedElements;
            private set
            {
                if (_HighlightBrushFocusedElements != value)
                {
                    _HighlightBrushFocusedElements = value;
                    NotifyPropertyChanged(nameof(HighlightBrushFocusedElements));
                }
            }
        }

        private void UpdateHighlightBrushFocusedElements()
        {
            List<MGElement> Elements = new();
            if (HighlightBrushFocusCheckBox && Window.TryGetElementByName("HighlightBrushSampleCheckBox", out MGElement CheckBox))
            {
                Elements.Add(CheckBox);
            }

            if (HighlightBrushFocusButton && Window.TryGetElementByName("HighlightBrushSampleButton", out MGElement Button))
            {
                Elements.Add(Button);
            }

            if (HighlightBrushFocusRadioButtons && Window.TryGetElementByName("HighlightBrushSampleRadioButtons", out MGElement RadioButtons))
            {
                Elements.Add(RadioButtons);
            }

            HighlightBrushFocusedElements = Elements;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color _HighlightBrushFocusedColor;
        public Color HighlightBrushFocusedColor
        {
            get => _HighlightBrushFocusedColor;
            set
            {
                if (_HighlightBrushFocusedColor != value)
                {
                    _HighlightBrushFocusedColor = value;
                    NotifyPropertyChanged(nameof(HighlightBrushFocusedColor));
                    NotifyPropertyChanged(nameof(HighlightBrushActualFocusedColor));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private float _HighlightBrushFocusedColorOpacity;
        public float HighlightBrushFocusedColorOpacity
        {
            get => _HighlightBrushFocusedColorOpacity;
            set
            {
                if (_HighlightBrushFocusedColorOpacity != value)
                {
                    _HighlightBrushFocusedColorOpacity = value;
                    NotifyPropertyChanged(nameof(HighlightBrushFocusedColorOpacity));
                    NotifyPropertyChanged(nameof(HighlightBrushActualFocusedColor));
                }
            }
        }

        public Color HighlightBrushActualFocusedColor => HighlightBrushFocusedColor * HighlightBrushFocusedColorOpacity;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color _HighlightBrushUnfocusedColor;
        public Color HighlightBrushUnfocusedColor
        {
            get => _HighlightBrushUnfocusedColor;
            set
            {
                if (_HighlightBrushUnfocusedColor != value)
                {
                    _HighlightBrushUnfocusedColor = value;
                    NotifyPropertyChanged(nameof(HighlightBrushUnfocusedColor));
                    NotifyPropertyChanged(nameof(HighlightBrushActualUnfocusedColor));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private float _HighlightBrushUnfocusedColorOpacity;
        public float HighlightBrushUnfocusedColorOpacity
        {
            get => _HighlightBrushUnfocusedColorOpacity;
            set
            {
                if (_HighlightBrushUnfocusedColorOpacity != value)
                {
                    _HighlightBrushUnfocusedColorOpacity = value;
                    NotifyPropertyChanged(nameof(HighlightBrushUnfocusedColorOpacity));
                    NotifyPropertyChanged(nameof(HighlightBrushActualUnfocusedColor));
                }
            }
        }

        public Color HighlightBrushActualUnfocusedColor => HighlightBrushUnfocusedColor * HighlightBrushUnfocusedColorOpacity;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _HighlightBrushFocusedElementPadding;
        public int HighlightBrushFocusedElementPadding
        {
            get => _HighlightBrushFocusedElementPadding;
            set
            {
                if (_HighlightBrushFocusedElementPadding != value)
                {
                    _HighlightBrushFocusedElementPadding = value;
                    NotifyPropertyChanged(nameof(HighlightBrushFocusedElementPadding));
                }
            }
        }
        #endregion MGHighlightFillBrush samples

        #region MGNineSliceFillBrush samples
        private MGBorder NineSliceResult { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _NineSliceSourceName;
        public string NineSliceSourceName
        {
            get => _NineSliceSourceName;
            set
            {
                if (_NineSliceSourceName != value)
                {
                    _NineSliceSourceName = value;
                    NotifyPropertyChanged(nameof(NineSliceSourceName));
                    NotifyPropertyChanged(nameof(NineSlicePlaceholderTextMargin));
                    NotifyPropertyChanged(nameof(NineSlicePlaceholderTextColor));
                    UpdateNineSliceBrush();
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _NineSliceSourceSample1;
        public bool NineSliceSourceSample1
        {
            get => _NineSliceSourceSample1;
            set
            {
                if (_NineSliceSourceSample1 != value)
                {
                    _NineSliceSourceSample1 = value;
                    NotifyPropertyChanged(nameof(NineSliceSourceSample1));
                    NineSliceSourceName = "Samples_9SliceTexture1";
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _NineSliceSourceSample2;
        public bool NineSliceSourceSample2
        {
            get => _NineSliceSourceSample2;
            set
            {
                if (_NineSliceSourceSample2 != value)
                {
                    _NineSliceSourceSample2 = value;
                    NotifyPropertyChanged(nameof(NineSliceSourceSample2));
                    NineSliceSourceName = "Samples_9SliceTexture2";
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _NineSliceSourceSample3;
        public bool NineSliceSourceSample3
        {
            get => _NineSliceSourceSample3;
            set
            {
                if (_NineSliceSourceSample3 != value)
                {
                    _NineSliceSourceSample3 = value;
                    NotifyPropertyChanged(nameof(NineSliceSourceSample3));
                    NineSliceSourceName = "Samples_9SliceTexture3";
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _NineSliceTargetMargin;
        public int NineSliceTargetMargin
        {
            get => _NineSliceTargetMargin;
            set
            {
                if (_NineSliceTargetMargin != value)
                {
                    _NineSliceTargetMargin = value;
                    NotifyPropertyChanged(nameof(NineSliceTargetMargin));
                    NotifyPropertyChanged(nameof(NineSlicePlaceholderTextMargin));
                    UpdateNineSliceBrush();
                }
            }
        }

        public Thickness NineSlicePlaceholderTextMargin => NineSliceSourceSample1 ? new(NineSliceTargetMargin + 5) : new(NineSliceTargetMargin / 2 + 2);
        public Color NineSlicePlaceholderTextColor => NineSliceSourceSample3 ? Color.Black : Color.White;

        private static readonly IReadOnlyDictionary<string, int> NineSliceSourceMargins = new Dictionary<string, int>()
        {
            { "Samples_9SliceTexture1", 52 },
            { "Samples_9SliceTexture2", 40 },
            { "Samples_9SliceTexture3", 40 }
        };

        private void UpdateNineSliceBrush()
        {
            MGNineSliceFillBrush NineSliceBrush = new MGNineSliceFillBrush(new Thickness(NineSliceTargetMargin), Resources.Textures[NineSliceSourceName], NineSliceSourceMargins[NineSliceSourceName]);
            NineSliceResult.BackgroundBrush.SetAll(NineSliceBrush);
        }
        #endregion MGNineSliceFillBrush samples

        private static void InitializeResources(ContentManager Content, MGDesktop Desktop)
        {
            MGResources Resources = Desktop.Resources;

            //  SourceMargin=52
            Resources.AddTexture("Samples_9SliceTexture1", new MGTextureData(new MonoGameImageResource(Content.Load<Texture2D>(Path.Combine("Brush Textures", "9SliceTexture-1")))));

            //  SourceMargin=40
            Texture2D NineSliceTextureAtlas = Content.Load<Texture2D>(Path.Combine("Brush Textures", "9SliceTextures-2"));
            MonoGameImageResource nineSliceAtlas = new(NineSliceTextureAtlas);
            Resources.AddTexture("Samples_9SliceTexture2", new MGTextureData(nineSliceAtlas, new Rectangle(136, 532, 128, 128)));
            Resources.AddTexture("Samples_9SliceTexture3", new MGTextureData(nineSliceAtlas, new Rectangle(4, 400, 128, 128)));
            //Resources.AddTexture("9SliceTexture3", new MGTextureData(NineSliceTextureAtlas, new Rectangle(136, 532, 128, 128)));

            //  A sub-rectangle of the same atlas (not the whole image), reused below to demonstrate MGTextureFillBrush's Tile mode on an
            //  atlas sub-rectangle over a rounded host (see Docs/drawing-architecture.md): each tile is a textured quad clipped
            //  to the rounded silhouette instead of using a wrap sampler, which would repeat the whole atlas rather than just this icon.
            Resources.AddTexture("Samples_TextureFillBrushAtlasTile", new MGTextureData(nineSliceAtlas, new Rectangle(4, 4, 128, 128)));
        }

        public IFillBrushSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "IFillBrush.xaml", () => InitializeResources(Content, Desktop))
        {
            HighlightBrushFocusCheckBox = false;
            HighlightBrushFocusButton = false;
            HighlightBrushFocusRadioButtons = true;
            HighlightBrushFocusedColor = Color.White;
            HighlightBrushFocusedColorOpacity = 0.35f;
            HighlightBrushUnfocusedColor = Color.Black;
            HighlightBrushUnfocusedColorOpacity = 0.35f;
            HighlightBrushFocusedElementPadding = 3;

            NineSliceResult = Window.GetElementByName<MGBorder>("NineSliceResult");
            NineSliceSourceSample1 = true;
            MGResizeGrip NineSliceResizeGrip = new(Window, NineSliceResult);
            NineSliceTargetMargin = 26;

            //  The XAML <TextureFillBrush> element does not expose Tile or a SourceRect/atlas region, so this case (Tile mode on an atlas
            //  sub-rectangle, rounded host) is wired up here in code-behind instead, onto the placeholder <Border> declared in the XAML.
            MGBorder TextureFillBrushAtlasTileRoundedHost = Window.GetElementByName<MGBorder>("TextureFillBrushAtlasTileRoundedHost");
            TextureFillBrushAtlasTileRoundedHost.BackgroundBrush.SetAll(new MGTextureFillBrush(Resources.Textures["Samples_TextureFillBrushAtlasTile"], Tile: true));

            Window.GetElementByName<MGTextBox>("TB1").Text = @"<Button Background=""Red"" />";

            Window.GetElementByName<MGTextBox>("TB2").Text = @"<Button>
    <Button.Background>
        <SolidFillBrush Color=""Red"" />
    </Button.Background>
</Button>";

            Window.GetElementByName<MGTextBox>("TB3").Text = @"<Button Background=""Red|Black"" />";

            Window.GetElementByName<MGTextBox>("TB4").Text = @"<Button>
    <Button.Background>
        <DiagonalGradientFillBrush Color1=""Red"" Color2=""Black"" />
    </Button.Background>
</Button>";

            Window.GetElementByName<MGTextBox>("TB5").Text = @"<Button Background=""Brown|Brown|DarkGray|DarkGray"" />";

            Window.GetElementByName<MGTextBox>("TB6").Text = @"<Button>
    <Button.Background>
        <GradientFillBrush TopLeftColor=""Brown"" TopRightColor=""Brown""
                            BottomRightColor=""DarkGray"" BottomLeftColor=""DarkGray"" />
    </Button.Background>
</Button>";


            MGProgressBar ProgressBar1 = Window.GetElementByName<MGProgressBar>("ProgressBar1");
            ProgressBar1.CompletedBrush.NormalValue = new MGProgressBarGradientBrush(ProgressBar1, Color.Red, Color.Yellow, new Color(0, 255, 0));
            int Counter = 0;
            ProgressBar1.OnEndUpdate += (sender, e) =>
            {
                if (Counter % 3 == 0)
                {
                    ProgressBar1.Value = (ProgressBar1.Value + 0.5f + ProgressBar1.Maximum) % ProgressBar1.Maximum;
                }

                Counter++;
            };

            Window.WindowDataContext = this;
        }
    }
}
