using MonoGame.Extended;
using System.Diagnostics;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI;

public class MGSeparator : MGElement
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Orientation _Orientation;
    public Orientation Orientation
    {
        get => _Orientation;
        set
        {
            if (_Orientation != value)
            {
                _Orientation = value;
                // ADR-0005/S2/S7a: this reacts to the public Orientation property being changed AFTER
                // construction, so it is classified LocalValue -- the caller just set a property, so the
                // resulting margin must win like any other LocalValue write. The constructor no longer goes
                // through this setter (ADR-0005/S7a): it assigns the backing field directly and sets the
                // construction-time margin as DefaultValue instead, so a Theme/Template default or a style can
                // still override that initial margin; only a later change to Orientation forces LocalValue here.
                SetMargin(AutoMargin, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Orientation));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _Size;
    public int Size
    {
        get => _Size;
        set
        {
            if (_Size != value)
            {
                _Size = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Size));
            }
        }
    }

    private const int DefaultMarginAmount = 3;
    private Thickness AutoMargin => Orientation switch
    {
        Orientation.Horizontal => new(0, DefaultMarginAmount, 0, DefaultMarginAmount),
        Orientation.Vertical => new(DefaultMarginAmount, 0, DefaultMarginAmount, 0),
        _ => throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {Orientation}")
    };

    /// <param name="Size">For a vertical separator, this represents the width. For a horizontal separator, this represents the height.</param>
    public MGSeparator(MGWindow Window, Orientation Orientation, int Size = 2)
        : base(Window, MGElementType.Separator)
    {
        using (BeginInitializing())
        {
            // ADR-0005/S7a: assign the backing field directly rather than going through the public Orientation
            // setter. That setter writes the auto margin as LocalValue (precedence 90) so a later change to the
            // property (after construction) still wins outright; going through it here would pin the
            // construction-time margin at LocalValue too, permanently outranking the DefaultValue write below and
            // any Theme/Template/style default for Margin ever applied afterwards. Setting the field only leaves
            // the margin write below as the sole contribution, at DefaultValue, so a style can freely override it.
            _Orientation = Orientation;
            SetMargin(AutoMargin, UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            this.Size = Size;
        }
    }

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
    {
        SharedSize = new(0);

        if (Orientation == Orientation.Horizontal)
        {
            return new(1, Size, 0, 0);
        }
        else if (Orientation == Orientation.Vertical)
        {
            return new(Size, 1, 0, 0);
        }
        else
        {
            throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {Orientation}");
        }
    }
}