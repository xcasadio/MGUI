using System.IO;
using System.Reflection;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using XamlElement = MGUI.Core.UI.XAML.Element;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Xaml;

/// <summary>X1: the loader stamps a <see cref="XamlSourcePosition"/> (ordinal, line, column) on every <see cref="XamlElement"/> DTO it
/// creates from an XML object element, and <see cref="UIToolingService.TryGetXamlSourcePosition(MGElement, out XamlSourcePosition)"/>
/// relays it to the created <see cref="MGElement"/>. Covers the acceptance items of Docs/Tasks/xaml-editor-tasks.md's X1 section.</summary>
public class XamlSourcePositionTests
{
    private const string Ns = "clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core";
    private const string NsX = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string NsSystem = "clr-namespace:System;assembly=mscorlib";
    private const string NsBinding = "clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core";

    /// <summary>Locates <paramref name="tagOpenMarker"/> (for example <c>"&lt;StackPanel "</c>) in <paramref name="text"/> and returns the
    /// 1-based (line, column) of the character right after its <c>&lt;</c> -- computed independently of the parser under test, by simply
    /// counting the newlines and the offset from the last one.</summary>
    private static (int Line, int Column) LocateTagPosition(string text, string tagOpenMarker)
    {
        var index = text.IndexOf(tagOpenMarker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Marker not found: {tagOpenMarker}");

        var nameStart = index + 1; // skip '<'
        var line = 1;
        var lastNewline = -1;
        for (var i = 0; i < nameStart; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lastNewline = i;
            }
        }

        var column = nameStart - lastNewline;
        return (line, column);
    }

    /// <summary>Recursively collects every reachable <see cref="XamlElement"/> DTO from <paramref name="root"/>: its
    /// <see cref="XamlElement.GetChildren"/> plus whatever its (and every held object's) public properties hold, directly or through an
    /// <see cref="System.Collections.IEnumerable"/> -- reaching content declared through property-element syntax (<c>Button.ToolTip</c>,
    /// <c>Button.Background</c>) that <see cref="XamlElement.GetChildren"/> alone does not walk. Used only to independently rebuild
    /// "every element the loader produced" for cross-checks against <see cref="MGUI.Editor.Document.XamlDocumentModel"/> and against
    /// <c>System.Xaml.XamlServices.Parse</c>; never a substitute for the DTO's own real behaviour.</summary>
    private static void CollectElements(object node, HashSet<XamlElement> visited)
    {
        if (node == null)
        {
            return;
        }

        if (node is XamlElement element)
        {
            if (!visited.Add(element))
            {
                return;
            }

            foreach (var child in element.GetChildren())
            {
                CollectElements(child, visited);
            }
        }

        var type = node.GetType();
        if (type.Namespace == null || !type.Namespace.StartsWith("MGUI.Core.UI.XAML", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object value;
            try
            {
                value = property.GetValue(node);
            }
            catch
            {
                continue;
            }

            if (value == null || value is string)
            {
                continue;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    CollectElements(item, visited);
                }
            }
            else
            {
                CollectElements(value, visited);
            }
        }
    }

    private const string NestedFixture =
        "<Window xmlns=\"" + Ns + "\" xmlns:dataBinding=\"" + NsBinding + "\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\" TitleText=\"X1\">\n" +
        "    <Window.Styles>\n" +
        "        <Style Name=\"Header1\" TargetType=\"TextBlock\">\n" +
        "            <Setter Property=\"IsBold\" Value=\"True\" />\n" +
        "        </Style>\n" +
        "    </Window.Styles>\n" +
        "    <StackPanel Name=\"Root\" Orientation=\"Vertical\">\n" +
        "        <Button Name=\"MyButton\">\n" +
        "            <Button.Background>\n" +
        "                <CompositedFillBrush>\n" +
        "                    <DiagonalGradientFillBrush Color1=\"rgb(24,24,24)\" Color2=\"LightBlue\" />\n" +
        "                </CompositedFillBrush>\n" +
        "            </Button.Background>\n" +
        "            <Button.ToolTip>\n" +
        "                <ToolTip Name=\"MyToolTip\" Padding=\"8,5\">\n" +
        "                    <TextBlock Name=\"ToolTipLabel\" Text=\"Hi\" />\n" +
        "                </ToolTip>\n" +
        "            </Button.ToolTip>\n" +
        "            <Button.ContextMenu>\n" +
        "                <ContextMenu Name=\"MyContextMenu\">\n" +
        "                    <ContextMenuButton Name=\"MenuItem1\" Content=\"Open\" />\n" +
        "                </ContextMenu>\n" +
        "            </Button.ContextMenu>\n" +
        "            <TextBlock Name=\"ButtonLabel\" Text=\"{dataBinding:MGBinding ElementName=MyButton, Path=IsEnabled}\" />\n" +
        "        </Button>\n" +
        "    </StackPanel>\n" +
        "</Window>\n";

