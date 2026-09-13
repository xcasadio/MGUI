using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace MGUI.Core.UI;

public class MGRotatedTextLabel : MGElement
{
    private string _text;
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Text));
            }
        }
    }

    private int _fontSize;
    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (_fontSize != value)
            {
                _fontSize = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(FontSize));
            }
        }
    }

    private Color _textColor;
    public Color TextColor
    {
        get => _textColor;
        set
        {
            if (_textColor != value)
            {
                _textColor = value;
                NotifyPropertyChanged(nameof(TextColor));
            }
        }
    }

    private float _rotationRadians;
    public float RotationRadians
    {
        get => _rotationRadians;
        set
        {
            if (_rotationRadians != value)
            {
                _rotationRadians = value;
                NotifyPropertyChanged(nameof(RotationRadians));
            }
        }
    }

    public MGRotatedTextLabel(MGWindow window, string text) : base(window, MGElementType.Misc)
    {
        using (BeginInitializing())
        {
            Text = text;
            FontSize = 11;
            TextColor = Color.White;
            RotationRadians = -(float)(Math.PI / 2.0);
            IsHitTestVisible = false;
        }
    }

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);
        return new Thickness(availableSize.Width, availableSize.Height, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (string.IsNullOrEmpty(Text) || layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        var family = ParentWindow.Desktop.DefaultFontFamily;
        var textEngine = GetTextEngine();
        var resolved = textEngine.ResolveFont(new FontSpec(family, FontSize, CustomFontStyles.Normal));
        if (!resolved.IsAvailable)
        {
            return;
        }

        var scale = resolved.SuggestedScale;
        var textSize = textEngine.MeasureText(resolved, Text);
        var origin = textSize / 2.0f;
        var position = new Vector2(
            layoutBounds.X + layoutBounds.Width / 2.0f,
            layoutBounds.Y + layoutBounds.Height / 2.0f) + DA.Offset.ToVector2();

        DA.DT.DrawTextViaEngine(resolved, Text, position, TextColor * DA.Opacity, origin, scale, RotationRadians);
    }
}