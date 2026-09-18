using MGUI.Core.Tooling;
using MGUI.Core.UI.XAML;

namespace MGUI.Core.UI;

/// <summary>Thrown by an <see cref="MGWindow"/> when a second <see cref="MGElement"/> claims a <see cref="MGElement.Name"/> that the
/// window's index already holds. A name identifies at most one element of a window (ADR-0013): the index is what
/// <see cref="MGWindow.GetElementByName(string)"/> and <c>{MGBinding ElementName=...}</c> resolve through, so it cannot hold two
/// elements under one key.<para/>
/// Two very different mistakes end up here, and <see cref="Exception.Message"/> distinguishes them by what it can actually observe:
/// whether both elements carry the <em>same</em> <see cref="XamlSourcePosition"/> -- same <see cref="XamlSourcePosition.SourceName"/>
/// and same <see cref="XamlSourcePosition.Ordinal"/>. That is evidence of one declaration reaching two elements, which a template
/// applied to more than one element does; it is reported as such, as the usual cause, not asserted as proven identity, because a
/// position is only unique within one parse of one document (<see cref="XamlSourcePosition.Ordinal"/> restarts at zero for every
/// parse and a display name is not required to be unique). Otherwise the name was declared twice, or assigned twice from code.<para/>
/// <see cref="LineNumber"/> and <see cref="LinePosition"/> are the position of the declaration the second element came from, or 0
/// when it has none (an element built by code, a control template part, a theme element). They are plain <see langword="int"/>
/// properties under these exact names so that a host which catches this exception -- while attaching a preview, for instance -- can
/// report it at that position. No loader code reads them back today: this exception is raised by the window's index, outside any
/// loader call, so it never becomes a <see cref="XamlLoaderDiagnostic"/> on its own (ADR-0013).</summary>
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

        //  One position, two elements: the loader deep-copies a ContentTemplate's content once per generated element and re-applies
        //  the declared Name to every copy, so the author sees one Name in the text and N elements at runtime. The message reports
        //  the shared position as the evidence it is, and names that cause as the usual one, rather than asserting it: a position is
        //  only unique within one parse of one document, so two parses of two documents that share a display name can collide here
        //  with no template involved at all.
        if (HasExistingPosition && HasNewPosition
            && ExistingPosition.Ordinal == NewPosition.Ordinal
            && string.Equals(ExistingPosition.SourceName, NewPosition.SourceName, StringComparison.Ordinal))
        {
            var OfSource = string.IsNullOrWhiteSpace(NewPosition.SourceName) ? string.Empty : $" of '{NewPosition.SourceName}'";
            return ($"Duplicate element name '{Name}'. A {ExistingType} already carries that name in this window, and both elements " +
                    $"carry the same source position: line {LineNumber}, column {LinePosition}{OfSource}. A name declared once and " +
                    $"reaching more than one element is what a template does, applying it to every element it generates. Remove the " +
                    $"name from the template content, or make the template generate a single element. A name may only identify one " +
                    $"element of a window.",
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