    private static Window ParseNestedFixture() =>
        XAMLParser.ParseWindowDefinition(XamlDocumentSource.FromString(NestedFixture, "fixture.xaml"), null, XamlLoaderMode.Strict, false, true);

    private static XamlElement FindByName(XamlElement root, string name)
    {
        HashSet<XamlElement> visited = new(ReferenceEqualityComparer.Instance);
        CollectElements(root, visited);
        return visited.SingleOrDefault(e => e.Name == name);
    }

    [Fact]
    public void Ordinal_Line_And_Column_AreExact_OnANestedDocument()
    {
        Window window = ParseNestedFixture();

        //  Document order among Element-derived object elements: Window(0), StackPanel(1), Button(2), ToolTip(3),
        //  TextBlock/ToolTipLabel(4), ContextMenu(5), ContextMenuButton/MenuItem1(6), TextBlock/ButtonLabel(7).
        //  Style, Setter, CompositedFillBrush and DiagonalGradientFillBrush do not resolve to an Element DTO and do not count;
        //  Window.Styles, Button.Background, Button.ToolTip and Button.ContextMenu are property elements and do not count either.
        Assert.True(window.SourcePosition.HasValue);
        Assert.Equal("fixture.xaml", window.SourcePosition.Value.SourceName);
        Assert.Equal(0, window.SourcePosition.Value.Ordinal);
        AssertPosition(window.SourcePosition.Value, "<Window ");

        AssertNamedElementPosition(window, "Root", 1, "<StackPanel ");
        AssertNamedElementPosition(window, "MyButton", 2, "<Button ");
        AssertNamedElementPosition(window, "MyToolTip", 3, "<ToolTip ");
        AssertNamedElementPosition(window, "ToolTipLabel", 4, "<TextBlock Name=\"ToolTipLabel\"");
        AssertNamedElementPosition(window, "MyContextMenu", 5, "<ContextMenu ");
        AssertNamedElementPosition(window, "MenuItem1", 6, "<ContextMenuButton ");
        AssertNamedElementPosition(window, "ButtonLabel", 7, "<TextBlock Name=\"ButtonLabel\"");

        void AssertNamedElementPosition(Window w, string name, int ordinal, string marker)
        {
            var found = FindByName(w, name);
            Assert.True(found?.SourcePosition.HasValue, $"{name} has no SourcePosition");
            Assert.Equal(ordinal, found.SourcePosition.Value.Ordinal);
            AssertPosition(found.SourcePosition.Value, marker);
        }

        void AssertPosition(XamlSourcePosition position, string marker)
        {
            var (line, column) = LocateTagPosition(NestedFixture, marker);
            Assert.Equal(line, position.LineNumber);
            Assert.Equal(column, position.LinePosition);
        }
    }

    [Fact]
    public void LinePosition_AccountsForTheLinebreakLiteralReplacement_InThePreparedMarkup()
    {
        //  The literal "\n" (backslash + n, two characters) inside the TextBlock's Text attribute is replaced with "&#x0a;" (six
        //  characters) by PrepareMarkup before the loader ever sees the markup, shifting every later column on that same line by +4.
        var raw = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"1\" Height=\"1\"><StackPanel>" +
                   "<TextBlock Text=\"A\\nB\" /><Button Name=\"X\" /></StackPanel></Window>";
        var prepared = raw.Replace(@"\n", "&#x0a;");

        Window window = XAMLParser.ParseWindowDefinition(XamlDocumentSource.FromString(raw, "literal.xaml"), null, XamlLoaderMode.Strict, false, true);
        var button = FindByName(window, "X");
        Assert.True(button?.SourcePosition.HasValue);

        var (line, column) = LocateTagPosition(prepared, "<Button ");
        Assert.Equal(line, button.SourcePosition.Value.LineNumber);
        Assert.Equal(column, button.SourcePosition.Value.LinePosition);
    }

