using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Styling;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using MGUI.Core.UI.Brushes.BorderBrushes;

namespace MGUI.Core.UI;

/// <summary>Represents a single clickable entry in an <see cref="MGMenuBar"/>.<para/>
/// Contains an optional <see cref="Submenu"/> that opens as a dropdown when the item is clicked.</summary>
public class MGMenuBarItem : MGSingleContentHost
{
    public MGMenuBar MenuBar { get; }

    #region Border
    /// <summary>Provides direct access to this element's border.</summary>
    public MGComponent<MGBorder> BorderComponent { get; }
    private MGBorder BorderElement { get; }
    public override MGBorder GetBorder() => BorderElement;

    public IBorderBrush BorderBrush
    {
        get => BorderElement.BorderBrush;
        set => BorderElement.BorderBrush = value;
    }

    public Thickness BorderThickness
    {
        get => BorderElement.BorderThickness;
        set => BorderElement.BorderThickness = value;
    }

    public MGCornerRadius CornerRadius
    {
        get => BorderElement.CornerRadius;
        set => BorderElement.CornerRadius = value;
    }
    #endregion Border

    #region ContentWrapper
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGButton _ContentWrapper;
    private MGVisualStateProjection OwnerVisualStateProjection { get; set; }
    private MGVisualStateProjection ContentWrapperVisualStateProjection { get; set; }

    /// <summary>The <see cref="MGButton"/> that wraps this item's content and handles click/hover interactions.</summary>
    public MGButton ContentWrapper
    {
        get => _ContentWrapper;
        internal set
        {
            if (_ContentWrapper != value)
            {
                var Previous = ContentWrapper;
                _ContentWrapper = value;

                if (Previous != null)
                {
                    using (Previous.AllowChangingContentTemporarily())
                        Previous.SetContent(null as MGElement);
                }

                if (ContentWrapper != null)
                {
                    ContentWrapper.IsFocusable = false;
                    ContentWrapper.CanChangeContent = false;
                    if (_ItemContent != null)
                    {
                        _ItemContent.IsHitTestVisible = false;
                    }
                    using (ContentWrapper.AllowChangingContentTemporarily())
                        ContentWrapper.SetContent(_ItemContent);
                }

                using (AllowChangingContentTemporarily())
                    SetContent(ContentWrapper);

                NPC(nameof(ContentWrapper));
                OnContentWrapperChanged();
                RefreshVisualStateProjection();
            }
        }
    }

    private void OnContentWrapperChanged()
    {
        if (ContentWrapper == null)
        {
            return;
        }

        ContentWrapper.AddCommandHandler((Btn, e) =>
        {
            if (MenuBar.ActiveItem == this)
            {
                MenuBar.CloseActiveItem();
            }
            else
            {
                MenuBar.OpenItem(this);
            }
        });

        ContentWrapper.MouseHandler.Entered += (sender, e) =>
        {
            if (MenuBar.IsMenuActive && MenuBar.ActiveItem != this)
            {
                MenuBar.OpenItem(this);
            }
        };
    }

    private void RefreshVisualStateProjection()
    {
        OwnerVisualStateProjection?.Dispose();
        ContentWrapperVisualStateProjection?.Dispose();

        OwnerVisualStateProjection = new(this, (_, __) => ApplyContentWrapperVisualState());
        if (ContentWrapper != null)
        {
            ContentWrapperVisualStateProjection = new(ContentWrapper, (_, __) => ApplyContentWrapperVisualState());
        }
    }

    private void ApplyContentWrapperVisualState()
    {
        if (ContentWrapper == null)
        {
            return;
        }

        var ownerState = VisualState;
        var wrapperState = ContentWrapper.VisualState;
        var isPressed = ownerState.IsPressed || wrapperState.IsPressed;
        var isHighlighted = ownerState.IsPressedOrHovered || wrapperState.IsPressedOrHovered || Submenu?.IsContextMenuOpen == true;
        ContentWrapper.IsSelected = isHighlighted;
        ContentWrapper.SpoofIsHoveredWhileDrawingBackground = isHighlighted && !isPressed;
        ContentWrapper.SpoofIsPressedWhileDrawingBackground = isPressed;

        var wrapperBorder = ContentWrapper.GetBorder();
        if (wrapperBorder != null)
        {
            wrapperBorder.IsSelected = isHighlighted;
            wrapperBorder.SpoofIsHoveredWhileDrawingBackground = isHighlighted && !isPressed;
            wrapperBorder.SpoofIsPressedWhileDrawingBackground = isPressed;
        }
    }

