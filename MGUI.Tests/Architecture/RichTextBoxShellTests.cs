using MGUI.Core.UI;
using MGUI.Core.UI.XAML;

namespace MGUI.Tests.Architecture;

public class RichTextBoxShellTests
{
    [Fact]
    public void RichTextBoxElementType_IsRegisteredAsInputElement()
    {
        Assert.True(Enum.IsDefined(typeof(MGElementType), MGElementType.RichTextBox));
        Assert.True((int)MGElementType.RichTextBox > (int)MGElementType.TextBox);
        Assert.True((int)MGElementType.RichTextBox < (int)MGElementType.PasswordBox);
    }

    [Fact]
    public void XamlRichTextBox_UsesRichTextBoxElementType()
    {
        RichTextBox xamlElement = new();

        Assert.Equal(MGElementType.RichTextBox, xamlElement.ElementType);
        Assert.IsAssignableFrom<TextBox>(xamlElement);
    }
}