    [Fact]
    public void LoaderOrdinal_And_DocumentModelOrdinal_AgreeOnTheSameNodes_OnTheNestedFixture()
    {
        Window window = ParseNestedFixture();
        HashSet<XamlElement> elements = new(ReferenceEqualityComparer.Instance);
        CollectElements(window, elements);

        var byOrdinal = elements.Where(e => e.SourcePosition.HasValue)
            .OrderBy(e => e.SourcePosition.Value.Ordinal)
            .ToList();

        Assert.True(MGUI.Editor.Document.XamlDocumentModel.TryParse(NestedFixture, out var model, out var error), error?.Message);

        var ordinal = 0;
        foreach (var element in byOrdinal)
        {
            Assert.True(model.TryGetNodeByOrdinal(ordinal, out var node), $"No document-model node for ordinal {ordinal}");
            Assert.Equal(element.GetType().Name, node.DtoType?.Name);
            Assert.Equal(element.SourcePosition.Value.LineNumber, IndexToLine(NestedFixture, node.StartTagRange.StartIndex + 1));
            Assert.Equal(element.SourcePosition.Value.LinePosition, IndexToColumn(NestedFixture, node.StartTagRange.StartIndex + 1));
            ordinal++;
        }

        Assert.False(model.TryGetNodeByOrdinal(ordinal, out _));
    }

    [Fact]
    public void LoaderOrdinal_And_DocumentModelOrdinal_AgreeOnTheSameNodes_OnCheckBoxSample()
    {
        var samplePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MGUI.Samples", "Controls", "CheckBox.xaml"));
        var raw = File.ReadAllText(samplePath);
        var prepared = raw.Replace(@"\n", "&#x0a;");

        Window window = XAMLParser.ParseWindowDefinition(XamlDocumentSource.FromString(raw, "CheckBox.xaml"), null, XamlLoaderMode.Compatibility, false, true);
        HashSet<XamlElement> elements = new(ReferenceEqualityComparer.Instance);
        CollectElements(window, elements);
        var byOrdinal = elements.Where(e => e.SourcePosition.HasValue)
            .OrderBy(e => e.SourcePosition.Value.Ordinal)
            .ToList();

        Assert.True(MGUI.Editor.Document.XamlDocumentModel.TryParse(prepared, out var model, out var error), error?.Message);

        Assert.True(byOrdinal.Count > 20);

        var ordinal = 0;
        foreach (var element in byOrdinal)
        {
            Assert.True(model.TryGetNodeByOrdinal(ordinal, out var node), $"No document-model node for ordinal {ordinal}");
            Assert.Equal(element.GetType().Name, node.DtoType?.Name);
            Assert.Equal(element.SourcePosition.Value.LineNumber, node.StartTagRange.StartIndex >= 0 ? IndexToLine(prepared, node.StartTagRange.StartIndex + 1) : -1);
            ordinal++;
        }

        Assert.False(model.TryGetNodeByOrdinal(ordinal, out _));
    }

    private static int IndexToLine(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static int IndexToColumn(string text, int index)
    {
        var lastNewline = -1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lastNewline = i;
            }
        }

