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

    [Fact]
    public void Update_UsesNativeCharacterThatDiffersFromUsQwertyFallback()
    {
        // Simulates an AZERTY layout: the physical "Q" key produces the character 'a'.
        InputTracker input = new();

        input.Keyboard.QueueTextInput('a', Keys.Q);
        input.Update(CreateUpdateArgs(Keys.Q));

        Assert.Equal("a", input.Keyboard.CurrentKeyPressedEvents[Keys.Q].PrintableValue);
    }

    [Fact]
    public void Update_MatchesEachOfTwoSimultaneousPrintableKeysToItsOwnCharacter()
    {
        InputTracker input = new();

        input.Keyboard.QueueTextInput('a', Keys.Q);
        input.Keyboard.QueueTextInput('b', Keys.W);
        input.Update(CreateUpdateArgs(Keys.Q, Keys.W));

        Assert.Equal("a", input.Keyboard.CurrentKeyPressedEvents[Keys.Q].PrintableValue);
        Assert.Equal("b", input.Keyboard.CurrentKeyPressedEvents[Keys.W].PrintableValue);
    }

    [Fact]
    public void Update_DoesNotReCreatePressedEvent_WhenNativeTextInputRepeatsWhileKeyStaysHeld()
    {
        // GameWindow.TextInput re-fires (OS key-repeat) while a key is held down. The tracker only consumes
        // native text input for keys that were just pressed (see TryConsumeNativeTextInputString), so a repeated
        // character queued while the key is still down must not be turned into a new synthetic press.
        InputTracker input = new();

        input.Keyboard.QueueTextInput('a', Keys.Q);
        input.Update(CreateUpdateArgs(Keys.Q));
        Assert.Equal("a", input.Keyboard.CurrentKeyPressedEvents[Keys.Q].PrintableValue);

        //  OS repeat: another native TextInput event for the same still-held key.
        input.Keyboard.QueueTextInput('a', Keys.Q);
        input.Update(CreateUpdateArgs(Keys.Q));
        Assert.Null(input.Keyboard.CurrentKeyPressedEvents[Keys.Q]);
    }

    [Fact]
    public void Update_DiscardsRepeatedNativeTextInput_WithoutLeakingIntoLaterKeyPresses()
    {
        InputTracker input = new();

        input.Keyboard.QueueTextInput('a', Keys.Q);
        input.Update(CreateUpdateArgs(Keys.Q));

        //  Repeat fires while Q is still held; the repeated character is queued but never consumed.
        input.Keyboard.QueueTextInput('a', Keys.Q);
        input.Update(CreateUpdateArgs(Keys.Q));

        //  Q is released, then a different key is pressed on a later tick without any queued text input.
        //  It must resolve via the US-QWERTY fallback, not via the discarded repeat character from Q.
        input.Update(CreateUpdateArgs());
        input.Update(CreateUpdateArgs(Keys.D2));

        Assert.Equal("2", input.Keyboard.CurrentKeyPressedEvents[Keys.D2].PrintableValue);
    }

    [Fact]
    public void Update_NormalizesNativeTabCharacterToFrameworkTabValue()
    {
        // GameWindow.TextInput reports the Tab key as a raw '\t'. MGTextBox assumes a Tab inserts TabValue.Length characters
        // (4 spaces) and moves the caret by that amount, so the raw 1-character '\t' must be normalized to the key-map value.
        InputTracker input = new();

        input.Keyboard.QueueTextInput('\t', Keys.Tab);
        input.Update(CreateUpdateArgs(Keys.Tab));

        string printableValue = input.Keyboard.CurrentKeyPressedEvents[Keys.Tab].PrintableValue;
        Assert.Equal(input.Keyboard.KeyToTextInputString(Keys.Tab), printableValue);
        Assert.Equal("    ", printableValue);
    }

    [Fact]
    public void Update_NormalizesNativeCarriageReturnToFrameworkEnterValue()
    {
        InputTracker input = new();

        input.Keyboard.QueueTextInput('\r', Keys.Enter);
        input.Update(CreateUpdateArgs(Keys.Enter));

        string printableValue = input.Keyboard.CurrentKeyPressedEvents[Keys.Enter].PrintableValue;
        Assert.Equal(input.Keyboard.KeyToTextInputString(Keys.Enter), printableValue);
        Assert.Equal("\n", printableValue);
    }

    [Fact]
    public void Update_NormalizesNativeLineFeedToFrameworkEnterValue()
    {
        InputTracker input = new();

        input.Keyboard.QueueTextInput('\n', Keys.Enter);
        input.Update(CreateUpdateArgs(Keys.Enter));

        Assert.Equal("\n", input.Keyboard.CurrentKeyPressedEvents[Keys.Enter].PrintableValue);
    }

    [Fact]
    public void Update_NormalizesTextInputOnlyTabWithoutPhysicalKeyState()
    {
        // Same normalization on the synthesized (no physical key down) path.
        InputTracker input = new();

        input.Keyboard.QueueTextInput('\t', Keys.Tab);
        input.Update(CreateUpdateArgs());

        Assert.Equal("    ", input.Keyboard.CurrentKeyPressedEvents[Keys.Tab].PrintableValue);
    }

    private static UpdateBaseArgs CreateUpdateArgs(params Keys[] pressedKeys)
        => new(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(16), new MouseState(), new KeyboardState(pressedKeys));
}