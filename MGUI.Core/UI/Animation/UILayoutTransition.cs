using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Core.UI.Animation;

/// <summary>Opt-in settings for <see cref="MGElement.LayoutTransition"/> (ADR-0011 decision 5): when a real layout pass moves the element,
/// it glides from its previous visual position back to the new one, drawn and hit-tested through a dedicated layout offset
/// (<see cref="UILayoutTransform"/>) instead of a change to the layout itself. When <see cref="AnimateSize"/> is also set, a size change is
/// animated the same way, through <see cref="UILayoutTransform.Scale"/> (Y5).<para/>
/// This is a settings object: it never carries an animated value, so reading or writing any animation target never opts an element in on
/// its own. Only a non-null <see cref="MGElement.LayoutTransition"/> whose <see cref="Duration"/> is greater than zero does, checked at the
/// moment <see cref="MGElement.UpdateLayout"/> would start a run.</summary>
public sealed class UILayoutTransition
{
    private TimeSpan _duration;

    /// <summary>How long the glide back to the new position takes. Negative values are refused. Zero (the default) means no transition ever
    /// plays, exactly like a null <see cref="MGElement.LayoutTransition"/>.</summary>
    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(Duration)} cannot be negative.");
            }

            _duration = value;
        }
    }

    /// <summary>The easing function applied to the glide. Null (the default) means linear.</summary>
    public IUIEasingFunction Easing { get; set; }

    /// <summary>Whether a size change of <see cref="MGElement.LayoutBounds"/> is animated too (Y5), on top of a position change: the content
    /// is stretched during the run, as in any FLIP technique. False (the default) means a size change alone plays nothing, exactly like
    /// Y4's behaviour.</summary>
    public bool AnimateSize { get; set; }
}