    private MGElement _ItemContent;
    #endregion ContentWrapper

    #region Submenu
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGContextMenu _Submenu;
    /// <summary>The dropdown <see cref="MGContextMenu"/> that opens when this item is clicked.<para/>
    /// Can be null if the item has no dropdown.</summary>
    public MGContextMenu Submenu
    {
        get => _Submenu;
        set
        {
            if (_Submenu != value)
            {
                if (_Submenu != null)
                {
                    if (_Submenu.OpenedFromMenuBarItem == this)
                    {
                        _Submenu.OpenedFromMenuBarItem = null;
                    }
                    _Submenu.ItemSelected -= Submenu_ItemSelected;
                    _Submenu.ItemToggled -= Submenu_ItemToggled;
                    _Submenu.ItemRadioSelected -= Submenu_ItemRadioSelected;
                    _Submenu.ContextMenuOpened -= Submenu_Opened;
                    _Submenu.ContextMenuClosed -= Submenu_Closed;
                }

                _Submenu = value;

                if (_Submenu != null)
                {
                    _Submenu.OpenedFromMenuBarItem = this;
                    _Submenu.ItemSelected += Submenu_ItemSelected;
                    _Submenu.ItemToggled += Submenu_ItemToggled;
                    _Submenu.ItemRadioSelected += Submenu_ItemRadioSelected;
                    _Submenu.ContextMenuOpened += Submenu_Opened;
                    _Submenu.ContextMenuClosed += Submenu_Closed;
                }

                NPC(nameof(Submenu));
            }
        }
    }

    private void Submenu_Opened(object sender, EventArgs e)
    {
        ApplyContentWrapperVisualState();
    }

    private void Submenu_Closed(object sender, EventArgs e)
    {
        ApplyContentWrapperVisualState();

        MenuBar.OnSubmenuClosed(this);
    }

