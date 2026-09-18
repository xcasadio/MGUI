using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Xaml;

/// <summary>ADR-0013: a name identifies at most one element of a window, and the window's index says so with an
/// <see cref="MGDuplicateElementNameException"/> that tells a declaration a template cloned from a name declared twice.<para/>
/// The collision is raised by <c>MGContentHost.InvokeContentAdded</c>, when a subtree is attached into a content host and its
/// traversal announces every element it holds. That traversal passes <c>IncludeSelf: false</c>, which skips the attached
/// element's own components -- and <c>MGListBox</c> keeps its items structure in its components. So the generated items of a
/// list box are announced when the list box is <em>wrapped</em> by the attached element (a <c>Window</c> root, a
/// <c>StackPanel</c> root) and are not announced when the list box <em>is</em> that element. Both behaviours are pinned below,
/// because they are what makes the same markup load or fail depending only on its root.</summary>
public class DuplicateElementNameTests
{
    /// <summary>The author's exact reproduction of 2026-09-18.</summary>
    private const string WindowRootMarkup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="20" Top="20" Width="400" Height="380">
            <ListBox Name="Options" Width="220" Height="120">
                <ListBox.ItemTemplate>
                    <ContentTemplate>
                        <TextBlock Name="ItemLabel" Text="Item" />
                    </ContentTemplate>
                </ListBox.ItemTemplate>
                <TextBlock Text="Option 1" />
                <TextBlock Text="Option 2" />
                <TextBlock Text="Option 3" />
            </ListBox>
        </Window>
        """;

    /// <summary>The same markup whose root <em>is</em> the templated control.</summary>
    private const string ListBoxRootMarkup = """
        <ListBox xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Name="Options" Width="220" Height="120">
            <ListBox.ItemTemplate>
                <ContentTemplate>
                    <TextBlock Name="ItemLabel" Text="Item" />
                </ContentTemplate>
            </ListBox.ItemTemplate>
            <TextBlock Text="Option 1" />
            <TextBlock Text="Option 2" />
            <TextBlock Text="Option 3" />
        </ListBox>
        """;

    /// <summary>The same list box, wrapped in a panel: the root is not what matters, the wrapping is.</summary>
    private const string WrappedListBoxMarkup = """
        <StackPanel xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Orientation="Vertical">
            <ListBox Name="Options" Width="220" Height="120">
                <ListBox.ItemTemplate>
                    <ContentTemplate>
                        <TextBlock Name="ItemLabel" Text="Item" />
                    </ContentTemplate>
                </ListBox.ItemTemplate>
                <TextBlock Text="Option 1" />
                <TextBlock Text="Option 2" />
                <TextBlock Text="Option 3" />
            </ListBox>
        </StackPanel>
        """;

    private const string TwoDeclarationsMarkup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="300">
            <StackPanel Orientation="Vertical">
                <Button Name="Same" Content="First" />
                <Button Name="Same" Content="Second" />
            </StackPanel>
        </Window>
        """;

