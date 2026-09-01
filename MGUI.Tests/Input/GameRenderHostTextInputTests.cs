using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace MGUI.Tests.Input;

/// <summary>Covers <see cref="GameRenderHost{TObservableGame}.OnTextInput(TextInputEventArgs)"/> in isolation, without instantiating a real
/// <see cref="Game"/> (which would require an actual window/graphics device). The host instance is allocated via
/// <see cref="FormatterServices.GetUninitializedObject(System.Type)"/> (same pattern as <c>ControlTemplateInfrastructureTests</c>),
/// bypassing the constructor entirely; the private text-input-sink field is then set directly via reflection so only the
/// TextInput -&gt; <see cref="IKeyboardTextInputSink"/> forwarding logic under test runs.</summary>
public class GameRenderHostTextInputTests
{
    private sealed class FakeObservableGame : Game, IObservableUpdate
    {
        public event System.EventHandler<System.TimeSpan> PreviewUpdate { add { } remove { } }
        public event System.EventHandler<System.EventArgs> EndUpdate { add { } remove { } }
    }

    private sealed class FakeTextInputSink : IKeyboardTextInputSink
    {
        public List<(char Character, Keys Key)> Queued { get; } = new();
        public void QueueTextInput(char character, Keys key) => Queued.Add((character, key));
    }

#pragma warning disable SYSLIB0050
    private static GameRenderHost<FakeObservableGame> CreateUninitializedHost()
        => (GameRenderHost<FakeObservableGame>)FormatterServices.GetUninitializedObject(typeof(GameRenderHost<FakeObservableGame>));
#pragma warning restore SYSLIB0050

    private static void SetSink(GameRenderHost<FakeObservableGame> host, IKeyboardTextInputSink sink)
        => typeof(GameRenderHost<FakeObservableGame>)
            .GetField("_textInputSink", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(host, sink);

    [Fact]
    public void OnTextInput_ForwardsCharacterAndKey_ToAttachedSink()
    {
        GameRenderHost<FakeObservableGame> host = CreateUninitializedHost();
        FakeTextInputSink sink = new();
        SetSink(host, sink);

        host.OnTextInput(new TextInputEventArgs('a', Keys.A));

        (char Character, Keys Key) queued = Assert.Single(sink.Queued);
        Assert.Equal('a', queued.Character);
        Assert.Equal(Keys.A, queued.Key);
    }

    [Fact]
    public void OnTextInput_ForwardsEachEventInOrder_ForMultipleCharacters()
    {
        GameRenderHost<FakeObservableGame> host = CreateUninitializedHost();
        FakeTextInputSink sink = new();
        SetSink(host, sink);

        host.OnTextInput(new TextInputEventArgs('a', Keys.Q));
        host.OnTextInput(new TextInputEventArgs('b', Keys.W));

        Assert.Equal(new[] { ('a', Keys.Q), ('b', Keys.W) }, sink.Queued);
    }

    [Fact]
    public void OnTextInput_DoesNotThrow_WhenNoSinkIsAttached()
    {
        GameRenderHost<FakeObservableGame> host = CreateUninitializedHost();

        Exception exception = Record.Exception(() => host.OnTextInput(new TextInputEventArgs('a', Keys.A)));

        Assert.Null(exception);
    }

    [Fact]
    public void GameRenderHost_ImplementsTextInputHostOptInInterface()
    {
        Assert.Contains(typeof(ITextInputHost), typeof(GameRenderHost<>).GetInterfaces());
    }

    [Fact]
    public void GameRenderHost_Dispose_DetachesTextInputSink()
    {
        // Dispose() touches Game.PreviewUpdate/EndUpdate/Window, which requires a real Game instance;
        // pin the wiring at the source level instead of instantiating one (consistent with the other
        // architecture tests in this suite that assert on method bodies via source text).
        string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "MGUI.MonoGame.Integration", "Rendering", "RenderHost.cs"));

        int disposeIndex = source.IndexOf("public void Dispose()");
        Assert.True(disposeIndex >= 0, "Dispose() method not found in RenderHost.cs");

        string disposeBody = source.Substring(disposeIndex);
        Assert.Contains("DetachTextInputSink();", disposeBody);
    }
}
