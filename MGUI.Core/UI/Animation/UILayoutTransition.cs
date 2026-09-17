using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Core.UI.Animation;

/// <summary>Opt-in settings for <see cref="MGElement.LayoutTransition"/> (ADR-0011 decision 5): when a real layout pass moves the element,
/// it glides from its previous visual position back to the new one, drawn and hit-tested through a dedicated layout offset
/// (<see cref="UILayoutTransform"/>) instead of a change to the layout itself.<para/>
/// This is a settings object: it never carries an animated value, so reading or writing any animation target never opts an element in on
/// its own. Only a non-null <see cref="MGElement.LayoutTransition"/> whose <see cref="Duration"/> is greater than zero does, checked at the
/// moment <see cref="MGElement.UpdateLayout"/> would start a run. <c>AnimateSize</c> (animating a size change too) is reserved for a later
/// slice (Y5) and deliberately not added here yet.</summary>
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
}
