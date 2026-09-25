using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Styling;
using MonoGame.Extended;

namespace MGUI.Core.UI;

/// <summary>
/// A modal question shown over a whole <see cref="MGDesktop"/>: a title, a message and one to <see cref="MaxButtonCount"/>
/// labelled buttons (ADR-0018).<para/>
/// The box is hosted in <see cref="MGDesktop.OverlayHost"/>, which must be modal: while the box is open, nothing else on the
/// desktop receives mouse or keyboard input (floating and nested windows included), and
/// <see cref="MGDesktop.ShouldCaptureGameplayInput"/> is <see langword="true"/>, so a host that honours it stops its own
/// shortcuts too.<para/>
/// Keyboard: Enter (or Space) activates the focused button, which is the default button when the box opens; Tab moves between
/// the buttons; Escape chooses the cancel button. Focus is kept on one of the box's buttons while the box is active.<para/>
/// The chosen button's index is reported exactly once through the callback given to <see cref="Show"/>, after the box has left
/// the overlay host. Opening another box from that callback is supported: the new box becomes the active overlay, and the click
/// or key press that closed the previous box never reaches it.<para/>
/// MGUI keeps no queue of message boxes: a host that may ask several questions at once queues them itself. A box shown while
/// another overlay is open is placed above it.
/// </summary>
public sealed class MGMessageBox
{
    /// <summary>The largest number of buttons a message box can show.</summary>
    public const int MaxButtonCount = 3;

    private const int ContentMinWidth = 280;
    private const int ContentMaxWidth = 520;
    private const int ButtonMinWidth = 80;

    private readonly MGStackPanel _content;
    private readonly MGButton[] _buttons;
    private Action<int> _closed;

    /// <summary>The desktop this box is shown on.</summary>
    public MGDesktop Desktop { get; }

    /// <summary>The title shown at the top of the box.</summary>
    public string Title { get; }

    /// <summary>The message shown under the title, wrapped to the box's width.</summary>
    public string Message { get; }

    /// <summary>The box's buttons, in the order of the labels given to <see cref="Show"/>.</summary>
    public IReadOnlyList<MGButton> Buttons => _buttons;

    /// <summary>The index of the button that has keyboard focus when the box opens.</summary>
    public int DefaultButtonIndex { get; }

    /// <summary>The index reported when the user presses Escape.</summary>
    public int CancelButtonIndex { get; }

    /// <summary><see langword="true"/> until a button is chosen.</summary>
    public bool IsOpen { get; private set; }

    internal MGOverlay Overlay { get; }

