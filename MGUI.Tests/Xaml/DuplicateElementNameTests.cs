using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using System.Collections.ObjectModel;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Xaml;

/// <summary>ADR-0013: a name identifies at most one element of a window, and the window's index says so with an
/// <see cref="MGDuplicateElementNameException"/> that tells a declaration a template cloned from a name declared twice.<para/>
/// The collision is raised by <c>MGContentHost.InvokeContentAdded</c>, when a subtree enters a window's content-host chain and
/// its traversal announces every element it holds -- including, since ADR-0015 decision 5, the attached element's own
/// components. <c>MGListBox</c> keeps its items structure in its components, so the generated items of a list box are
/// announced whether the list box is <em>wrapped</em> by the attached element (a <c>Window</c> root, a <c>StackPanel</c> root)
/// or <em>is</em> that element itself: the wrapping no longer decides anything, pinned below.<para/>
/// Where the collision surfaces depends only on when the tree first enters a live window's chain, never on parsing alone: for
/// a root that is not a <c>Window</c>, that moment is the host's own <c>SetContent</c> once the loaded tree is attached
/// elsewhere; for a <c>Window</c> root, it is the loader itself, because an <see cref="MGWindow"/> indexes its own tree from
/// construction and its own <c>SetContent</c> (called while the loader builds it) is that entry point.</summary>
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
    /// window and runs a few frames -- the sequence of the XAML editor's re-parse. Returns the exception raised by either step,
    /// or null: for a <c>Window</c> root the collision surfaces inside the load itself (ADR-0015 decision 5, the loaded window
    /// indexes its own tree from construction), for any other root it surfaces at the attach, so both are wrapped here.</summary>
    private static Exception LoadAndAttach(string markup, out MGWindow host)
    {
        var (runtime, desktop, hostWindow, presenter) = CreateHost();
        host = hostWindow;

        MGDesktop capturedDesktop = desktop;
        GraphTestRuntime capturedRuntime = runtime;
        return Record.Exception(() =>
        {
            MGElement root = UIToolingService.LoadPreview(hostWindow,
                XamlDocumentSource.FromString(markup, "repro.xaml"), null, XamlLoaderMode.Strict, false, true);
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

    /// <summary>The exception now surfaces from inside <c>LoadAndAttach</c>'s load step, not its attach step: <c>WindowRootMarkup</c>'s
    /// root is a <c>Window</c>, so the collision is raised by that window's own <c>SetContent</c> while the loader still builds
    /// it (ADR-0015 decision 5) -- one call earlier than the later <c>presenter.SetContent</c>. Same exception, same assertions,
    /// wherever it is thrown from.</summary>
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

        // Both elements carry one source position, so the message reports that, names the template as the cause to look for,
        // and says what to change -- rather than claiming two declarations.
        Assert.Contains("ItemLabel", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("TextBlock", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("the same source position", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("template", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains($"line {expected.Line}, column {expected.Column} of 'repro.xaml'", duplicate.Message, StringComparison.Ordinal);
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

    /// <summary>ADR-0015 decision 5: the attach-time traversal now walks the attached element's own components too, so a
    /// <c>ListBox</c> root whose item template carries a <c>Name</c> fails exactly like a wrapped one -- the wrapping never
    /// decided anything, only the traversal's <c>IncludeSelf</c> asymmetry did.</summary>
    [Fact]
    public void TheSameMarkup_Fails_WhenTheTemplatedControlIsTheRoot_LikeAnyWrapper()
    {
        Exception thrown = LoadAndAttach(ListBoxRootMarkup, out _);

        MGDuplicateElementNameException duplicate = Assert.IsType<MGDuplicateElementNameException>(thrown);
        Assert.Equal("ItemLabel", duplicate.Name);
        Assert.Contains("the same source position", duplicate.Message, StringComparison.Ordinal);
    }

    /// <summary>The root is not what matters, the wrapping is: a <c>StackPanel</c> root around the same list box fails exactly
    /// like the <c>Window</c> root. Keeps the documented rule honest.</summary>
    [Fact]
    public void AnyWrapperAroundTheTemplatedControl_Fails_LikeTheWindowRoot()
    {
        Exception thrown = LoadAndAttach(WrappedListBoxMarkup, out _);

        MGDuplicateElementNameException duplicate = Assert.IsType<MGDuplicateElementNameException>(thrown);
        Assert.Equal("ItemLabel", duplicate.Name);
        Assert.Contains("the same source position", duplicate.Message, StringComparison.Ordinal);
    }

    /// <summary>The shared-position message names the template as the usual cause without asserting it, because a position is only
    /// unique within one parse: two documents that share a display name can reach this branch with no template anywhere. The message
    /// must stay true then -- it reports the evidence (one position, two elements) and never claims one declaration.</summary>
    [Fact]
    public void TwoIndependentDocumentsSharingASourceName_GetAMessageThatStaysTrue()
    {
        var (runtime, desktop, host, presenter) = CreateHost();
        MGStackPanel panel = new(host, Orientation.Vertical);
        presenter.SetContent(panel);

        const string OneElement = """
            <TextBlock xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Name="Foo" Text="Hi" />
            """;

        // Two independent parses, same display name: their single elements both land at ordinal 0 of "shared.xaml".
        MGElement first = UIToolingService.LoadPreview(host,
            XamlDocumentSource.FromString(OneElement, "shared.xaml"), null, XamlLoaderMode.Strict, false, true);
        MGElement second = UIToolingService.LoadPreview(host,
            XamlDocumentSource.FromString(OneElement, "shared.xaml"), null, XamlLoaderMode.Strict, false, true);

        Assert.True(panel.TryAddChild(first));
        MGDuplicateElementNameException duplicate =
            Assert.Throws<MGDuplicateElementNameException>(() => panel.TryAddChild(second));

        Assert.True(UIToolingService.TryGetXamlSourcePosition(duplicate.ExistingElement, out XamlSourcePosition a));
        Assert.True(UIToolingService.TryGetXamlSourcePosition(duplicate.NewElement, out XamlSourcePosition b));
        Assert.Equal(a.Ordinal, b.Ordinal);
        Assert.Equal(a.SourceName, b.SourceName);

        // There is no template here at all, so the message must not claim there is one, nor claim a single declaration.
        Assert.Contains("the same source position", duplicate.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("declared once, at line", duplicate.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("is instantiated more than once", duplicate.Message, StringComparison.Ordinal);
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

    // -- 5. what a host gets when it catches the attachment failure (ADR-0013, option A) --

    /// <summary>The failure of the report is raised outside every loader call, so no wrapping inside the loader can classify it.
    /// A host that attaches a loaded tree itself catches it and asks for the same description the loader would have produced.
    /// Uses <c>WrappedListBoxMarkup</c> (a <c>StackPanel</c> root, not a <c>Window</c>): its load genuinely succeeds, since
    /// nothing indexes anything until the tree is attached into a live window's content-host chain -- unlike a <c>Window</c>
    /// root, covered by <see cref="ADocumentFailingInsideTheLoader_GetsTheSameDiagnosticThroughFromException"/> below.</summary>
    [Fact]
    public void AHostThatCatchesTheAttachment_GetsTheSameDiagnosticTheLoaderWouldHaveGiven()
    {
        var (runtime, desktop, host, presenter) = CreateHost();
        XamlDocumentSource source = XamlDocumentSource.FromString(WrappedListBoxMarkup, "repro.xaml");

        // The loader itself succeeds: this is the whole point.
        MGElement root = UIToolingService.LoadPreview(host, source, null, XamlLoaderMode.Strict, false, true);

        Exception thrown = Record.Exception(() => presenter.SetContent(root));
        Assert.IsType<MGDuplicateElementNameException>(thrown);

        XamlLoaderDiagnostic diagnostic = XamlLoaderDiagnostic.FromException(thrown, source, "Preview");

        Assert.Equal(XamlLoaderDiagnosticCode.DuplicateElementName, diagnostic.Code);
        Assert.Equal("repro.xaml", diagnostic.SourceName);
        Assert.Equal("Preview", diagnostic.DocumentKind);

        (int Line, int Column) expected = LocateTagPosition(WrappedListBoxMarkup, "<TextBlock Name=\"ItemLabel\"");
        Assert.Equal(expected.Line, diagnostic.LineNumber);
        Assert.Equal(expected.Column, diagnostic.LinePosition);
        Assert.Contains("ItemLabel", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>The other half: a document whose root is a <c>Window</c> directly wrapping the templated control fails inside
    /// the loader itself (ADR-0015 decision 5, the loaded window indexes its own tree from construction). It still gets the
    /// same diagnostic through <see cref="XamlLoaderDiagnostic.FromException"/>, unwrapped from wherever it was thrown.</summary>
    [Fact]
    public void ADocumentFailingInsideTheLoader_GetsTheSameDiagnosticThroughFromException()
    {
        var (runtime, desktop, host, presenter) = CreateHost();
        XamlDocumentSource source = XamlDocumentSource.FromString(WindowRootMarkup, "repro.xaml");

        Exception thrown = Record.Exception(() =>
            UIToolingService.LoadPreview(host, source, null, XamlLoaderMode.Strict, false, true));
        Assert.IsType<MGDuplicateElementNameException>(thrown);

        XamlLoaderDiagnostic diagnostic = XamlLoaderDiagnostic.FromException(thrown, source, "Preview");

        Assert.Equal(XamlLoaderDiagnosticCode.DuplicateElementName, diagnostic.Code);
        Assert.Equal("repro.xaml", diagnostic.SourceName);
        Assert.Equal("Preview", diagnostic.DocumentKind);

        (int Line, int Column) expected = LocateTagPosition(WindowRootMarkup, "<TextBlock Name=\"ItemLabel\"");
        Assert.Equal(expected.Line, diagnostic.LineNumber);
        Assert.Equal(expected.Column, diagnostic.LinePosition);
        Assert.Contains("ItemLabel", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>A XamlLoaderException is unwrapped, not re-described: its own diagnostic comes back untouched.</summary>
    [Fact]
    public void FromException_OnALoaderException_ReturnsItsOwnDiagnosticUnchanged()
    {
        XamlDocumentSource source = XamlDocumentSource.FromString(TwoDeclarationsMarkup, "twice.xaml");

        XamlLoaderException thrown = Assert.Throws<XamlLoaderException>(() =>
            XAMLParser.ParseElementDefinition(source, null, XamlLoaderMode.Strict, false, true));

        Assert.Same(thrown.Diagnostic, XamlLoaderDiagnostic.FromException(thrown, source, "Preview"));
    }

    // -- 6. a rename the index refuses leaves nothing behind (ADR-0013) ------

    [Fact]
    public void ARefusedRename_LeavesBothElementsAndTheIndexExactlyAsTheyWere()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300);
        desktop.Windows.Add(window);

        MGStackPanel panel = new(window, Orientation.Vertical);
        window.SetContent(panel);

        MGTextBlock a = new(window, "A") { Name = "X" };
        MGTextBlock b = new(window, "B") { Name = "Y" };
        Assert.True(panel.TryAddChild(a));
        Assert.True(panel.TryAddChild(b));

        Assert.Throws<MGDuplicateElementNameException>(() => a.Name = "Y");

        // A keeps the name it had, and the entry that resolves to it; B is untouched.
        Assert.Equal("X", a.Name);
        Assert.True(window.TryGetElementByName("X", out MGElement stillA));
        Assert.Same(a, stillA);
        Assert.True(window.TryGetElementByName("Y", out MGElement stillB));
        Assert.Same(b, stillB);

        // And removing A afterwards does not take B's entry with it.
        Assert.True(panel.TryRemoveChild(a));
        Assert.False(window.TryGetElementByName("X", out _));
        Assert.True(window.TryGetElementByName("Y", out MGElement bAgain));
        Assert.Same(b, bAgain);
    }

    [Fact]
    public void ARenameTheIndexAccepts_FreesTheOldNameAndResolvesTheNewOne()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300);
        desktop.Windows.Add(window);

        MGStackPanel panel = new(window, Orientation.Vertical);
        window.SetContent(panel);

        MGTextBlock a = new(window, "A") { Name = "X" };
        Assert.True(panel.TryAddChild(a));

        a.Name = "Z";

        Assert.False(window.TryGetElementByName("X", out _));
        Assert.True(window.TryGetElementByName("Z", out MGElement renamed));
        Assert.Same(a, renamed);

        // The freed name is available again, and taking it does not disturb the renamed element.
        MGTextBlock c = new(window, "C") { Name = "X" };
        Assert.True(panel.TryAddChild(c));
        Assert.True(window.TryGetElementByName("X", out MGElement taken));
        Assert.Same(c, taken);
        Assert.True(window.TryGetElementByName("Z", out _));
    }

    /// <summary>The other way an element ends up carrying a name the index never gave it: the add that would have indexed it was
    /// refused, and the element stayed in the tree anyway. Removing it must not take the real holder's entry with it.</summary>
    [Fact]
    public void RemovingAnElementWhoseNameTheIndexNeverAccepted_LeavesTheRealHolderIndexed()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300);
        desktop.Windows.Add(window);

        MGStackPanel panel = new(window, Orientation.Vertical);
        window.SetContent(panel);

        MGTextBlock holder = new(window, "holder") { Name = "X" };
        Assert.True(panel.TryAddChild(holder));

        MGTextBlock intruder = new(window, "intruder") { Name = "X" };
        Assert.Throws<MGDuplicateElementNameException>(() => panel.TryAddChild(intruder));

        // The refused add left the intruder in the tree, still carrying the name, but never indexed under it.
        Assert.Contains(intruder, panel.Children);
        Assert.Equal("X", intruder.Name);
        Assert.True(window.TryGetElementByName("X", out MGElement indexed));
        Assert.Same(holder, indexed);

        // Removing it must drop nothing: the entry under "X" is the holder's, not its own.
        Assert.True(panel.TryRemoveChild(intruder));
        Assert.True(window.TryGetElementByName("X", out MGElement stillTheHolder));
        Assert.Same(holder, stillTheHolder);
    }

    // -- 7. components are announced on both sides of the content-host chain (ADR-0015 decisions 4/5, T1.2) --

    /// <summary>A minimal <see cref="MGSingleContentHost"/> that exposes the protected <c>AddComponent</c> / <c>RemoveComponent</c>
    /// of <see cref="MGContentHost"/>, to prove a component added to (or removed from) an already-attached host reaches the
    /// window's name index exactly like the host's regular content does.</summary>
    private sealed class ComponentTestHost : MGSingleContentHost
    {
        public ComponentTestHost(MGWindow window) : base(window, MGElementType.Custom) { }

        private MGComponent<MGBorder> _namedComponent;

        public void AddNamedComponent(MGBorder element)
        {
            _namedComponent = MGComponentBase.Create(element);
            AddComponent(_namedComponent);
        }

        public void RemoveNamedComponent()
        {
            if (_namedComponent != null)
            {
                RemoveComponent(_namedComponent);
                _namedComponent = null;
            }
        }
    }

    /// <summary>ADR-0015 decision 5: attaching an element announces its own components too, not only the components of the
    /// elements nested below it (those were already announced before this decision, since a nested element's own
    /// <c>TraverseVisualTree</c> call always passes <c>IncludeSelf: true</c>).<para/>
    /// Mutation proof (P2 step 7i): reverting <c>InvokeContentAdded</c> / <c>InvokeContentRemoved</c> to
    /// <c>IncludeSelf: false</c> turns this test red while <see cref="ANestedElement_AnnouncesItsOwnComponents_JustLikeBeforeThisDecision"/>
    /// stays green.</summary>
    [Fact]
    public void ADirectlyAttachedElement_AnnouncesItsOwnComponents_IndexedAfterAttachAndUnindexedAfterRemoval()
    {
        var (runtime, desktop, host, presenter) = CreateHost();

        MGListBox<string> listBox = new(host);
        listBox.InnerBorder.Name = "Inner";

        Assert.False(host.TryGetElementByName("Inner", out _));

        presenter.SetContent(listBox);
        Assert.True(host.TryGetElementByName("Inner", out MGElement resolved));
        Assert.Same(listBox.InnerBorder, resolved);

        presenter.SetContent(null);
        Assert.False(host.TryGetElementByName("Inner", out _));
    }

    /// <summary>Same scenario as above, but the named element is nested one level below the attached root: this already worked
    /// before ADR-0015 decision 5, and stays true after it (pinned so the fix cannot regress the case it did not change).</summary>
    [Fact]
    public void ANestedElement_AnnouncesItsOwnComponents_JustLikeBeforeThisDecision()
    {
        var (runtime, desktop, host, presenter) = CreateHost();

        MGListBox<string> listBox = new(host);
        listBox.InnerBorder.Name = "InnerNested";
        MGStackPanel panel = new(host, Orientation.Vertical);
        Assert.True(panel.TryAddChild(listBox));

        Assert.False(host.TryGetElementByName("InnerNested", out _));

        presenter.SetContent(panel);
        Assert.True(host.TryGetElementByName("InnerNested", out MGElement resolved));
        Assert.Same(listBox.InnerBorder, resolved);

        presenter.SetContent(null);
        Assert.False(host.TryGetElementByName("InnerNested", out _));
    }

    /// <summary>ADR-0015 decision 4: a component removed from an already-attached host is unindexed, mirroring the add path;
    /// adding the same instance back does not throw <see cref="MGDuplicateElementNameException"/>, because the removal really
    /// unindexed it.<para/>
    /// Mutation proof (P2 step 7ii): removing the <c>MGContentHost.RemoveComponent</c> override turns the "removed -> not
    /// resolved" assertion red.</summary>
    [Fact]
    public void AComponentAddedToAnAlreadyAttachedHost_IsIndexedThenUnindexedOnRemoval_AndCanBeReaddedWithoutADuplicateNameError()
    {
        var (runtime, desktop, host, presenter) = CreateHost();
        ComponentTestHost testHost = new(host);
        presenter.SetContent(testHost);

        MGBorder namedBorder = new(host) { Name = "Late" };
        Assert.False(host.TryGetElementByName("Late", out _));

        testHost.AddNamedComponent(namedBorder);
        Assert.True(host.TryGetElementByName("Late", out MGElement resolved));
        Assert.Same(namedBorder, resolved);

        testHost.RemoveNamedComponent();
        Assert.False(host.TryGetElementByName("Late", out _));

        testHost.AddNamedComponent(namedBorder);
        Assert.True(host.TryGetElementByName("Late", out MGElement resolvedAgain));
        Assert.Same(namedBorder, resolvedAgain);
    }

    // -- 8. MGListBox becomes a content host (ADR-0015 decision 6, T1.3) ------

    /// <summary>An item added to an already-attached <see cref="MGListBox{TItemType}"/> reaches the window's name index, and
    /// leaves it once removed. Before ADR-0015 decision 6, this failed: a directly attached root list box announces only its
    /// own components at attach time (ADR-0015 decision 5), never the nested events raised by items added afterwards, because
    /// only an <c>MGContentHost</c> relays those.<para/>
    /// Mutation proof (T1.3 step 6): reverting <see cref="MGListBox{TItemType}"/> to derive from <c>MGElement</c> turns the
    /// "resolved after attach" assertion red.</summary>
    [Fact]
    public void AListBox_ItemAddedAfterAttach_IsIndexed_AndUnindexedOnRemoval()
    {
        var (runtime, desktop, host, presenter) = CreateHost();

        MGListBox<string> listBox = new(host)
        {
            ItemTemplate = item => new MGTextBlock(host, item)
        };
        presenter.SetContent(listBox);

        ObservableCollection<string> source = new() { "First" };
        listBox.SetItemsSource(source);
        Assert.False(host.TryGetElementByName("Late", out _));

        listBox.ListBoxItems[0].Content.Name = "Late";
        Assert.True(host.TryGetElementByName("Late", out MGElement resolved));
        Assert.Same(listBox.ListBoxItems[0].Content, resolved);

        source.RemoveAt(0);
        Assert.False(host.TryGetElementByName("Late", out _));
    }

    /// <summary>Same invariant for <see cref="MGListBox{TItemType}.Header"/>: set after attach, it is indexed; replaced, the
    /// previous instance leaves the index and the new one enters it.</summary>
    [Fact]
    public void AListBox_HeaderSetAfterAttach_IsIndexed_AndReplacedHeaderSwapsTheIndexEntry()
    {
        var (runtime, desktop, host, presenter) = CreateHost();

        MGListBox<string> listBox = new(host);
        presenter.SetContent(listBox);

        MGTextBlock firstHeader = new(host, "First") { Name = "Header" };
        listBox.Header = firstHeader;
        Assert.True(host.TryGetElementByName("Header", out MGElement resolved));
        Assert.Same(firstHeader, resolved);

        MGTextBlock secondHeader = new(host, "Second") { Name = "Header" };
        listBox.Header = secondHeader;
        Assert.True(host.TryGetElementByName("Header", out MGElement resolvedAfterReplace));
        Assert.Same(secondHeader, resolvedAfterReplace);
    }

    /// <summary>Same invariant under UI virtualization: a realized item's element is indexed once named, exactly like the
    /// non-virtualized case above.</summary>
    [Fact]
    public void AVirtualizedListBox_RealizedItemNamedAfterAttach_IsIndexed()
    {
        var (runtime, desktop, host, presenter) = CreateHost();

        MGListBox<string> listBox = new(host)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VirtualizationMode = ListBoxVirtualizationMode.Always,
            ItemTemplate = item => new MGTextBlock(host, item)
        };
        presenter.SetContent(listBox);

        ObservableCollection<string> source = new(Enumerable.Range(0, 5).Select(i => $"item {i}"));
        listBox.SetItemsSource(source);
        desktop.Update();
        desktop.Update();

        Assert.False(host.TryGetElementByName("RealizedItem", out _));

        //  InternalItems (and so ListBoxItems) is null in the recycling path (virtualized mode), so the realized
        //  item's content is found the same way an item's text is found elsewhere in this suite: by walking the
        //  visual tree the item template actually produced.
        MGTextBlock realizedItem = listBox.TraverseVisualTree(true, true, false, false).OfType<MGTextBlock>()
            .First(tb => tb.Text.StartsWith("item ", StringComparison.Ordinal));
        realizedItem.Name = "RealizedItem";
        Assert.True(host.TryGetElementByName("RealizedItem", out MGElement resolved));
        Assert.Same(realizedItem, resolved);
    }
}
