using MGUI.Editor;
using Xunit;

namespace MGUI.Tests.Editor;

public class XamlEditorSessionTests
{
    [Fact]
    public void Defaults_AreEmptyTextNullDataContextAndUniqueSourceName()
    {
        XamlEditorSession first = new();
        XamlEditorSession second = new();

        Assert.Equal(string.Empty, first.Text);
        Assert.Null(first.DesignDataContext);
        Assert.False(string.IsNullOrWhiteSpace(first.SourceName));
        Assert.NotEqual(first.SourceName, second.SourceName);
    }

    [Fact]
    public void Text_NullAssignment_BecomesEmptyString()
    {
        XamlEditorSession session = new() { Text = "abc" };

        session.Text = null;

        Assert.Equal(string.Empty, session.Text);
    }

    [Fact]
    public void TextChanged_IsRaisedOnce_OnActualChange_AndNotOnEqualAssignment()
    {
        XamlEditorSession session = new();
        int raiseCount = 0;
        session.TextChanged += (_, _) => raiseCount++;

        session.Text = "hello";
        Assert.Equal(1, raiseCount);

        session.Text = "hello";
        Assert.Equal(1, raiseCount);

        session.Text = "world";
        Assert.Equal(2, raiseCount);
    }

    [Fact]
    public void SourceNameChanged_IsRaisedOnce_OnActualChange_AndNotOnEqualAssignment()
    {
        XamlEditorSession session = new();
        int raiseCount = 0;
        session.SourceNameChanged += (_, _) => raiseCount++;

        string newName = "MyDocument.xaml";
        session.SourceName = newName;
        Assert.Equal(1, raiseCount);

        session.SourceName = newName;
        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public void DesignDataContextChanged_IsRaisedOnce_OnActualChange_AndNotOnEqualAssignment()
    {
        XamlEditorSession session = new();
        int raiseCount = 0;
        session.DesignDataContextChanged += (_, _) => raiseCount++;

        object context = new();
        session.DesignDataContext = context;
        Assert.Equal(1, raiseCount);

        session.DesignDataContext = context;
        Assert.Equal(1, raiseCount);

        session.DesignDataContext = null;
        Assert.Equal(2, raiseCount);

        session.DesignDataContext = null;
        Assert.Equal(2, raiseCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SourceName_RejectsNullOrWhitespace(string invalidName)
    {
        XamlEditorSession session = new();

        Assert.Throws<ArgumentException>(() => session.SourceName = invalidName);
    }
}
