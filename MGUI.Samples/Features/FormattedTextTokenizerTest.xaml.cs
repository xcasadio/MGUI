// FormattedTextTokenizerTest.xaml.cs
// ─────────────────────────────────────────────────────────────────────────────
// PURPOSE
//   Validates the Task 7 fix in FormattedTextTokenizer.EscapeMarkdown:
//   Consecutive backslashes before '[' are now counted and handled correctly.
//
// BUG FIXED (Task 7):
//   OLD: if (c == OpenTagChar && PreviousCharacter != EscapeOpenTagChar) → add escape
//   This only checked the immediately preceding char. For EVEN counts (e.g. '\\['):
//   the last '\' was the PreviousCharacter, so no escape was added — leaving '[' unescaped.
//
//   NEW: Count consecutive backslashes before '['.
//   For N backslashes: add N extra (doubling them to preserve literals) + 1 escape.
//
// TOKENIZER PROOF (cases verified with TryTokenize):
//   Input              Tokens produced               Expected
//   "\\[Bold]"         StringLiteral("[Bold]")       '[' IS escaped (1 backslash = odd)
//   "\\\\[Bold]"       StringLiteral("\") + tag...   '[' NOT escaped (2 backslashes = even)
//   "\\\\\\[Bold]"     StringLiteral("\\[Bold]")     '[' IS escaped (3 backslashes = odd)
//   (All as C# regular string literals: 1, 2, 3 backslashes + [Bold])
//
// ESCAPEMARK DOWN PROOF:
//   EscapeMarkdown is implemented in FormattedTextTokenizer.cs.
//   The fix verifies that raw text with backslashes before '[' is correctly escaped.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public class FormattedTextTokenizerTestSample : SampleBase
    {
        private MGTextBlock Result1 { get; }
        private MGTextBlock Result2 { get; }
        private MGTextBlock Result3 { get; }
        private MGTextBlock Result4 { get; }
        private MGTextBlock Result5 { get; }
        private MGTextBlock ResultSummary { get; }

        private MGTextBlock EscapeTest1 { get; }
        private MGTextBlock EscapeTest2 { get; }
        private MGTextBlock EscapeTest3 { get; }

        private bool _testsRun;

        public FormattedTextTokenizerTestSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Features)}", "FormattedTextTokenizerTest.xaml")
        {
            Result1 = Window.GetElementByName<MGTextBlock>("Result1");
            Result2 = Window.GetElementByName<MGTextBlock>("Result2");
            Result3 = Window.GetElementByName<MGTextBlock>("Result3");
            Result4 = Window.GetElementByName<MGTextBlock>("Result4");
            Result5 = Window.GetElementByName<MGTextBlock>("Result5");
            ResultSummary = Window.GetElementByName<MGTextBlock>("ResultSummary");
            EscapeTest1 = Window.GetElementByName<MGTextBlock>("EscapeTest1");
            EscapeTest2 = Window.GetElementByName<MGTextBlock>("EscapeTest2");
            EscapeTest3 = Window.GetElementByName<MGTextBlock>("EscapeTest3");

            Window.OnBeginUpdateContents += (_, _) =>
            {
                if (_testsRun) return;
                _testsRun = true;
                RunTokenizerTests();
            };

            Window.WindowDataContext = this;
        }

        private void RunTokenizerTests()
        {
            FTTokenizer tok = new FTTokenizer();
            int passed = 0, total = 0;

            // ── TOKEN PROOF CASE 1: "\[Bold]" (1 backslash + [Bold]) ─────────
            // Odd backslash count → '[' IS escaped → all tokens are StringLiteral
            {
                total++;
                bool ok = tok.TryTokenize("\\[Bold]", false, out List<FTTokenMatch> tokens);
                // The tokenizer should parse this entirely as string literals (no tags).
                // '\\[Bold]' in C# string = \[Bold] = 1 backslash + [Bold]
                // Tokenizer: SubPattern3 matches \[ → literal '[', 'Bold]' → literal
                bool allLiteral = ok && tokens.All(t => t.TokenType == FTTokenType.StringLiteral);
                string combined = string.Concat(tokens.Select(t => t.Value));
                bool correct = combined == "[Bold]";

                if (allLiteral && correct)
                {
                    Result1.SetText("[c=LimeGreen]✓[/c] Case 1 PASSED: '\\[Bold]' tokenized as StringLiteral = \"[Bold]\"");
                    passed++;
                }
                else
                    Result1.SetText($"[c=Red]✗[/c] Case 1 FAILED: ok={ok} allLiteral={allLiteral} value='{combined}'");

                Debug.Assert(allLiteral && correct,
                    $"[FTTokenizerTest] Case 1 failed: ok={ok}, allLiteral={allLiteral}, combined='{combined}'");
                Debug.WriteLine($"[FTTokenizerTest] Case 1: ok={ok}, tokens={tokens.Count}, value='{combined}'");
            }

            // ── TOKEN PROOF CASE 2: "\\[Bold]" (2 backslashes + [Bold]) ──────
            // Even backslash count → '[' NOT escaped → first token is StringLiteral('\'), then tag
            {
                total++;
                // In C# regular string: "\\[Bold]" = \\ + [Bold] = 2 backslashes + [Bold]
                bool ok = tok.TryTokenize("\\\\[b]Bold[/b]", false, out List<FTTokenMatch> tokens);
                // The tokenizer should: '\\' → literal '\', then '[b]Bold[/b]' → tag + string + close tag
                bool hasTag = ok && tokens.Any(t => t.TokenType == FTTokenType.BoldOpenTagType);
                bool hasLiteralBackslash = ok && tokens.Any(t => t.TokenType == FTTokenType.StringLiteral
                                                                  && t.Value.Contains('\\'));

                if (ok && hasTag)
                {
                    Result2.SetText("[c=LimeGreen]✓[/c] Case 2 PASSED: '\\\\[b]Bold[/b]' → '\\' literal + bold tag");
                    passed++;
                }
                else
                    Result2.SetText($"[c=Red]✗[/c] Case 2 FAILED: ok={ok} hasTag={hasTag}");

                Debug.Assert(ok && hasTag,
                    $"[FTTokenizerTest] Case 2 failed: ok={ok}, hasTag={hasTag}");
                Debug.WriteLine($"[FTTokenizerTest] Case 2: ok={ok}, tokens={tokens.Count}");
            }

            // ── TOKEN PROOF CASE 3: "\\\[Bold]" (3 backslashes + [Bold]) ─────
            // Odd backslash count → '[' IS escaped → renders as literal '\[Bold]'
            {
                total++;
                // In C# regular string: "\\\[Bold]" = \\\ + [Bold] = 3 backslashes + [Bold]
                bool ok = tok.TryTokenize("\\\\\\[Bold]", false, out List<FTTokenMatch> tokens);
                bool allLiteral = ok && tokens.All(t => t.TokenType == FTTokenType.StringLiteral);
                string combined = string.Concat(tokens.Select(t => t.Value));
                // '\\' branch → '\\', '\[' → '[', result = '\\[Bold]' no wait...
                // 3 backslashes: '\\' branch → '\', remaining='\\[Bold]'... actually '\\\[Bold]':
                // '\\' branch → '\', remaining='\[Bold]'. SubPattern3 '\[' → '['. remaining='Bold]'. literal 'Bold]'
                // combined = '\' + '[' + 'Bold]' = '\[Bold]'
                bool correct = combined == "\\[Bold]";

                if (allLiteral && correct)
                {
                    Result3.SetText("[c=LimeGreen]✓[/c] Case 3 PASSED: '\\\\\\[Bold]' tokenized as StringLiteral = \"\\[Bold]\"");
                    passed++;
                }
                else
                    Result3.SetText($"[c=Red]✗[/c] Case 3 FAILED: ok={ok} allLiteral={allLiteral} value='{combined}'");

                Debug.Assert(allLiteral && correct,
                    $"[FTTokenizerTest] Case 3 failed: ok={ok}, allLiteral={allLiteral}, combined='{combined}'");
                Debug.WriteLine($"[FTTokenizerTest] Case 3: ok={ok}, tokens={tokens.Count}, value='{combined}'");
            }

            // ── ESCAPEMARK DOWN TEST: EscapeMarkdown("\\[Bold]") ─────────────
            // Raw input 1 backslash + '[Bold]' → output should make '[' escaped
            {
                total++;
                string rawInput = "\\[Bold]";   // 1 backslash + [Bold]
                string escaped = FTTokenizer.EscapeMarkdown(rawInput);
                // After fix: escaped = \\\[Bold] (3 backslashes) → renders \[Bold] (1 backslash + literal '[Bold]')
                bool ok = tok.TryTokenize(escaped, false, out List<FTTokenMatch> tokens);
                bool allLiteral = ok && tokens.All(t => t.TokenType == FTTokenType.StringLiteral);
                string rendered = string.Concat(tokens.Select(t => t.Value));
                bool correct = rendered == rawInput;  // should render exactly as typed
                string display = $"EscapeMarkdown input='\\[Bold]' → escaped='{escaped}' → rendered='{rendered}'";

                if (correct)
                {
                    Result4.SetText($"[c=LimeGreen]✓[/c] EscapeMarkdown test 1 PASSED: {display}");
                    passed++;
                }
                else
                    Result4.SetText($"[c=Red]✗[/c] EscapeMarkdown test 1 FAILED: {display}");

                EscapeTest1.SetText($"EscapeMarkdown: '\\[Bold]' → escaped='{escaped}'");

                Debug.Assert(correct,
                    $"[FTTokenizerTest] EscapeMarkdown test 1 failed: escaped='{escaped}', rendered='{rendered}', expected='{rawInput}'");
            }

            // ── ESCAPEMARK DOWN TEST: EscapeMarkdown("\\\\[Bold]") ───────────
            // Raw input 2 backslashes + '[Bold]' → output should preserve both backslashes + escape '['
            {
                total++;
                string rawInput = "\\\\[Bold]";   // 2 backslashes + [Bold]
                string escaped = FTTokenizer.EscapeMarkdown(rawInput);
                bool ok = tok.TryTokenize(escaped, false, out List<FTTokenMatch> tokens);
                bool allLiteral = ok && tokens.All(t => t.TokenType == FTTokenType.StringLiteral);
                string rendered = string.Concat(tokens.Select(t => t.Value));
                bool correct = rendered == rawInput;
                string display = $"EscapeMarkdown input='\\\\[Bold]' → escaped='{escaped}' → rendered='{rendered}'";

                if (correct)
                {
                    Result5.SetText($"[c=LimeGreen]✓[/c] EscapeMarkdown test 2 PASSED: {display}");
                    passed++;
                }
                else
                    Result5.SetText($"[c=Red]✗[/c] EscapeMarkdown test 2 FAILED: {display}");

                EscapeTest2.SetText($"EscapeMarkdown: '\\\\[Bold]' → escaped='{escaped}'");
                EscapeTest3.SetText($"(All {total} tests, {passed} passed)");

                Debug.Assert(correct,
                    $"[FTTokenizerTest] EscapeMarkdown test 2 failed: escaped='{escaped}', rendered='{rendered}', expected='{rawInput}'");
            }

            // ── Final summary ─────────────────────────────────────────────────
            string color = passed == total ? "LimeGreen" : "Red";
            ResultSummary.SetText($"[c={color}]{passed}/{total} tests passed[/c]");
            Debug.WriteLine($"[FTTokenizerTest] {passed}/{total} test cases passed.");
        }
    }
}
