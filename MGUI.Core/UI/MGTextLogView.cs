using MGUI.Shared.Helpers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI;

public readonly record struct MGTextLogEntry(string Message, DateTime Timestamp, string Category = null);

/// <summary>Lightweight append-only text feed for console, log, and debug scenarios.
/// It intentionally builds on <see cref="MGListBox{TItemType}"/> and <see cref="MGTextBlock"/> instead of introducing a document editor.</summary>
public class MGTextLogView : MGListBox<MGTextLogEntry>
{
    public ObservableCollection<MGTextLogEntry> Entries { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string _TimestampFormat;
    public string TimestampFormat
    {
        get => _TimestampFormat;
        set
        {
            if (_TimestampFormat != value)
            {
                _TimestampFormat = value;
                RefreshItemTemplate();
                NPC(nameof(TimestampFormat));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _ShowTimestamps;
    public bool ShowTimestamps
    {
        get => _ShowTimestamps;
        set
        {
            if (_ShowTimestamps != value)
            {
                _ShowTimestamps = value;
                RefreshItemTemplate();
                NPC(nameof(ShowTimestamps));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _AllowsInlineFormatting;
    public bool AllowsInlineFormatting
    {
        get => _AllowsInlineFormatting;
        set
        {
            if (_AllowsInlineFormatting != value)
            {
                _AllowsInlineFormatting = value;
                RefreshItemTemplate();
                NPC(nameof(AllowsInlineFormatting));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _AutoScrollToBottom;
    public bool AutoScrollToBottom
    {
        get => _AutoScrollToBottom;
        set
        {
            if (_AutoScrollToBottom != value)
            {
                _AutoScrollToBottom = value;
                NPC(nameof(AutoScrollToBottom));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _MaxEntries;
    public int MaxEntries
    {
        get => _MaxEntries;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxEntries), "MaxEntries must be greater than zero.");
            }

            if (_MaxEntries != value)
            {
                _MaxEntries = value;
                ValidateMaxEntries();
                NPC(nameof(MaxEntries));
            }
        }
    }

    private bool IsAdjustingEntries { get; set; }
    private bool IsEntriesValidationPending { get; set; }

    public MGTextLogView(MGWindow ParentWindow, int MaxEntries = 250)
        : base(ParentWindow)
    {
        using (BeginInitializing())
        {
            Entries = new();
            Entries.CollectionChanged += Entries_CollectionChanged;
            SetItemsSource(Entries);

            IsTitleVisible = false;
            SelectionMode = ListBoxSelectionMode.None;
            OuterBorderThickness = new(0);
            TitleBorderThickness = new(0);
            InnerBorderThickness = new(0);
            ItemsPanel.SetBorderThicknessTagged(new(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            SetMinHeight(0, UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            ItemContainerStyle = presenter =>
            {
                ApplyDefaultItemContainerStyle(presenter);
                presenter.SetBorderThickness(new(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            };

            _TimestampFormat = @"'\\['HH:mm:ss']'";
            _ShowTimestamps = true;
            _AllowsInlineFormatting = true;
            _AutoScrollToBottom = true;
            _MaxEntries = MaxEntries;
            RefreshItemTemplate();
        }
    }

    public void AppendEntry(string Message, string Category = null)
        => AppendEntry(new(Message, DateTime.Now, Category));

    public void AppendEntry(MGTextLogEntry Entry)
    {
        bool WasScrolledToBottom = AutoScrollToBottom && ScrollViewer.VerticalOffset.IsAlmostEqual(ScrollViewer.MaxVerticalOffset);
        Entries.Add(Entry);
        ValidateMaxEntries();
        if (WasScrolledToBottom)
        {
            ScrollViewer.QueueScrollToBottom();
        }
    }

    public void ClearEntries() => Entries.Clear();

    private void Entries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsAdjustingEntries && e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset)
        {
            IsEntriesValidationPending = true;
        }
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        try
        {
            if (IsEntriesValidationPending)
            {
                ValidateMaxEntries();
            }
        }
        finally
        {
            IsEntriesValidationPending = false;
        }

        base.UpdateSelf(UA);
    }

    private void ValidateMaxEntries()
    {
        if (Entries == null)
        {
            return;
        }

        IsAdjustingEntries = true;
        try
        {
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
        }
        finally
        {
            IsAdjustingEntries = false;
        }
    }

    private void RefreshItemTemplate()
    {
        ItemTemplate = entry => CreateEntryElement(entry);
    }

    private MGElement CreateEntryElement(MGTextLogEntry Entry)
    {
        MGTextBlock TextBlock = new(ParentWindow, FormatEntryText(Entry), AllowsInlineFormatting: AllowsInlineFormatting)
        {
            WrapText = true,
            ManagedParent = this,
        };

        return TextBlock;
    }

    private string FormatEntryText(MGTextLogEntry Entry)
    {
        StringBuilder Builder = new();

        if (ShowTimestamps)
        {
            Builder.Append(Entry.Timestamp.ToString(TimestampFormat));
            if (!string.IsNullOrEmpty(Entry.Category) || !string.IsNullOrEmpty(Entry.Message))
            {
                Builder.Append(' ');
            }
        }

        if (!string.IsNullOrWhiteSpace(Entry.Category))
        {
            Builder.Append('(').Append(Entry.Category).Append(") ");
        }

        Builder.Append(Entry.Message ?? string.Empty);
        return Builder.ToString();
    }
}