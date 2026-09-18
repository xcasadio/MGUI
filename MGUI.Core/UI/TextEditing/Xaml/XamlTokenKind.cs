namespace MGUI.Core.UI.TextEditing.Xaml;

/// <summary>The role a single <see cref="XamlToken"/> plays in a tolerant, non-validating scan of XAML markup
/// (backlog task 3 of <c>Docs/Tasks/richtextbox-autocomplete-tasks.md</c>). The tokenizer never throws: an input
/// it cannot make sense of simply produces <see cref="Unknown"/> tokens instead of failing.</summary>
public enum XamlTokenKind
{
    LessThan,
    GreaterThan,
    Slash,
    Equals,
    Identifier,
    String,
    Whitespace,
    Text,
    OpenBrace,
    CloseBrace,
    Colon,
    Dot,
    Comment,
    Unknown,
    EndOfFile
}