    // -- harness --------------------------------------------------------------

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Host, MGContentPresenter Presenter) CreateHost()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow host = new(desktop, 0, 0, 800, 600);
        desktop.Windows.Add(host);
        MGContentPresenter presenter = new(host);
        host.SetContent(presenter);
        return (runtime, desktop, host, presenter);
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex)
    {
        MouseState mouse = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    /// <summary>Loads <paramref name="markup"/> the way a preview host does, then attaches it into a presenter of the host
    /// window and runs a few frames -- the sequence of the XAML editor's re-parse. Returns the exception the attachment raised,
    /// or null.</summary>
    private static Exception LoadAndAttach(string markup, out MGWindow host)
    {
        var (runtime, desktop, hostWindow, presenter) = CreateHost();
        host = hostWindow;

        MGElement root = UIToolingService.LoadPreview(hostWindow,
            XamlDocumentSource.FromString(markup, "repro.xaml"), null, XamlLoaderMode.Strict, false, true);

        MGDesktop capturedDesktop = desktop;
        GraphTestRuntime capturedRuntime = runtime;
        return Record.Exception(() =>
        {
            presenter.SetContent(root);
            for (int i = 1; i <= 4; i++)
            {
                Frame(capturedRuntime, capturedDesktop, i);
            }
        });
    }

    /// <summary>Locates <paramref name="tagOpenMarker"/> in <paramref name="text"/> and returns the 1-based (line, column) of the
    /// first character of the element's name, right after its <c>&lt;</c> -- computed by counting newlines, independently of the
    /// parser under test. Same helper as <c>XamlSourcePositionTests</c>.</summary>
    private static (int Line, int Column) LocateTagPosition(string text, string tagOpenMarker)
    {
        int index = text.IndexOf(tagOpenMarker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Marker not found: {tagOpenMarker}");

        int nameStart = index + 1; // skip '<'
        int line = 1;
        int lastNewline = -1;
        for (int i = 0; i < nameStart; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lastNewline = i;
            }
        }

        return (line, nameStart - lastNewline);
    }

    // -- 1. the author's reproduction -----------------------------------------

    [Fact]
    public void TheAuthorsReproduction_Throws_NamingTheTemplateDeclarationAndItsPosition()
    {
        Exception thrown = LoadAndAttach(WindowRootMarkup, out _);

        MGDuplicateElementNameException duplicate = Assert.IsType<MGDuplicateElementNameException>(thrown);
        Assert.Equal("ItemLabel", duplicate.Name);

        // The position is the template's own declaration -- the one node the author has to change.
        (int Line, int Column) expected = LocateTagPosition(WindowRootMarkup, "<TextBlock Name=\"ItemLabel\"");
        Assert.Equal(expected.Line, duplicate.LineNumber);
        Assert.Equal(expected.Column, duplicate.LinePosition);

        // Both elements are clones of one declared node, so the message says so rather than claiming two declarations.
        Assert.Contains("ItemLabel", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("TextBlock", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("declared once", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("instantiated", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains($"line {expected.Line}, column {expected.Column}", duplicate.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the second element is", duplicate.Message, StringComparison.Ordinal);

        Assert.IsType<MGTextBlock>(duplicate.ExistingElement);
        Assert.IsType<MGTextBlock>(duplicate.NewElement);
        Assert.NotSame(duplicate.ExistingElement, duplicate.NewElement);

        // Mutation guard for the ordinal test that picks the message: the two clones really do share one document node.
        Assert.True(UIToolingService.TryGetXamlSourcePosition(duplicate.ExistingElement, out XamlSourcePosition existing));
        Assert.True(UIToolingService.TryGetXamlSourcePosition(duplicate.NewElement, out XamlSourcePosition added));
        Assert.Equal(existing.Ordinal, added.Ordinal);
        Assert.Equal(existing.SourceName, added.SourceName);
    }

    /// <summary>The asymmetry the author reported, pinned so a change to the traversal cannot make it drift silently: the very
    /// same markup loads when the templated control is the document root.</summary>
    [Fact]
    public void TheSameMarkup_Loads_WhenTheTemplatedControlIsTheRoot()
    {
        Assert.Null(LoadAndAttach(ListBoxRootMarkup, out MGWindow host));

        // Nothing was indexed either: the generated labels were never announced to the window.
        Assert.False(host.TryGetElementByName("ItemLabel", out _));
    }

    /// <summary>The root is not what matters, the wrapping is: a <c>StackPanel</c> root around the same list box fails exactly
    /// like the <c>Window</c> root. Keeps the documented rule honest.</summary>
    [Fact]
    public void AnyWrapperAroundTheTemplatedControl_Fails_LikeTheWindowRoot()
    {
        Exception thrown = LoadAndAttach(WrappedListBoxMarkup, out _);

        MGDuplicateElementNameException duplicate = Assert.IsType<MGDuplicateElementNameException>(thrown);
        Assert.Equal("ItemLabel", duplicate.Name);
        Assert.Contains("declared once", duplicate.Message, StringComparison.Ordinal);
    }

    // -- 2. a name really declared twice --------------------------------------

    [Fact]
    public void TwoDeclarationsOfOneName_Throw_WithTheOtherMessageAndTheSecondPosition()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        Exception thrown = Record.Exception(() => XAMLParser.LoadRootWindow(desktop,
            XamlDocumentSource.FromString(TwoDeclarationsMarkup, "twice.xaml"), XamlLoaderMode.Compatibility, false, true));

        MGDuplicateElementNameException duplicate = Assert.IsType<MGDuplicateElementNameException>(thrown);
        Assert.Equal("Same", duplicate.Name);

        // Two distinct declarations: the reported position is the second one, the one to rename.
        (int Line, int Column) second = LocateTagPosition(TwoDeclarationsMarkup, "<Button Name=\"Same\" Content=\"Second\"");
        Assert.Equal(second.Line, duplicate.LineNumber);
        Assert.Equal(second.Column, duplicate.LinePosition);

        Assert.Contains("the second element is", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains($"declared at line {second.Line}, column {second.Column}", duplicate.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("declared once", duplicate.Message, StringComparison.Ordinal);

        // The two elements come from different document nodes, which is exactly what tells this case from a template's clones.
        Assert.True(UIToolingService.TryGetXamlSourcePosition(duplicate.ExistingElement, out XamlSourcePosition existing));
        Assert.True(UIToolingService.TryGetXamlSourcePosition(duplicate.NewElement, out XamlSourcePosition added));
        Assert.NotEqual(existing.Ordinal, added.Ordinal);
    }

    // -- 3. no XAML at all ----------------------------------------------------

    [Fact]
    public void TwoNamesAssignedFromCode_Throw_WithoutAPosition()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300);
        desktop.Windows.Add(window);

        MGStackPanel panel = new(window, Orientation.Vertical);
        window.SetContent(panel);

        MGTextBlock first = new(window, "First") { Name = "Shared" };
        Assert.True(panel.TryAddChild(first));

        MGTextBlock second = new(window, "Second");
        Assert.True(panel.TryAddChild(second));

        // The rename path (Element_NameChanged), not the add path: the element is already in the tree when it takes the name.
        MGDuplicateElementNameException duplicate =
            Assert.Throws<MGDuplicateElementNameException>(() => second.Name = "Shared");

        Assert.Equal("Shared", duplicate.Name);
        Assert.Same(first, duplicate.ExistingElement);
        Assert.Same(second, duplicate.NewElement);
        Assert.Equal(0, duplicate.LineNumber);
        Assert.Equal(0, duplicate.LinePosition);
        Assert.Contains("the second element is a MGTextBlock.", duplicate.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("declared at line", duplicate.Message, StringComparison.Ordinal);
    }

    // -- 4. two controls sharing one XAML control template (plan item P10) ----

    /// <summary>What happens when two controls share a XAML <c>ControlTemplate</c> whose part carries a <c>Name</c>:
    /// <c>ControlTemplateLoader</c> instantiates the declared root once per control, so both parts take the same name. The
    /// result is recorded here rather than assumed; ADR-0013 does not change it.</summary>
    [Fact]
    public void TwoControlsSharingAXamlControlTemplate_WithANamedPart()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        desktop.Resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString("""
            <ControlTemplate xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                             Name="Test.NamedPart" TargetType="Expander">
              <Border Name="SharedPartRoot" />
              <ControlTemplateDefinition.Parts>
                <TemplatePart Name="PART_Border" ElementName="SharedPartRoot" IsRequired="False" />
              </ControlTemplateDefinition.Parts>
            </ControlTemplate>
            """));

        string markup = """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="300">
                <StackPanel Orientation="Vertical">
                    <Expander Name="First" ControlTemplate="Test.NamedPart" />
                    <Expander Name="Second" ControlTemplate="Test.NamedPart" />
                </StackPanel>
            </Window>
            """;

        MGWindow window = null;
        Exception thrown = Record.Exception(() =>
        {
            window = XAMLParser.LoadRootWindow(desktop,
                XamlDocumentSource.FromString(markup, "parts.xaml"), XamlLoaderMode.Compatibility, false, true);
            desktop.Windows.Add(window);
            for (int i = 1; i <= 4; i++)
            {
                Frame(runtime, desktop, i);
            }
        });

        Assert.Null(thrown);

        //  Not a vacuous pass: the template really was applied twice, and both instantiated parts really do carry the one
        //  declared name. They simply never reach the window's index, because a part is a component of the element it
        //  templates and the attach-time traversal skips the components of the element being attached.
        //  Not a vacuous pass: both controls really did ask for that template, and each really does hold its own instantiated
        //  parts. What does not happen is the name reaching the window: a control template's declared part names are kept in
        //  MGElement.TemplateParts, a per-element table, and are not applied as MGElement.Name, so two controls sharing one
        //  template never collide. ADR-0013 does not change this.
        MGElement first = window.GetElementByName<MGElement>("First");
        MGElement second = window.GetElementByName<MGElement>("Second");
        Assert.Equal("Test.NamedPart", first.ControlTemplateName);
        Assert.Equal("Test.NamedPart", second.ControlTemplateName);
        Assert.True(first.TryGetTemplatePart("PART_Border", out MGElement firstPart));
        Assert.True(second.TryGetTemplatePart("PART_Border", out MGElement secondPart));
        Assert.NotSame(firstPart, secondPart);
        Assert.Empty(window.TraverseVisualTree(true, true, true, true).Where(x => x.Name == "SharedPartRoot"));
        Assert.False(window.TryGetElementByName("SharedPartRoot", out _));
    }
}
