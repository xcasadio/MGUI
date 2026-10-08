using System.Text;

namespace MGUI.Shared.Text
{
    /// <summary>
    /// The characters whose ink defines the line box of the built-in text engines (ADR-0023):
    /// U+0020 to U+024F (Basic Latin, Latin-1 Supplement, Latin Extended-A and B), which is also the
    /// character range of the SpriteFont atlases MGUI ships. Whitespace and control characters carry
    /// no ink and are left out.
    /// <para/>
    /// A line box spans from the highest to the lowest ink of these characters, so a glyph of the
    /// repertoire is never drawn outside the <see cref="ResolvedFont.LineHeight"/> of its line.
    /// Characters outside the repertoire may extend past the box.
    /// </summary>
    public static class LineBoxRepertoire
    {
        /// <summary>The first character of the repertoire.</summary>
        public const char First = ' ';

        /// <summary>The last character of the repertoire.</summary>
        public const char Last = 'ɏ';

        /// <summary>Every inked character of the repertoire, in code point order.</summary>
        public static string InkedCharacters { get; } = BuildInkedCharacters();

        /// <summary>True when <paramref name="c"/> belongs to the repertoire and carries ink
        /// (not whitespace, not a control character).</summary>
        public static bool Contains(char c)
            => c >= First && c <= Last && !char.IsWhiteSpace(c) && !char.IsControl(c);

        private static string BuildInkedCharacters()
        {
            var builder = new StringBuilder(Last - First + 1);
            for (int code = First; code <= Last; code++)
            {
                char c = (char)code;
                if (Contains(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
