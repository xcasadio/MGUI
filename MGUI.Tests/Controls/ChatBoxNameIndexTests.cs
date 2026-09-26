using MGUI.Core.UI;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Controls;

/// <summary>ADR-0015 decision 6: <see cref="MGChatBox"/> and <see cref="MGChatBoxMessage"/> derive from <c>MGContentHost</c>,
/// so their components -- including the messages list nested below them -- relay their nested content-host events to the
/// window's name index once the chat box is attached.<para/>
/// Mutation proof (T1.3 step 6): reverting <see cref="MGChatBox"/> to derive from <c>MGElement</c> turns the "message added
/// after attach is indexed" assertion red.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class ChatBoxNameIndexTests
{
    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window) CreateHost()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 800, 600);
        desktop.Windows.Add(window);
        return (runtime, desktop, window);
    }

    /// <summary>A component named before the chat box is attached is indexed once attached: the own-component announcement
    /// of ADR-0015 decision 5 already covered this case (it does not require the chat box to be a content host), pinned here
    /// so the new base class does not regress it.</summary>
    [Fact]
    public void AChatBox_ComponentNamedBeforeAttach_IsIndexedAfterAttach()
    {
        var (runtime, desktop, window) = CreateHost();

        MGChatBox chatBox = new(window);
        chatBox.InputTextBox.Name = "Input";
        Assert.False(window.TryGetElementByName("Input", out _));

        window.SetContent(chatBox);
        Assert.True(window.TryGetElementByName("Input", out MGElement resolved));
        Assert.Same(chatBox.InputTextBox, resolved);
    }

    /// <summary>A message sent after the chat box is attached is indexed once one of its text blocks is named, and leaves the
    /// index once the message is removed. This requires <see cref="MGChatBox"/> to relay the nested content-host events of its
    /// <see cref="MGChatBox.MessagesContainer"/> (an <see cref="MGListBox{TItemType}"/>, itself a content host since T1.3):
    /// before that, a message added after attach never reached the window.</summary>
    [Fact]
    public void AChatBox_MessageAddedAfterAttach_IsIndexed_AndUnindexedOnRemoval()
    {
        var (runtime, desktop, window) = CreateHost();

        MGChatBox chatBox = new(window);
        window.SetContent(chatBox);

        chatBox.SendMessage("hello");
        desktop.Update();

        MGChatBoxMessage message = chatBox.TraverseVisualTree(true, true, false, false).OfType<MGChatBoxMessage>().Single();
        Assert.False(window.TryGetElementByName("Msg1", out _));

        message.MessageTextBlock.Name = "Msg1";
        Assert.True(window.TryGetElementByName("Msg1", out MGElement resolved));
        Assert.Same(message.MessageTextBlock, resolved);

        chatBox.Messages.RemoveAt(0);
        desktop.Update();
        Assert.False(window.TryGetElementByName("Msg1", out _));
    }
}