    private void Submenu_ItemSelected(object sender, MGContextMenuButton e) => MenuBar.InvokeItemSelected(e);
    private void Submenu_ItemToggled(object sender, MGContextMenuToggle e) => MenuBar.InvokeItemToggled(e);
    private void Submenu_ItemRadioSelected(object sender, MGContextMenuRadioButton e) => MenuBar.InvokeItemRadioSelected(e);
    #endregion Submenu

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);

        if (CurrentTheme != null && ContentWrapper != null)
        {
            var background = CurrentTheme.GetBackgroundBrush(MGElementType.MenuBarItem);
            Color? textForeground = CurrentTheme.TextBlockFallbackForeground.GetValue(true).NormalValue;
            ContentWrapper.SetBackground(background, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
            ContentWrapper.SetDefaultTextForegroundAll(textForeground, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
            if (ContentWrapper.GetBorder() != null)
            {
                ContentWrapper.GetBorder().SetBackground(background?.Copy(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
            }
        }
    }

    /// <summary>Opens the <see cref="Submenu"/> positioned directly below this item in screen space.</summary>
    internal void OpenSubmenu()
    {
        if (Submenu == null || Submenu.IsContextMenuOpen)
        {
            return;
        }

        var ScreenBounds = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, LayoutBounds);
        Point AnchorPoint = new(ScreenBounds.Left, ScreenBounds.Bottom);
        Submenu.TryOpenContextMenu(AnchorPoint);
    }

    /// <summary>Closes the <see cref="Submenu"/> if it is currently open.</summary>
    internal void CloseSubmenu()
    {
        if (Submenu?.IsContextMenuOpen == true)
        {
            Submenu.TryCloseContextMenu();
        }
    }

    public MGMenuBarItem(MGMenuBar MenuBar, MGElement Content)
        : base(MenuBar.SelfOrParentWindow, MGElementType.MenuBarItem)
    {
        using (BeginInitializing())
        {
            this.MenuBar = MenuBar;
            _ItemContent = Content;

            BorderElement = new(SelfOrParentWindow, new Thickness(0), MGUniformBorderBrush.Black);
            BorderComponent = MGComponentBase.Create(BorderElement);
            AddComponent(BorderComponent);
            BorderElement.OnBorderBrushChanged += (sender, e) => NPC(nameof(BorderBrush));
            BorderElement.OnBorderThicknessChanged += (sender, e) => NPC(nameof(BorderThickness));
            BorderElement.OnCornerRadiusChanged += (sender, e) => NPC(nameof(CornerRadius));

            CanChangeContent = false;

            ContentWrapper = MenuBar.ButtonWrapperTemplate(SelfOrParentWindow);
        }
    }
}

/// <summary>A horizontal menu bar that displays a row of top-level <see cref="MGMenuBarItem"/>s,
/// each of which can open a dropdown <see cref="MGContextMenu"/> when clicked.<para/>
/// Supports hierarchical menus (unlimited levels), separators, toggle items, radio groups, disabled items, and icon + text.</summary>
public class MGMenuBar : MGSingleContentHost
{
    internal static int GetAdjacentItemIndex(int currentIndex, int count, UINavigationAction action)
    {
        if (count <= 0)
        {
            return -1;
        }

        var normalizedIndex = currentIndex < 0 ? 0 : currentIndex;
        return action switch
        {
            UINavigationAction.MoveLeft => Math.Max(0, normalizedIndex - 1),
            UINavigationAction.MoveRight => Math.Min(count - 1, normalizedIndex + 1),
            UINavigationAction.Home => 0,
            UINavigationAction.End => count - 1,
            _ => normalizedIndex
        };
    }

    /// <inheritdoc/>
    public override bool CanHandleKeyboardInput => IsFocusable || IsMenuActive;
    #region Border
    /// <summary>Provides direct access to this element's border.</summary>
    public MGComponent<MGBorder> BorderComponent { get; }
    private MGBorder BorderElement { get; }
    public override MGBorder GetBorder() => BorderElement;

    public IBorderBrush BorderBrush
    {
        get => BorderElement.BorderBrush;
        set => BorderElement.BorderBrush = value;
    }

    public Thickness BorderThickness
    {
        get => BorderElement.BorderThickness;
        set => BorderElement.BorderThickness = value;
    }

    public MGCornerRadius CornerRadius
    {
        get => BorderElement.CornerRadius;
        set => BorderElement.CornerRadius = value;
    }
    #endregion Border

    #region Internal layout
    /// <summary>The horizontal <see cref="MGStackPanel"/> that contains all top-level <see cref="MGMenuBarItem"/>s.</summary>
    public MGStackPanel ItemsPanel { get; }
    #endregion Internal layout

    #region Items
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ObservableCollection<MGMenuBarItem> _Items { get; }
    public IList<MGMenuBarItem> Items => _Items;

    /// <summary>Adds a new top-level menu item with the given text label.</summary>
    /// <param name="Text">The label text displayed in the bar.</param>
    /// <param name="Configure">Optional callback invoked after creation to configure the item (e.g. attach a <see cref="MGMenuBarItem.Submenu"/>).</param>
    public MGMenuBarItem AddItem(string Text, Action<MGMenuBarItem> Configure = null)
        => AddItem(new MGTextBlock(SelfOrParentWindow, Text, null, GetTheme().FontSettings.ContextMenuFontSize), Configure);

    /// <summary>Adds a new top-level menu item with a custom content element.</summary>
    public MGMenuBarItem AddItem(MGElement Content, Action<MGMenuBarItem> Configure = null)
    {
        MGMenuBarItem Item = new(this, Content);
        Configure?.Invoke(Item);
        _Items.Add(Item);
        return Item;
    }
    #endregion Items

    #region ButtonWrapperTemplate
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Func<MGWindow, MGButton> _ButtonWrapperTemplate;
    /// <summary>Factory used to create the <see cref="MGButton"/> wrapper for each <see cref="MGMenuBarItem"/>.<para/>
    /// Default value: <see cref="CreateDefaultBarButton"/></summary>
    public Func<MGWindow, MGButton> ButtonWrapperTemplate
    {
        get => _ButtonWrapperTemplate;
        set
        {
            if (_ButtonWrapperTemplate != value)
            {
                _ButtonWrapperTemplate = value;
                NPC(nameof(ButtonWrapperTemplate));
            }
        }
    }

    /// <summary>Creates the default button style for a top-level <see cref="MGMenuBarItem"/>.</summary>
    public MGButton CreateDefaultBarButton(MGWindow Window)
    {
        MGButton Button = new(Window ?? this.SelfOrParentWindow, new Thickness(0), MGUniformBorderBrush.Black);
        Button.SetPadding(new Thickness(8, 3, 8, 3), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        Button.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        Button.HorizontalContentAlignment = HorizontalAlignment.Center;
        Button.VerticalContentAlignment = VerticalAlignment.Center;
        var background = GetTheme().GetBackgroundBrush(MGElementType.MenuBarItem);
        Color? textForeground = GetTheme().TextBlockFallbackForeground.GetValue(true).NormalValue;
        // ADR-0005: MGMenuBarItem.OnThemeChanged re-writes these same elements' Background IN PLACE at Theme(20)
        // precedence; tagging this factory write LocalValue(90) would outrank and freeze it against later theme
        // refreshes, so it uses Default(0) instead, like a constructor writing its own default.
        Button.SetBackground(background, UIValueResolutionSource.Default(UIInvalidationKind.Draw));
        Button.SetDefaultTextForegroundAll(textForeground, UIValueResolutionSource.Default(UIInvalidationKind.Draw));
        Button.GetBorder().SetBackground(background?.Copy(), UIValueResolutionSource.Default(UIInvalidationKind.Draw));
        return Button;
    }
    #endregion ButtonWrapperTemplate

    #region Active Item / Menu State
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsMenuActive;
    /// <summary>True when at least one top-level item's dropdown is open.</summary>
    public bool IsMenuActive
    {
        get => _IsMenuActive;
        private set
        {
            if (_IsMenuActive != value)
            {
                _IsMenuActive = value;
                NPC(nameof(IsMenuActive));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MGMenuBarItem _ActiveItem;
    /// <summary>The <see cref="MGMenuBarItem"/> whose dropdown is currently open, or null.</summary>
    public MGMenuBarItem ActiveItem
    {
        get => _ActiveItem;
        private set
        {
            if (_ActiveItem != value)
            {
                _ActiveItem = value;
                NPC(nameof(ActiveItem));
            }
        }
    }

    /// <summary>Opens the dropdown of the given <paramref name="Item"/>,
    /// closing the previously active item's dropdown if needed.</summary>
    internal void OpenItem(MGMenuBarItem Item)
    {
        if (Item == null || Item.Submenu == null)
        {
            return;
        }

        if (ActiveItem != null && ActiveItem != Item)
        {
            ActiveItem.CloseSubmenu();
        }

        ActiveItem = Item;
        IsMenuActive = true;

        // Claim keyboard focus before opening the submenu so the submenu's initial focus
        // assignment can take precedence for popup item navigation.
        GetDesktop().QueueFocusedKeyboardHandler(this, KeyboardFocusSource.Programmatic);
        Item.OpenSubmenu();
    }

    /// <summary>Closes the dropdown of the current active item.</summary>
    internal void CloseActiveItem()
    {
        if (ActiveItem != null)
        {
            var Prev = ActiveItem;
            ActiveItem = null;
            IsMenuActive = false;
            Prev.CloseSubmenu();

            // Release keyboard focus
            if (GetDesktop().FocusedKeyboardHandler == this)
            {
                GetDesktop().ClearQueuedFocusedKeyboardHandler();
            }
        }
    }

    /// <summary>Called by <see cref="MGMenuBarItem"/> when its submenu closes for any reason.</summary>
    internal void OnSubmenuClosed(MGMenuBarItem Item)
    {
        if (ActiveItem == Item)
        {
            ActiveItem = null;
            IsMenuActive = false;
        }
    }
    #endregion Active Item / Menu State

    #region Events
    /// <summary>Invoked when a <see cref="MGContextMenuButton"/> in any dropdown is clicked.</summary>
    public event EventHandler<MGContextMenuButton> ItemSelected;
    /// <summary>Invoked when a <see cref="MGContextMenuToggle"/> in any dropdown is toggled.</summary>
    public event EventHandler<MGContextMenuToggle> ItemToggled;
    /// <summary>Invoked when a <see cref="MGContextMenuRadioButton"/> in any dropdown is selected.</summary>
    public event EventHandler<MGContextMenuRadioButton> ItemRadioSelected;

    internal void InvokeItemSelected(MGContextMenuButton e) => ItemSelected?.Invoke(this, e);
    internal void InvokeItemToggled(MGContextMenuToggle e) => ItemToggled?.Invoke(this, e);
    internal void InvokeItemRadioSelected(MGContextMenuRadioButton e) => ItemRadioSelected?.Invoke(this, e);
    #endregion Events

    /// <param name="Window">The parent <see cref="MGWindow"/>.</param>
    public MGMenuBar(MGWindow Window)
        : base(Window, MGElementType.MenuBar)
    {
        using (BeginInitializing())
        {
            IsFocusable = true;
            BorderElement = new(Window, new Thickness(0, 0, 0, 1), MGUniformBorderBrush.Black);
            BorderComponent = MGComponentBase.Create(BorderElement);
            AddComponent(BorderComponent);
            BorderElement.OnBorderBrushChanged += (sender, e) => NPC(nameof(BorderBrush));
            BorderElement.OnBorderThicknessChanged += (sender, e) => NPC(nameof(BorderThickness));
            BorderElement.OnCornerRadiusChanged += (sender, e) => NPC(nameof(CornerRadius));

            ButtonWrapperTemplate = CreateDefaultBarButton;

            ItemsPanel = new(Window, Orientation.Horizontal);
            ItemsPanel.Spacing = 0;
            ItemsPanel.ManagedParent = this;
            ItemsPanel.CanChangeContent = false;

            using (AllowChangingContentTemporarily())
                SetContent(ItemsPanel);
            CanChangeContent = false;

            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Top;
            SetMinHeight(22, UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            SetPadding(new Thickness(2, 1, 2, 1), UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

            _Items = new();
            _Items.CollectionChanged += (sender, e) =>
            {
                using (ItemsPanel.AllowChangingContentTemporarily())
                {
                    if (e.Action is NotifyCollectionChangedAction.Add)
                    {
                        if (e.NewItems != null)
                        {
                            var Index = e.NewStartingIndex;
                            foreach (MGMenuBarItem Item in e.NewItems)
                            {
                                ItemsPanel.TryInsertChild(Index, Item);
                                Index++;
                            }
                        }
                    }

                    if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset)
                    {
                        if (e.OldItems != null)
                        {
                            foreach (MGMenuBarItem Item in e.OldItems)
                            {
                                ItemsPanel.TryRemoveChild(Item);
                            }
                        }
                    }

                    if (e.Action is NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Move)
                    {
                        throw new NotImplementedException();
                    }
                }
            };
        }
    }

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        if (_Items.Count == 0)
        {
            return false;
        }

        if (action == UINavigationAction.Cancel && IsMenuActive)
        {
            CloseActiveItem();
            return true;
        }

        if (action == UINavigationAction.Submit)
        {
            var targetItem = ActiveItem ?? _Items.FirstOrDefault();
            if (targetItem?.Submenu != null)
            {
                OpenItem(targetItem);
                return true;
            }

            return false;
        }

        if (action is not (UINavigationAction.MoveLeft or UINavigationAction.MoveRight or UINavigationAction.Home or UINavigationAction.End))
        {
            return false;
        }

        var currentIndex = Math.Max(0, _Items.IndexOf(ActiveItem));
        var nextIndex = GetAdjacentItemIndex(currentIndex, _Items.Count, action);
        if (nextIndex < 0)
        {
            return false;
        }

        OpenItem(_Items[nextIndex]);
        return true;
    }
}