using MGUI.Core.Tooling;
using MGUI.Core.UI.XAML;

namespace MGUI.Core.UI;

/// <summary>Thrown by an <see cref="MGWindow"/> when a second <see cref="MGElement"/> claims a <see cref="MGElement.Name"/> that the
/// window's index already holds. A name identifies at most one element of a window (ADR-0013): the index is what
/// <see cref="MGWindow.GetElementByName(string)"/> and <c>{MGBinding ElementName=...}</c> resolve through, so it cannot hold two
/// elements under one key.<para/>
/// Two very different mistakes end up here, and <see cref="Exception.Message"/> tells them apart. When both elements carry the
/// <see cref="XamlSourcePosition"/> of the <em>same</em> document node -- same <see cref="XamlSourcePosition.SourceName"/> and same
/// <see cref="XamlSourcePosition.Ordinal"/> -- the name was declared once, inside a template the loader cloned once per generated
/// element. Otherwise the name was simply declared twice, or assigned twice from code.<para/>
/// <see cref="LineNumber"/> and <see cref="LinePosition"/> are the position of the declaration the second element came from, or 0
/// when it has none (an element built by code, a control template part, a theme element). They are named exactly so that
/// <c>XamlLoaderDiagnostics</c> reads them back by reflection and carries them into a
/// <see cref="XamlLoaderDiagnosticCode.DuplicateElementName"/> diagnostic.</summary>
public sealed class MGDuplicateElementNameException : InvalidOperationException
{
    /// <summary>The name both elements carry.</summary>
    public string Name { get; }

    /// <summary>The element the window's index already holds under <see cref="Name"/>.</summary>
    public MGElement ExistingElement { get; }

    /// <summary>The element that tried to claim <see cref="Name"/> and was rejected.</summary>
    public MGElement NewElement { get; }

    /// <summary>1-based line of the declaration <see cref="NewElement"/> came from, or 0 when it carries no source position.</summary>
    public int LineNumber { get; }

    /// <summary>1-based column of the declaration <see cref="NewElement"/> came from, or 0 when it carries no source position.</summary>
    public int LinePosition { get; }

    public MGDuplicateElementNameException(string Name, MGElement ExistingElement, MGElement NewElement)
        : this(Name, ExistingElement, NewElement, Describe(Name, ExistingElement, NewElement)) { }

    private MGDuplicateElementNameException(string Name, MGElement ExistingElement, MGElement NewElement,
        (string Message, int LineNumber, int LinePosition) Description)
        : base(Description.Message)
    {
        this.Name = Name;
        this.ExistingElement = ExistingElement;
        this.NewElement = NewElement;
        this.LineNumber = Description.LineNumber;
        this.LinePosition = Description.LinePosition;
    }

    private static (string Message, int LineNumber, int LinePosition) Describe(string Name, MGElement ExistingElement, MGElement NewElement)
    {
        var ExistingType = DescribeType(ExistingElement);
        var NewType = DescribeType(NewElement);

        var HasExistingPosition = TryGetSourcePosition(ExistingElement, out var ExistingPosition);
        var HasNewPosition = TryGetSourcePosition(NewElement, out var NewPosition);

        var LineNumber = HasNewPosition ? NewPosition.LineNumber : 0;
        var LinePosition = HasNewPosition ? NewPosition.LinePosition : 0;

        //  Same document node, two instances: the loader deep-copies a ContentTemplate's content once per generated element and
        //  re-applies the declared Name to every copy, so the author sees one Name in the text and N elements at runtime.
        if (HasExistingPosition && HasNewPosition
            && ExistingPosition.Ordinal == NewPosition.Ordinal
            && string.Equals(ExistingPosition.SourceName, NewPosition.SourceName, StringComparison.Ordinal))
        {
            return ($"Duplicate element name '{Name}'. A {ExistingType} already carries that name in this window. " +
                    $"The name is declared once, at line {LineNumber}, column {LinePosition}, inside a template that is instantiated " +
                    $"more than once: a name may only identify one element of a window. Remove the name from the template content, " +
                    $"or make the template generate a single element.",
                LineNumber, LinePosition);
        }

        var Declared = HasNewPosition ? $" declared at line {LineNumber}, column {LinePosition}" : string.Empty;
        return ($"Duplicate element name '{Name}'. A {ExistingType} already carries that name in this window; " +
                $"the second element is a {NewType}{Declared}. A name may only identify one element of a window.",
            LineNumber, LinePosition);
    }

    private static bool TryGetSourcePosition(MGElement Element, out XamlSourcePosition Position)
    {
        if (Element == null)
        {
            Position = default;
            return false;
        }

        return UIToolingService.TryGetXamlSourcePosition(Element, out Position);
    }

    /// <summary>The runtime type name, with the arity suffix of a generic type stripped so that an <c>MGListBox&lt;object&gt;</c>
    /// reads as <c>MGListBox</c> rather than <c>MGListBox`1</c>.</summary>
    private static string DescribeType(MGElement Element)
    {
        if (Element == null)
        {
            return nameof(MGElement);
        }

        var TypeName = Element.GetType().Name;
        var Arity = TypeName.IndexOf('`');
        return Arity < 0 ? TypeName : TypeName[..Arity];
    }
}
