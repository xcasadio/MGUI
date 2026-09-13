using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Interpolation;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// The fluent entry point (ADR-0007, decision 9): sugar over <see cref="UIPropertyAnimation{T}"/> and <see cref="UISequenceAnimation"/>, no new
/// concept. <c>element.Animate("Opacity", 0f, 1f, 0.3).Ease(UIEasing.CubicOut).Play()</c> starts one animation; <c>.Then(...)</c> appends a step and
/// <c>Play</c> then starts a sequence; <c>Build</c> returns the animation without starting it.
/// </summary>
public static class UIAnimateExtensions
{
    /// <summary>Starts building an animation of <paramref name="property"/> from <paramref name="from"/> to <paramref name="to"/> over <paramref name="seconds"/>.</summary>
    public static UIAnimationBuilder<T> Animate<T>(this MGElement element, string property, T from, T to, double seconds)
        => Animate(element, property, from, to, TimeSpan.FromSeconds(seconds));

    /// <summary>Starts building an animation of <paramref name="property"/> from its current value to <paramref name="to"/> over <paramref name="seconds"/>.</summary>
    public static UIAnimationBuilder<T> Animate<T>(this MGElement element, string property, T to, double seconds)
        => Animate(element, property, to, TimeSpan.FromSeconds(seconds));

    public static UIAnimationBuilder<T> Animate<T>(this MGElement element, string property, T from, T to, TimeSpan duration)
        => new(RequireElement(element), new UIAnimationBuilder.Chain(), UIAnimationBuilder.CreateStep(property, from, true, to, duration));

    public static UIAnimationBuilder<T> Animate<T>(this MGElement element, string property, T to, TimeSpan duration)
        => new(RequireElement(element), new UIAnimationBuilder.Chain(), UIAnimationBuilder.CreateStep(property, default, false, to, duration));

    private static MGElement RequireElement(MGElement element) => element ?? throw new ArgumentNullException(nameof(element));
}

/// <summary>The untyped part of a fluent chain: the element, the steps built so far, and what every step type can do (chain, play, build).</summary>
public abstract class UIAnimationBuilder
{
    /// <summary>What the builders of one chain share: the steps, and the sequence built from them (built once, so <c>Build</c> then <c>Play</c> return the same instance).</summary>
    internal sealed class Chain
    {
        public readonly List<UIAnimation> Steps = new();
        public UISequenceAnimation Sequence;
    }

    private readonly Chain _Chain;

    private protected UIAnimationBuilder(MGElement element, Chain chain, UIAnimation current)
    {
        Element = element;
        _Chain = chain;
        Current = current;
        _Chain.Steps.Add(current);
        _Chain.Sequence = null;
    }

    /// <summary>The element the chain animates.</summary>
    public MGElement Element { get; }

    /// <summary>The step the typed options apply to (the last one added).</summary>
    public UIAnimation Current { get; }

    /// <summary>Every step of the chain, in order.</summary>
    public IReadOnlyList<UIAnimation> Steps => _Chain.Steps;

    /// <summary>Appends a step that runs after the current one: <c>Play</c> then starts a <see cref="UISequenceAnimation"/>.</summary>
    public UIAnimationBuilder<TNext> Then<TNext>(string property, TNext from, TNext to, double seconds)
        => Then(property, from, to, TimeSpan.FromSeconds(seconds));

    public UIAnimationBuilder<TNext> Then<TNext>(string property, TNext to, double seconds)
        => Then(property, to, TimeSpan.FromSeconds(seconds));

    public UIAnimationBuilder<TNext> Then<TNext>(string property, TNext from, TNext to, TimeSpan duration)
        => new(Element, _Chain, CreateStep(property, from, true, to, duration));

    public UIAnimationBuilder<TNext> Then<TNext>(string property, TNext to, TimeSpan duration)
        => new(Element, _Chain, CreateStep(property, default, false, to, duration));

    /// <summary>Appends an animation built elsewhere (a keyframe animation) as the next step.</summary>
    public UIAnimationBuilder<TNext> Then<TNext>(UIPropertyAnimation<TNext> animation)
        => new(Element, _Chain, animation ?? throw new ArgumentNullException(nameof(animation)));

    /// <summary>Appends a pause of <paramref name="seconds"/> before the next step.</summary>
    public UIAnimationBuilder Wait(double seconds) => Wait(TimeSpan.FromSeconds(seconds));

    public UIAnimationBuilder Wait(TimeSpan duration)
        => new UIDelayBuilder(Element, _Chain, new UIDelayAnimation(duration < TimeSpan.Zero ? TimeSpan.Zero : duration));