        return index - lastNewline;
    }

    [Fact]
    public void StampSourcePositions_MismatchedLengths_StampsNothingAndReturnsFalse()
    {
        var a = new XamlElement[] { new TextBlock(), new TextBlock() };
        var positions = new List<(int, int)> { (1, 1) };

        var result = XAMLParser.StampSourcePositions("s.xaml", positions, a);

        Assert.False(result);
        Assert.All(a, e => Assert.False(e.SourcePosition.HasValue));
    }

    [Fact]
    public void StampSourcePositions_EqualLengths_StampsEveryInstance()
    {
        var a = new XamlElement[] { new TextBlock(), new TextBlock() };
        var positions = new List<(int, int)> { (1, 1), (2, 5) };

        var result = XAMLParser.StampSourcePositions("s.xaml", positions, a);

        Assert.True(result);
        Assert.Equal(0, a[0].SourcePosition.Value.Ordinal);
        Assert.Equal(1, a[1].SourcePosition.Value.Ordinal);
        Assert.Equal(2, a[1].SourcePosition.Value.LineNumber);
        Assert.Equal(5, a[1].SourcePosition.Value.LinePosition);
    }

    [Fact]
    public void CreatedMGElement_CarriesPosition_RootWindowIncluded()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(NestedFixture, "fixture.xaml"), XamlLoaderMode.Strict, false, true);

        Assert.True(UIToolingService.TryGetXamlSourcePosition(window, out var windowPosition));
        Assert.Equal(0, windowPosition.Ordinal);

        MGElement button = window.GetElementByName<MGElement>("MyButton");
        Assert.True(UIToolingService.TryGetXamlSourcePosition(button, out var buttonPosition));
        Assert.Equal(2, buttonPosition.Ordinal);
    }

    [Fact]
    public void ControlTemplatePart_HasNoPosition_WhileItsDeclaredOwnerDoes()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        MGDesktop desktop = new(runtime);
        string xaml = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">" +
                      "<Button Name=\"OpenButton\" Content=\"Open\">" +
                      "<Button.ContextMenu>" +
                      "<ContextMenu Name=\"SampleMenu\" ControlTemplate=\"ContextMenu.Default\">" +
                      "<ContextMenuButton Content=\"Item\" />" +
                      "</ContextMenu>" +
                      "</Button.ContextMenu>" +
                      "</Button>" +
                      "</Window>";
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), false, true);

        MGElement menu = window.GetElementByName<MGElement>("SampleMenu");
        Assert.True(UIToolingService.TryGetXamlSourcePosition(menu, out _));
        Assert.NotEmpty(menu.TemplateParts);

        foreach (var part in menu.TemplateParts.Values)
        {
            Assert.False(UIToolingService.TryGetXamlSourcePosition(part, out _), $"Template part '{part.GetType().Name}' unexpectedly carries a source position.");
        }
    }

    [Fact]
    public void StringContent_ConvertedByTypeConverter_HasNoPosition()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 300));
        MGDesktop desktop = new(runtime);
        string xaml = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">" +
                      "<Button Name=\"B\" Content=\"OK\" /></Window>";
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), false, true);

        MGButton button = window.GetElementByName<MGButton>("B");
        MGTextBlock generated = button.TraverseVisualTree(false, false, false, false)
            .OfType<MGTextBlock>()
            .FirstOrDefault();

        Assert.NotNull(generated);
        Assert.False(UIToolingService.TryGetXamlSourcePosition(generated, out _));
    }

    [Fact]
    public void ItemTemplate_Clones_KeepTheirOwnTemplateNodeOrdinal_ForEveryGeneratedItem()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 300));
        MGDesktop desktop = new(runtime);
        string xaml = "<Window xmlns=\"" + Ns + "\" xmlns:x=\"" + NsX + "\" xmlns:System=\"" + NsSystem + "\" Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\">" +
                      "<ListBox Name=\"MyListBox\" ItemType=\"{x:Type System:String}\">" +
                      "<ListBox.ItemTemplate>" +
                      "<ContentTemplate>" +
                      "<StackPanel Orientation=\"Horizontal\">" +
                      "<TextBlock Name=\"ItemLabel\" Text=\"Item\" />" +
                      "</StackPanel>" +
                      "</ContentTemplate>" +
                      "</ListBox.ItemTemplate>" +
                      "<System:String>Option 1</System:String>" +
                      "<System:String>Option 2</System:String>" +
                      "<System:String>Option 3</System:String>" +
                      "</ListBox>" +
                      "</Window>";

        //  Compatibility mode: Strict's ValidateKnownElementNames only recognizes MGUI's own element names, not an arbitrary CLR type
        //  reached through another xmlns prefix such as "System:String" (same reason the probe's own ComboBox/ListBox cases use
        //  System.Xaml.XamlServices.Parse directly rather than the strict loader).
        Window definition = XAMLParser.ParseDefinition<Window>(XamlDocumentSource.FromString(xaml), null, XamlLoaderMode.Compatibility, false, true);
        var listBoxDefinition = FindListBoxDefinition(definition);
        Assert.NotNull(listBoxDefinition);
        int templateRootOrdinal = listBoxDefinition.ItemTemplate.Content.SourcePosition.Value.Ordinal;
        int templateLabelOrdinal = FindByName(listBoxDefinition.ItemTemplate.Content, "ItemLabel").SourcePosition.Value.Ordinal;

        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml, "list.xaml"), XamlLoaderMode.Compatibility, false, true);
        desktop.Update();
        desktop.Update();

        MGListBox<string> listBox = window.GetElementByName<MGListBox<string>>("MyListBox");
        Assert.Equal(3, listBox.ListBoxItems.Count);

        foreach (var item in listBox.ListBoxItems)
        {
            Assert.True(UIToolingService.TryGetXamlSourcePosition(item.Content, out var rootPosition));
            Assert.Equal(templateRootOrdinal, rootPosition.Ordinal);

            MGTextBlock label = item.Content.TraverseVisualTree(false, false, false, false).OfType<MGTextBlock>().First();
            Assert.True(UIToolingService.TryGetXamlSourcePosition(label, out var labelPosition));
            Assert.Equal(templateLabelOrdinal, labelPosition.Ordinal);
        }
    }

    private static MGUI.Core.UI.XAML.ListBox FindListBoxDefinition(XamlElement root)
    {
        HashSet<XamlElement> visited = new(ReferenceEqualityComparer.Instance);
        CollectElements(root, visited);
        return visited.OfType<MGUI.Core.UI.XAML.ListBox>().FirstOrDefault();
    }

    [Fact]
    public void GraphEqualsXamlServicesParse_OnThreeDocuments_IncludingCheckBoxSample()
    {
        AssertGraphEquals(NestedFixture);
        AssertGraphEquals("<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"1\" Height=\"1\"><TextBlock Text=\"Hi\" /></Window>");

        var samplePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MGUI.Samples", "Controls", "CheckBox.xaml"));
        AssertGraphEquals(File.ReadAllText(samplePath));
    }

    private static void AssertGraphEquals(string rawMarkup)
    {
        var prepared = rawMarkup.Replace(@"\n", "&#x0a;");
        var baseline = System.Xaml.XamlServices.Parse(prepared);
        var manual = XAMLParser.ParseWindowDefinition(XamlDocumentSource.FromString(rawMarkup), null, XamlLoaderMode.Compatibility, false, true);

        Assert.Equal(Dump(baseline), Dump(manual));
    }

    private static bool IsXamlDtoType(Type t) => t.Namespace != null && t.Namespace.StartsWith("MGUI.Core.UI.XAML", StringComparison.Ordinal);

    private static string Dump(object value)
    {
        System.Text.StringBuilder sb = new();
        DumpValue(value, sb, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return sb.ToString();
    }

    private static void DumpValue(object value, System.Text.StringBuilder sb, int depth, HashSet<object> visited)
    {
        const int maxDepth = 60;
        if (value == null)
        {
            sb.Append("null\n");
            return;
        }

        if (depth > maxDepth)
        {
            sb.Append("<max depth>\n");
            return;
        }

        var t = value.GetType();
        if (t.IsPrimitive || value is string || t.IsEnum || value is decimal)
        {
            sb.Append(value is string s ? $"\"{s}\"" : value).Append('\n');
            return;
        }

        if (!t.IsValueType && !visited.Add(value))
        {
            sb.Append($"<cycle {t.Name}>\n");
            return;
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            sb.Append($"[{t.Name}]\n");
            foreach (var item in enumerable)
            {
                DumpValue(item, sb, depth + 1, visited);
            }

            return;
        }

        sb.Append($"{t.Name} {{\n");
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0 && p.CanRead).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            //  X1's own SourcePosition is deliberately excluded: XamlServices.Parse (the baseline) never sets it, so comparing it
            //  would always fail and would not be testing anything about the DTO graph itself.
            if (p.Name == nameof(XamlElement.SourcePosition))
            {
                continue;
            }

            object pv;
            try
            {
                pv = p.GetValue(value);
            }
            catch (Exception ex)
            {
                sb.Append($"  {p.Name} = <threw {ex.GetType().Name}>\n");
                continue;
            }

            if (pv == null)
            {
                sb.Append($"  {p.Name} = null\n");
                continue;
            }

            var relevant = IsXamlDtoType(pv.GetType()) || pv.GetType().IsEnum || pv.GetType().IsPrimitive || pv is string || pv is decimal
                           || (pv is System.Collections.IEnumerable && pv is not string);
            if (!relevant)
            {
                sb.Append($"  {p.Name} : <not traversed>\n");
                continue;
            }

            sb.Append($"  {p.Name} =\n");
            DumpValue(pv, sb, depth + 1, visited);
        }

        sb.Append("}\n");
    }

    [Fact]
    public void SourcePosition_CannotBeSetFromMarkup()
    {
        //  "SourcePosition" resolves to Element.SourcePosition, whose setter is internal: the XAML member locator finds no writable
        //  public member with that name, so the loader raises the usual "unknown member" failure the same way it would for any other
        //  unknown attribute -- exactly like any other read-only property would.
        string xaml = "<Window xmlns=\"" + Ns + "\" Left=\"0\" Top=\"0\" Width=\"1\" Height=\"1\">" +
                      "<TextBlock SourcePosition=\"x\" Text=\"Hi\" /></Window>";

        Assert.ThrowsAny<Exception>(() => XAMLParser.ParseWindowDefinition(XamlDocumentSource.FromString(xaml), null, XamlLoaderMode.Compatibility, false, true));
    }
}
