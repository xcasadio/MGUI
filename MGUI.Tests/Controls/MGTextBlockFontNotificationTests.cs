using MGUI.Core.UI;
using MGUI.Tests.Graph;
using System.Collections.Generic;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Controls;

/// <summary>
/// <see cref="MGTextBlock.TrySetFont(string, int)"/> raises PropertyChanged for <see cref="MGTextBlock.FontFamily"/> and <see cref="MGTextBlock.FontSize"/>
/// exactly when that value changes. The previous values used to be captured from the method parameters, which shadow the properties, so each new value
/// was compared with itself and neither notification was ever raised.
/// </summary>
public class MGTextBlockFontNotificationTests
{
    [Fact]
    public void TrySetFontSize_RaisesFontSize_OnlyWhenTheSizeChanges()
    {
        MGTextBlock textBlock = CreateTextBlock();
        int newSize = textBlock.FontSize + 4;
        List<string> notifications = RecordFontNotifications(textBlock);

        Assert.True(textBlock.TrySetFontSize(newSize));
        Assert.Equal(newSize, textBlock.FontSize);
        Assert.Equal(new[] { nameof(MGTextBlock.FontSize) }, notifications);

        notifications.Clear();
        Assert.True(textBlock.TrySetFontSize(newSize));
        Assert.Empty(notifications);
    }

    [Fact]
    public void TrySetFont_RaisesFontFamilyAndFontSize_OnlyForTheValuesThatChange()
    {
        MGTextBlock textBlock = CreateTextBlock();
        int initialSize = textBlock.FontSize;
        Assert.NotEqual("TestSerif", textBlock.FontFamily);
        List<string> notifications = RecordFontNotifications(textBlock);

        Assert.True(textBlock.TrySetFont("TestSerif", initialSize));
        Assert.Equal("TestSerif", textBlock.FontFamily);
        Assert.Equal(new[] { nameof(MGTextBlock.FontFamily) }, notifications);

        notifications.Clear();
        Assert.True(textBlock.TrySetFont("TestMono", initialSize + 4));
        Assert.Equal(new[] { nameof(MGTextBlock.FontFamily), nameof(MGTextBlock.FontSize) }, notifications);

        notifications.Clear();
        Assert.True(textBlock.TrySetFont("TestMono", initialSize + 4));
        Assert.Empty(notifications);
    }

    private static MGTextBlock CreateTextBlock()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300);
        return new MGTextBlock(window, "Sample text");
    }

    /// <summary>Records the FontFamily and FontSize notifications raised by <paramref name="textBlock"/>; other property names are ignored.</summary>
    private static List<string> RecordFontNotifications(MGTextBlock textBlock)
    {
        List<string> notifications = new();
        textBlock.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MGTextBlock.FontFamily) or nameof(MGTextBlock.FontSize))
            {
                notifications.Add(e.PropertyName);
            }
        };
        return notifications;
    }
}