    /// <summary>The animation the chain describes: the single step, or a <see cref="UISequenceAnimation"/> of the steps (named after the first named
    /// step), built once per chain.</summary>
    public UIAnimation Build()
    {
        var steps = _Chain.Steps;
        if (steps.Count == 1)
        {
            return steps[0];
        }

        if (_Chain.Sequence == null)
        {
            UISequenceAnimation sequence = new();
            for (var i = 0; i < steps.Count; i++)
            {
                sequence.Append(steps[i]);
                sequence.Name ??= steps[i].Name;
            }

            _Chain.Sequence = sequence;
        }

        return _Chain.Sequence;
    }

    /// <summary>Builds and starts the animation on <see cref="Element"/>; returns it (a <see cref="UISequenceAnimation"/> when the chain has several steps).</summary>
    public UIAnimation Play()
    {
        var animation = Build();
        Element.Animations.Start(animation);
        return animation;
    }

    internal static UIPropertyAnimation<T> CreateStep<T>(string property, T from, bool hasFrom, T to, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(property))
        {
            throw new ArgumentException("An animation needs a property path.", nameof(property));
        }

        //  Resolved now, so a wrong path or value type fails where the chain is written, not when it plays.
        UIAnimationTargets.Resolve<T>(property);
        UIPropertyAnimation<T> step = new(property) { To = to, Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration };
        if (hasFrom)
        {
            step.From = from;
        }

        return step;
    }

    /// <summary>The builder positioned on a <see cref="UIDelayAnimation"/> step: only the chain operations apply.</summary>
    private sealed class UIDelayBuilder : UIAnimationBuilder
    {
        public UIDelayBuilder(MGElement element, Chain chain, UIDelayAnimation delay)
            : base(element, chain, delay) { }
    }
}

/// <summary>The typed options of the current step of a fluent chain (see <see cref="UIAnimateExtensions.Animate{T}(MGElement, string, T, T, double)"/>).</summary>
public sealed class UIAnimationBuilder<T> : UIAnimationBuilder
{
    internal UIAnimationBuilder(MGElement element, Chain chain, UIPropertyAnimation<T> animation)
        : base(element, chain, animation)
    {
        Animation = animation;
    }

    /// <summary>The step being configured.</summary>
    public UIPropertyAnimation<T> Animation { get; }

    public UIAnimationBuilder<T> Ease(IUIEasingFunction easing)
    {
        Animation.Easing = easing;
        return this;
    }

    /// <summary>An easing by name (<see cref="UIEasing.TryGet"/>).</summary>
    /// <exception cref="ArgumentException">The name is unknown.</exception>
    public UIAnimationBuilder<T> Ease(string easingName)
    {
        if (!UIEasing.TryGet(easingName, out var easing))
        {
            throw new ArgumentException($"Unknown easing '{easingName}'. Known names: {string.Join(", ", UIEasing.Names)}.", nameof(easingName));
        }

        return Ease(easing);
    }

    public UIAnimationBuilder<T> Interpolate(IUIInterpolator<T> interpolator)
    {
        Animation.Interpolator = interpolator;
        return this;
    }

    public UIAnimationBuilder<T> Delay(double seconds) => Delay(TimeSpan.FromSeconds(seconds));

    public UIAnimationBuilder<T> Delay(TimeSpan delay)
    {
        Animation.Delay = delay;
        return this;
    }

    /// <summary>Plays the step <paramref name="count"/> times in total (see <see cref="UIAnimation.RepeatCount"/>).</summary>
    public UIAnimationBuilder<T> Repeat(int count)
    {
        Animation.RepeatCount = count;
        return this;
    }

    public UIAnimationBuilder<T> RepeatForever()
    {
        Animation.RepeatForever = true;
        return this;
    }

    public UIAnimationBuilder<T> AutoReverse(bool value = true)
    {
        Animation.AutoReverse = value;
        return this;
    }

    public UIAnimationBuilder<T> Fill(UIAnimationFillBehavior behavior)
    {
        Animation.FillBehavior = behavior;
        return this;
    }

    public UIAnimationBuilder<T> OnCancel(UIAnimationCancelBehavior behavior)
    {
        Animation.CancelBehavior = behavior;
        return this;
    }

    public UIAnimationBuilder<T> Named(string name)
    {
        Animation.Name = name;
        return this;
    }

    /// <summary>Anything the shorthands do not cover.</summary>
    public UIAnimationBuilder<T> Configure(Action<UIPropertyAnimation<T>> configure)
    {
        (configure ?? throw new ArgumentNullException(nameof(configure)))(Animation);
        return this;
    }
}