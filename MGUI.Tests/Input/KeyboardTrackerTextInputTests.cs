using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Input;

public class KeyboardTrackerTextInputTests
{
    [Fact]
    public void Update_UsesNativeTextInputCharacterForPressedKey()
    {
        InputTracker input = new();

        input.Keyboard.QueueTextInput('1', Keys.D1);
        input.Update(CreateUpdateArgs(Keys.D1));

        Assert.Equal("1", input.Keyboard.CurrentKeyPressedEvents[Keys.D1].PrintableValue);
    }

    [Fact]
    public void Update_DoesNotLetModifierConsumeNativeTextInputCharacter()
    {
        InputTracker input = new();

        input.Keyboard.QueueTextInput('1', Keys.D1);
        input.Update(CreateUpdateArgs(Keys.LeftShift, Keys.D1));

        Assert.Null(input.Keyboard.CurrentKeyPressedEvents[Keys.LeftShift].PrintableValue);
        Assert.Equal("1", input.Keyboard.CurrentKeyPressedEvents[Keys.D1].PrintableValue);
    }

    [Fact]
    public void Update_FallsBackToKeyboardMappingWithoutNativeTextInput()
    {
        InputTracker input = new();

        input.Update(CreateUpdateArgs(Keys.D1));

        string printableValue = input.Keyboard.CurrentKeyPressedEvents[Keys.D1].PrintableValue;

        Assert.False(string.IsNullOrEmpty(printableValue));
    }

    [Fact]
    public void Update_CreatesPressedEventForTextInputWithoutPhysicalKeyState()
    {
        InputTracker input = new();

        input.Keyboard.QueueTextInput('9', Keys.D9);
        input.Update(CreateUpdateArgs());

        Assert.Equal("9", input.Keyboard.CurrentKeyPressedEvents[Keys.D9].PrintableValue);
        Assert.False(input.Keyboard.IsPressed(Keys.D9));
    }

    private static UpdateBaseArgs CreateUpdateArgs(params Keys[] pressedKeys)
        => new(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(16), new MouseState(), new KeyboardState(pressedKeys));
}