    private MGMessageBox(MGDesktop desktop, string title, string message, IReadOnlyList<string> buttonLabels,
        int defaultButtonIndex, int cancelButtonIndex, Action<int> closed)
    {
        Desktop = desktop;
        Title = title ?? string.Empty;
        Message = message ?? string.Empty;
        DefaultButtonIndex = defaultButtonIndex;
        CancelButtonIndex = cancelButtonIndex;
        _closed = closed;

        MGWindow window = desktop.OverlayHost.ParentWindow;

        _content = new MGStackPanel(window, Orientation.Vertical)
        {
            Spacing = 12,
            MinWidth = ContentMinWidth,
            MaxWidth = ContentMaxWidth,
        };
        _content.SetPadding(new Thickness(16, 12, 16, 12), UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

        _content.TryAddChild(new MGTextBlock(window, Title, AllowsInlineFormatting: false) { IsBold = true });
        _content.TryAddChild(new MGTextBlock(window, Message, AllowsInlineFormatting: false) { WrapText = true });

        var buttonRow = new MGStackPanel(window, Orientation.Horizontal)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        _buttons = new MGButton[buttonLabels.Count];
        for (int i = 0; i < _buttons.Length; i++)
        {
            var button = new MessageBoxButton(window, this, i) { MinWidth = ButtonMinWidth };
            button.SetContent(new MGTextBlock(window, buttonLabels[i], AllowsInlineFormatting: false));
            _buttons[i] = button;
            buttonRow.TryAddChild(button);
        }

        _content.TryAddChild(buttonRow);

        Overlay = desktop.OverlayHost.AddOverlay(_content, ShowCloseButton: false);
    }

    /// <summary>Shows a message box over the whole <paramref name="desktop"/> and returns it.</summary>
    /// <param name="desktop">The desktop to cover. Its <see cref="MGDesktop.OverlayHost"/> must be modal.</param>
    /// <param name="title">The title; shown as plain text, never parsed as inline formatting.</param>
    /// <param name="message">The message; shown as plain text and wrapped.</param>
    /// <param name="buttonLabels">One to <see cref="MaxButtonCount"/> labels, shown left to right.</param>
    /// <param name="defaultButtonIndex">The button focused when the box opens, activated by Enter.</param>
    /// <param name="cancelButtonIndex">The index reported when the user presses Escape.</param>
    /// <param name="closed">Called exactly once with the chosen button's index, after the box has left the overlay host.</param>
    /// <exception cref="ArgumentNullException"><paramref name="desktop"/>, <paramref name="buttonLabels"/> or <paramref name="closed"/> is null.</exception>
    /// <exception cref="ArgumentException">There are no labels or more than <see cref="MaxButtonCount"/>, or a label is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultButtonIndex"/> or <paramref name="cancelButtonIndex"/> does not name a button.</exception>
    /// <exception cref="InvalidOperationException">The desktop's overlay host is not modal, so the box would not block the desktop.</exception>
    public static MGMessageBox Show(MGDesktop desktop, string title, string message, IReadOnlyList<string> buttonLabels,
        int defaultButtonIndex, int cancelButtonIndex, Action<int> closed)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(buttonLabels);
        ArgumentNullException.ThrowIfNull(closed);

        if (buttonLabels.Count == 0 || buttonLabels.Count > MaxButtonCount)
        {
            throw new ArgumentException($"A message box shows 1 to {MaxButtonCount} buttons, not {buttonLabels.Count}.", nameof(buttonLabels));
        }

        for (int i = 0; i < buttonLabels.Count; i++)
        {
            if (buttonLabels[i] == null)
            {
                throw new ArgumentException($"Button label {i} is null.", nameof(buttonLabels));
            }
        }

        if (defaultButtonIndex < 0 || defaultButtonIndex >= buttonLabels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultButtonIndex), defaultButtonIndex, "The default button index does not name a button.");
        }

        if (cancelButtonIndex < 0 || cancelButtonIndex >= buttonLabels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(cancelButtonIndex), cancelButtonIndex, "The cancel button index does not name a button.");
        }

        if (!desktop.OverlayHost.IsModal)
        {
            throw new InvalidOperationException("MGMessageBox needs a modal MGDesktop.OverlayHost: a non-modal overlay would let input reach the content underneath.");
        }

        var messageBox = new MGMessageBox(desktop, title, message, buttonLabels, defaultButtonIndex, cancelButtonIndex, closed);
        messageBox.Open();
        return messageBox;
    }

    private void Open()
    {
        MGOverlayHost host = Desktop.OverlayHost;

        //  Ties on ZIndex keep the overlay that was opened first active, so a box shown over another open overlay must rank above it.
        double topZIndex = double.NegativeInfinity;
        IReadOnlyList<MGOverlay> openOverlays = host.OpenOverlays;
        for (int i = 0; i < openOverlays.Count; i++)
        {
            topZIndex = Math.Max(topZIndex, openOverlays[i].ZIndex);
        }

        if (openOverlays.Count > 0)
        {
            Overlay.ZIndex = topZIndex + 1;
        }

        IsOpen = true;
        _content.OnBeginUpdate += KeepKeyboardFocusInside;
        host.TryOpen(Overlay);
        Desktop.QueueFocusedKeyboardHandler(_buttons[DefaultButtonIndex], KeyboardFocusSource.Programmatic);
    }

    /// <summary>Keeps keyboard focus on one of the box's buttons while it is the active overlay, so Enter and Escape always
    /// reach it: a click on the box's text, for instance, would otherwise leave nothing focused.</summary>
    private void KeepKeyboardFocusInside(object sender, MGElement.ElementUpdateEventArgs e)
    {
        if (!IsOpen || Desktop.OverlayHost.ActiveOverlay != Overlay)
        {
            return;
        }

        if (IsOwnButton(Desktop.FocusedKeyboardHandler) || IsOwnButton(Desktop.QueuedFocusedKeyboardHandler))
        {
            return;
        }

        Desktop.QueueFocusedKeyboardHandler(_buttons[DefaultButtonIndex], KeyboardFocusSource.Programmatic);
    }

    private bool IsOwnButton(MGElement element)
    {
        if (element == null)
        {
            return false;
        }

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (ReferenceEquals(_buttons[i], element))
            {
                return true;
            }
        }

        return false;
    }

    private bool Choose(int buttonIndex)
    {
        if (!IsOpen)
        {
            return false;
        }

        IsOpen = false;
        _content.OnBeginUpdate -= KeepKeyboardFocusInside;

        MGOverlayHost host = Desktop.OverlayHost;
        //  TryClose first: TryRemoveOverlay alone leaves an open overlay in OpenOverlays.
        host.TryClose(Overlay);
        host.TryRemoveOverlay(Overlay);

        if (IsOwnButton(Desktop.QueuedFocusedKeyboardHandler))
        {
            Desktop.ClearQueuedFocusedKeyboardHandler();
        }

        if (IsOwnButton(Desktop.FocusedKeyboardHandler))
        {
            Desktop.ClearFocusedKeyboardHandler();
        }

        Action<int> closed = _closed;
        _closed = null;
        closed(buttonIndex);
        return true;
    }

    private sealed class MessageBoxButton : MGButton
    {
        private readonly MGMessageBox _owner;

        public MessageBoxButton(MGWindow window, MGMessageBox owner, int index)
            : base(window, _ => owner.Choose(index))
        {
            _owner = owner;
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
            => action == UINavigationAction.Cancel
                ? _owner.Choose(_owner.CancelButtonIndex)
                : base.TryHandleNavigationAction(action);
    }
}
