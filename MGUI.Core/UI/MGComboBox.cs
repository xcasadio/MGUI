using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.XAML;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using Thickness = MonoGame.Extended.Thickness;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Size = MonoGame.Extended.Size;
using MGUI.Core.UI.Data_Binding;

namespace MGUI.Core.UI
{
    public record class TemplatedElement<TDataType, TElementType>(TDataType SourceData, TElementType Element);

    /// <typeparam name="TItemType">The type that the ItemsSource will be bound to. Usually this would be: <see cref="string"/> for simple text-choices</typeparam>
    public class MGComboBox<TItemType> : MGSingleContentHost, INavigationTargetVisibilityHandler
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _AutoWidthFromContent;
        public bool AutoWidthFromContent
        {
            get => _AutoWidthFromContent;
            set
            {
                if (_AutoWidthFromContent != value)
                {
                    _AutoWidthFromContent = value;
                    LayoutChanged(this, true);
                    NPC(nameof(AutoWidthFromContent));
                }
            }
        }

        protected internal override bool IgnorePreferredWidthDuringMeasure => AutoWidthFromContent;

        public const string BorderPartName = "PART_Border";
        public const string DropdownArrowPartName = "PART_DropdownArrow";
        public const string DropdownWindowPartName = "PART_DropdownWindow";
        public const string DropdownHeaderPresenterPartName = "PART_DropdownHeaderPresenter";
        public const string DropdownFooterPresenterPartName = "PART_DropdownFooterPresenter";
        public const string DropdownItemsPanelPartName = "PART_DropdownItemsPanel";
        public const string DropdownScrollViewerPartName = "PART_DropdownScrollViewer";
        public const string DropdownDockPanelPartName = "PART_DropdownDockPanel";
        internal const string DropdownItemTemplateOwnerMetadataKey = "ComboBox.DropdownItemOwner";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(BorderPartName, typeof(MGBorder));
            yield return new(DropdownArrowPartName, typeof(MGContentPresenter));
            yield return new(DropdownWindowPartName, typeof(MGWindow));
            yield return new(DropdownHeaderPresenterPartName, typeof(MGContentPresenter), false);
            yield return new(DropdownFooterPresenterPartName, typeof(MGContentPresenter), false);
            yield return new(DropdownItemsPanelPartName, typeof(MGStackPanel));
            yield return new(DropdownScrollViewerPartName, typeof(MGScrollViewer));
            yield return new(DropdownDockPanelPartName, typeof(MGDockPanel), false);
        }

        internal static int GetNextNavigationIndex(int currentIndex, int itemCount, UINavigationAction action)
        {
            if (itemCount <= 0)
            {
                return -1;
            }

            int largeStep = Math.Max(1, itemCount / 5);

            return action switch
            {
                UINavigationAction.MoveUp or UINavigationAction.MovePrevious => Math.Max(0, currentIndex - 1),
                UINavigationAction.MoveDown or UINavigationAction.MoveNext => Math.Min(itemCount - 1, currentIndex + 1),
                UINavigationAction.Home => 0,
                UINavigationAction.End => itemCount - 1,
                UINavigationAction.PageUp => Math.Max(0, currentIndex - largeStep),
                UINavigationAction.PageDown => Math.Min(itemCount - 1, currentIndex + largeStep),
                _ => currentIndex
            };
        }

        #region Border
        /// <summary>Provides direct access to this element's border.</summary>
        public MGComponent<MGBorder> BorderComponent { get; private set; }
        private MGBorder BorderElement { get; set; }
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

        #region Dropdown Arrow
        /// <summary>Provides direct access to the dropdown part of this combobox.</summary>
        public MGComponent<MGContentPresenter> DropdownArrowComponent { get; private set; }
        public MGContentPresenter DropdownArrowElement { get; private set; }

        /// <summary>The width of the <see cref="MGContentPresenter"/> that hosts the dropdown arrow. This should be >= <see cref="DropdownArrowWidth"/></summary>
        public const int DropdownArrowPaddedWidth = 16;
        /// <summary>The width of the dropdown arrow</summary>
        public const int DropdownArrowWidth = 10;
        /// <summary>The height of the <see cref="MGContentPresenter"/> that hosts the dropdown arrow. This should be >= <see cref="DropdownArrowHeight"/></summary>
        public const int DropdownArrowPaddedHeight = 16;
        /// <summary>The height of the dropdown arrow</summary>
        public const int DropdownArrowHeight = 6;

        /// <summary>The default empty width between the right edge of the <see cref="MGSingleContentHost.Content"/> and the left edge of the dropdown arrow</summary>
        public static int DefaultDropdownArrowLeftMargin { get; set; } = 7;
        /// <summary>The default empty width between the right edge of the dropdown arrow and the right edge of this <see cref="MGComboBox{TItemType}"/></summary>
        public static int DefaultDropdownArrowRightMargin { get; set; } = 5;

        /// <summary>The default empty width between the right edge of the <see cref="MGSingleContentHost.Content"/> and the left edge of the dropdown arrow<para/>
        /// See also: <see cref="DefaultDropdownArrowLeftMargin"/></summary>
        public int DropdownArrowLeftMargin
        {
            get => DropdownArrowElement.Margin.Left;
            set
            {
                if (DropdownArrowElement.Margin.Left != value)
                {
                    DropdownArrowElement.SetMargin(DropdownArrowElement.Margin.ChangeLeft(value), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                    NPC(nameof(DropdownArrowLeftMargin));
                }
            }
        }

        /// <summary>The default empty width between the right edge of the dropdown arrow and the right edge of this <see cref="MGComboBox{TItemType}"/><para/>
        /// See also: <see cref="DefaultDropdownArrowRightMargin"/></summary>
        public int DropdownArrowRightMargin
        {
            get => DropdownArrowElement.Margin.Right;
            set
            {
                if (DropdownArrowElement.Margin.Right != value)
                {
                    DropdownArrowElement.SetMargin(DropdownArrowElement.Margin.ChangeRight(value), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                    NPC(nameof(DropdownArrowRightMargin));
                }
            }
        }
        #endregion Dropdown Arrow

        #region Templated Items
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<TemplatedElement<TItemType, MGButton>> _TemplatedItems;
        private ObservableCollection<TemplatedElement<TItemType, MGButton>> TemplatedItems
        {
            get => _TemplatedItems;
            set
            {
                if (_TemplatedItems != value)
                {
                    if (TemplatedItems != null)
                    {
                        HandleTemplatedContentRemoved(TemplatedItems.Select(x => x.Element));
                        TemplatedItems.CollectionChanged -= TemplatedItems_CollectionChanged;
                    }
                    _TemplatedItems = value;
                    if (TemplatedItems != null)
                    {
                        TemplatedItems.CollectionChanged += TemplatedItems_CollectionChanged;
                    }

                    DropdownContentChanged();
                    _NavigationTarget = null;
                    HoveredItem = null;
                }
            }
        }

        private static void HandleTemplatedContentRemoved(IEnumerable<MGButton> Items)
        {
            if (Items != null)
            {
                foreach (var Item in Items)
                {
                    Item.RemoveDataBindings(true);
                }
            }
        }

        private void TemplatedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            DropdownContentChanged();

            if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace && e.OldItems != null)
            {
                HandleTemplatedContentRemoved(e.OldItems.Cast<TemplatedElement<TItemType, MGButton>>().Select(x => x.Element));
            }

            //  Rows can leave the list while the dropdown is open: never keep a keyboard target or a hovered row that is no longer listed.
            if (_NavigationTarget != null && !TemplatedItems.Contains(_NavigationTarget))
            {
                _NavigationTarget = null;
                RefreshDropdownItemSelectionVisuals();
            }

            if (HoveredItem != null && !TemplatedItems.Contains(HoveredItem))
            {
                HoveredItem = null;
            }
        }

        #region Selected Item
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TemplatedElement<TItemType, MGButton> _SelectedTemplatedItem;
        private TemplatedElement<TItemType, MGButton> SelectedTemplatedItem
        {
            get => _SelectedTemplatedItem;
            set
            {
                if (_SelectedTemplatedItem != value)
                {
                    TemplatedElement<TItemType, MGButton> PreviousSelection = SelectedTemplatedItem;
                    if (SelectedTemplatedItem != null)
                    {
                        SelectedTemplatedItem.Element.IsSelected = false;
                    }

                    _SelectedTemplatedItem = value;
                    UpdateSelectedContent();
                    if (SelectedTemplatedItem != null)
                    {
                        SelectedTemplatedItem.Element.IsSelected = true;
                    }

                    //  A committed selection ends keyboard navigation inside the dropdown (see SetNavigationTarget). When keyboard navigation also set
                    //  HoveredItem, it is cleared as well, so that Submit or the next arrow key cannot act on the old target; a real mouse hover is kept.
                    if (_NavigationTarget != null)
                    {
                        if (ReferenceEquals(HoveredItem, _NavigationTarget))
                        {
                            SetNavigationTarget(null);
                        }
                        else
                        {
                            _NavigationTarget = null;
                            RefreshDropdownItemSelectionVisuals();
                        }
                    }

                    NPC(nameof(SelectedTemplatedItem));
                    NPC(nameof(SelectedItem));
                    NPC(nameof(SelectedIndex));

                    SelectedItemChanged?.Invoke(this, new(
                        PreviousSelection == null ? default(TItemType) : PreviousSelection.SourceData,
                        SelectedTemplatedItem == null ? default(TItemType) : SelectedTemplatedItem.SourceData
                    ));
                }
            }
        }

        private readonly EqualityComparer<TItemType> Comparer = EqualityComparer<TItemType>.Default;
        /// <summary>Warning - If <see cref="ItemsSource"/> contains several items with the same hash code, this property's setter function will always select the first matching value.<para/>
        /// See also: <see cref="SelectedIndex"/></summary>
        public TItemType SelectedItem
        {
            get => SelectedTemplatedItem == null ? default(TItemType) : SelectedTemplatedItem.SourceData;
            set => SelectedTemplatedItem = TemplatedItems.FirstOrDefault(x => Comparer.Equals(value, x.SourceData));
        }

        /// <summary>The index of the currently-selected item.<para/>
        /// See also: <see cref="SelectedItem"/></summary>
        public int SelectedIndex
        {
            get => SelectedTemplatedItem == null ? -1 : TemplatedItems.IndexOf(SelectedTemplatedItem);
            set => SelectedTemplatedItem = value == -1 ? null : TemplatedItems[value];
        }

        public event EventHandler<EventArgs<TItemType>> SelectedItemChanged;

        private void UpdateSelectedContent()
        {
            Content?.RemoveDataBindings(true);
            MGElement SelectedContent = SelectedTemplatedItem == null || SelectedItemTemplate == null ? null : SelectedItemTemplate(SelectedTemplatedItem.SourceData);
            ManagedSetContent(SelectedContent);
        }
        #endregion Selected Item

        #region Hovered Item
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TemplatedElement<TItemType, MGButton> _HoveredItem;
        /// <summary>The item within the dropdown that is currently hovered by the mouse, if any.</summary>
        public TemplatedElement<TItemType, MGButton> HoveredItem
        {
            get => _HoveredItem;
            private set
            {
                if (_HoveredItem != value)
                {
                    TemplatedElement<TItemType, MGButton> Previous = HoveredItem;
                    _HoveredItem = value;
                    NPC(nameof(HoveredItem));
                    HoveredItemChanged?.Invoke(this, new(Previous, HoveredItem));
                }
            }
        }

        public event EventHandler<EventArgs<TemplatedElement<TItemType, MGButton>>> HoveredItemChanged;

        private void UpdateHoveredDropdownItem()
        {
            if (IsDropdownOpen && TemplatedItems != null && DropdownStackPanel.IsHovered)
            {
                HoveredItem = TemplatedItems.FirstOrDefault(x => x.Element.VisualState.IsHovered);
            }
            else
            {
                HoveredItem = null;
            }
        }
        #endregion Hovered Item
        #endregion Templated Items

        #region Items Source
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<TItemType> _ItemsSource;
        public ObservableCollection<TItemType> ItemsSource
        {
            get => _ItemsSource;
            private set
            {
                if (_ItemsSource != value)
                {
                    if (ItemsSource != null)
                    {
                        ItemsSource.CollectionChanged -= ItemsSource_CollectionChanged;
                    }

                    _ItemsSource = value;
                    if (ItemsSource != null)
                    {
                        ItemsSource.CollectionChanged += ItemsSource_CollectionChanged;
                    }

                    if (ItemsSource == null || DropdownItemTemplate == null)
                    {
                        TemplatedItems = null;
                    }
                    else
                    {
                        IEnumerable<TemplatedElement<TItemType, MGButton>> Values = ItemsSource.Select(x => new TemplatedElement<TItemType, MGButton>(x, DropdownItemTemplate(x)));
                        TemplatedItems = new ObservableCollection<TemplatedElement<TItemType, MGButton>>(Values);
                    }

                    NPC(nameof(ItemsSource));
                }
            }
        }

        private void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (TemplatedItems != null && DropdownItemTemplate != null)
            {
                if (e.Action is NotifyCollectionChangedAction.Reset)
                {
                    HandleTemplatedContentRemoved(TemplatedItems.Select(x => x.Element));
                    TemplatedItems.Clear();
                }
                else if (e.Action is NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    int CurrentIndex = e.NewStartingIndex;
                    foreach (TItemType Item in e.NewItems)
                    {
                        TemplatedElement<TItemType, MGButton> TemplatedItem = new(Item, DropdownItemTemplate(Item));
                        TemplatedItems.Insert(CurrentIndex, TemplatedItem);
                        CurrentIndex++;
                    }
                }
                else if (e.Action is NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    int CurrentIndex = e.OldStartingIndex;
                    foreach (var Item in e.OldItems)
                    {
                        TemplatedItems.RemoveAt(CurrentIndex);
                        CurrentIndex++;
                    }
                }
                else if (e.Action is NotifyCollectionChangedAction.Replace)
                {
                    List<TItemType> Old = e.OldItems.Cast<TItemType>().ToList();
                    List<TItemType> New = e.NewItems.Cast<TItemType>().ToList();
                    for (int i = 0; i < Old.Count; i++)
                    {
                        TItemType NewItem = New[i]; 
                        TemplatedElement<TItemType, MGButton> TemplatedItem = new(NewItem, DropdownItemTemplate(NewItem));
                        TemplatedItems[e.OldStartingIndex + i] = TemplatedItem;
                    }
                }
                else if (e.Action is NotifyCollectionChangedAction.Move)
                {
                    throw new NotImplementedException();
                }
            }
        }

        /// <param name="Value"><see cref="ItemsSource"/> will be set to a copy of this <see cref="ICollection{T}"/>.<br/>
        /// If you want <see cref="ItemsSource"/> to dynamically update as the collection changes, pass in an <see cref="ObservableCollection{T}"/></param>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="IsDropdownOpen"/>=true.</exception>
        public void SetItemsSource(ICollection<TItemType> Value)
        {
            if (Value is ObservableCollection<TItemType> Observable)
            {
                ItemsSource = Observable;
            }
            else
            {
                ItemsSource = new ObservableCollection<TItemType>(Value.ToList());
            }
        }
        #endregion Items Source

        #region Item Template
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _DropdownItemControlTemplateName;
        public string DropdownItemControlTemplateName
        {
            get => _DropdownItemControlTemplateName;
            set
            {
                if (_DropdownItemControlTemplateName != value)
                {
                    _DropdownItemControlTemplateName = value;

                    if (TemplatedItems != null)
                    {
                        foreach (TemplatedElement<TItemType, MGButton> item in TemplatedItems)
                        {
                            if (item?.Element != null)
                            {
                                ApplyDefaultDropdownButtonSettings(item.Element);
                            }
                        }
                    }

                    NPC(nameof(DropdownItemControlTemplateName));
                }
            }
        }

        /// <summary>See also: <see cref="ApplyDefaultDropdownButtonSettings(MGButton)"/></summary>
        public MGButton CreateDefaultDropdownButton()
        {
            MGButton Button = new(Dropdown, new(0), null);
            ApplyDefaultDropdownButtonSettings(Button);
            return Button;
        }

        public void ApplyDefaultDropdownButtonSettings(MGButton Target)
        {
            Target.Metadata[DropdownItemTemplateOwnerMetadataKey] = this;
            Target.IsFocusable = false;
            Target.ControlTemplateName = DropdownItemControlTemplateName;
            Target.ApplyControlTemplate(false);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Func<TItemType, MGButton> _DropdownItemTemplate;
        /// <summary>The template to use for items inside the dropdown.<br/>
        /// Highly recommend to use an <see cref="MGElement"/> with padding, such as '8,5,8,5'.<para/>
        /// Default value: <see cref="MGUI.Core.UI.Styling.MGControlTemplateCatalog.CreateDefaultComboBoxDropdownItem{TItemType}(MGComboBox{TItemType}, TItemType)"/><para/>
        /// <code>
        /// DropdownItemTemplate = item => <see cref="MGUI.Core.UI.Styling.MGControlTemplateCatalog.CreateDefaultComboBoxDropdownItem{TItemType}(MGComboBox{TItemType}, TItemType)"/>(this, item);
        /// </code></summary>
        public Func<TItemType, MGButton> DropdownItemTemplate
        {
            get => _DropdownItemTemplate;
            set
            {
                if (_DropdownItemTemplate != value)
                {
                    _DropdownItemTemplate = value;

                    if (ItemsSource == null || DropdownItemTemplate == null)
                    {
                        TemplatedItems = null;
                    }
                    else
                    {
                        IEnumerable<TemplatedElement<TItemType, MGButton>> Values = ItemsSource.Select(x => new TemplatedElement<TItemType, MGButton>(x, DropdownItemTemplate(x)));
                        TemplatedItems = new ObservableCollection<TemplatedElement<TItemType, MGButton>>(Values);
                    }

                    NPC(nameof(DropdownItemTemplate));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Func<TItemType, MGElement> _SelectedItemTemplate;
        /// <summary>The template to use for the selected item<para/>
        /// Default value:<para/>
        /// <code>
        /// SelectedItemTemplate = item => <see cref="MGUI.Core.UI.Styling.MGControlTemplateCatalog.CreateDefaultComboBoxSelectedItemContent{TItemType}(MGWindow, TItemType)"/>(Window, item);
        /// </code></summary>
        public Func<TItemType, MGElement> SelectedItemTemplate
        {
            get => _SelectedItemTemplate;
            set
            {
                if (_SelectedItemTemplate != value)
                {
                    _SelectedItemTemplate = value;
                    UpdateSelectedContent();
                    NPC(nameof(SelectedItemTemplate));
                }
            }
        }
        #endregion Item Template

        #region Dropdown
        /// <summary>When the dropdown is opened, it will attempt to use the <see cref="MGElement.ActualWidth"/> of this <see cref="MGComboBox{TItemType}"/>,<br/>
        /// but the width will be clamped to the range [<see cref="MinDropdownWidth"/>, <see cref="MaxDropdownWidth"/>]</summary>
        public int MinDropdownWidth { get; set; } = 180;
        /// <summary>When the dropdown is opened, it will attempt to use the <see cref="MGElement.ActualWidth"/> of this <see cref="MGComboBox{TItemType}"/>,<br/>
        /// but the width will be clamped to the range [<see cref="MinDropdownWidth"/>, <see cref="MaxDropdownWidth"/>]</summary>
        public int MaxDropdownWidth { get; set; } = 1000;
        public int MinDropdownHeight { get; set; } = 100;
        public int MaxDropdownHeight { get; set; } = 360;

        /// <summary>The floating <see cref="MGWindow"/> used to display the content inside of the dropdown when <see cref="IsDropdownOpen"/> is true.<para/>
        /// Warning - be careful when editing properties on this object. Some changes could break the combobox's functionality,<br/>
        /// such as setting <see cref="MGWindow.IsTitleBarVisible"/> and <see cref="MGWindow.IsCloseButtonVisible"/> to true, and then clicking the close button.<para/>
        /// See also: <see cref="DropdownScrollViewer"/>, <see cref="DropdownStackPanel"/></summary>
        public MGWindow Dropdown { get; private set; }
        /// <summary>The <see cref="MGScrollViewer"/> that the <see cref="Dropdown"/>'s Content is wrapped in.<para/>
        /// See also: <see cref="Dropdown"/>, <see cref="DropdownStackPanel"/></summary>
        public MGScrollViewer DropdownScrollViewer { get; private set; }
        /// <summary>The <see cref="MGStackPanel"/> that the <see cref="ItemsSource"/>'s rows are added to.<para/>
        /// See also: <see cref="Dropdown"/>, <see cref="DropdownScrollViewer"/></summary>
        public MGStackPanel DropdownStackPanel { get; private set; }
        private MGDockPanel DropdownDockPanel { get; set; }

        private bool IsDropdownContentValid;
        private void DropdownContentChanged()
        {
            IsDropdownContentValid = false;
        }

        internal static int GetFittedDropdownLeft(int PreferredLeft, int PreferredRight, int DesiredWidth, Rectangle Viewport, float Scale)
        {
            float ActualScale = Scale > 0 ? Scale : 1.0f;
            int DesiredScreenWidth = Math.Min(Viewport.Width, (int)Math.Ceiling(Math.Max(0, DesiredWidth) * ActualScale));
            int MaxLeft = Math.Max(Viewport.Left, Viewport.Right - DesiredScreenWidth);

            int ActualLeft = PreferredLeft;
            if (ActualLeft + DesiredScreenWidth > Viewport.Right)
            {
                ActualLeft = PreferredRight - DesiredScreenWidth;
            }

            return Math.Clamp(ActualLeft, Viewport.Left, MaxLeft);
        }

        private void PositionDropdown(int DesiredWidth)
        {
            Rectangle Viewport = GetDesktop().ValidScreenBounds;
            Point TopLeft = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, LayoutBounds.BottomLeft());
            Point TopRight = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, LayoutBounds.BottomRight());
            Dropdown.Left = GetFittedDropdownLeft(TopLeft.X, TopRight.X, DesiredWidth, Viewport, Dropdown.Scale);
            Dropdown.Top = TopLeft.Y;
        }

        private void UpdateDropdownContent()
        {
            IsDropdownContentValid = true;

            using (DropdownStackPanel.AllowChangingContentTemporarily())
            {
                foreach (MGElement Element in DropdownStackPanel.Children.ToList())
                {
                    DropdownStackPanel.TryRemoveChild(Element);
                }

                if (TemplatedItems != null)
                {
                    foreach (TemplatedElement<TItemType, MGButton> UIItem in TemplatedItems)
                    {
                        DropdownStackPanel.TryAddChild(UIItem.Element);
                    }
                }
            }

            Rectangle Viewport = GetDesktop().ValidScreenBounds;
            Point TopLeft = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, LayoutBounds.BottomLeft());
            float ActualScale = Dropdown.Scale > 0 ? Dropdown.Scale : 1.0f;
            int ActualAvailableWidth = Math.Max(0, (int)(Viewport.Width / ActualScale));
            int AvailableHeightScreenSpace = Viewport.Bottom - TopLeft.Y;
            int ActualAvailableHeight = Math.Max(0, (int)(AvailableHeightScreenSpace / ActualScale));
            var (MinSize, MaxSize) = MGWindow.GetEffectiveSizeConstraints(
                Math.Max(ActualWidth, MinDropdownWidth),
                MinDropdownHeight,
                Math.Min(MaxDropdownWidth, ActualAvailableWidth),
                Math.Min(MaxDropdownHeight, ActualAvailableHeight));

            Size DesiredSize = Dropdown.ComputeContentSize(MinSize.Width, MinSize.Height, MaxSize.Width, MaxSize.Height);
            PositionDropdown(DesiredSize.Width);

            Size ActualSize = Dropdown.ApplySizeToContent(SizeToContent.WidthAndHeight, MinSize.Width, MinSize.Height, MaxSize.Width, MaxSize.Height, false);
            PositionDropdown(ActualSize.Width);
            Dropdown.ValidateWindowSizeAndPosition();
            Dropdown.UpdateLayout(new Rectangle(Dropdown.Left, Dropdown.Top, Dropdown.WindowWidth, Dropdown.WindowHeight));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsDropdownOpen;
        public bool IsDropdownOpen
        {
            get => _IsDropdownOpen;
            set
            {
                if (_IsDropdownOpen != value)
                {
                    CancelEventArgs e = new CancelEventArgs(false);
                    DropdownOpening?.Invoke(this, e);
                    if (e.Cancel)
                    {
                        return;
                    }

                    _IsDropdownOpen = value;

                    if (IsDropdownOpen)
                    {
                        Point TopLeft = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, LayoutBounds.BottomLeft());
                        Dropdown.Left = TopLeft.X;
                        Dropdown.Top = TopLeft.Y;
                        UpdateDropdownContent();
                        ParentWindow.AddNestedWindow(Dropdown);

                        //  Opening shows the committed selection as is: no navigation target, and no spoofed hover on the selected row.
                        SetNavigationTarget(null);
                        ((INavigationTargetVisibilityHandler)this).EnsureNavigationTargetVisible();
                    }
                    else
                    {
                        ParentWindow.RemoveNestedWindow(Dropdown);
                        SetNavigationTarget(null);
                    }

                    NPC(nameof(IsDropdownOpen));
                    DropdownOpened?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler<CancelEventArgs> DropdownOpening;
        public event EventHandler<EventArgs> DropdownOpened;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color _DropdownArrowColor;
        /// <summary>The color of the inverted triangle on the right-side of this <see cref="MGComboBox{TItemType}"/>.<para/>
        /// Default value: <see cref="MGTheme.DropdownArrowColor"/><para/>
        /// See also:<br/><see cref="MGWindow.Theme"/><br/><see cref="MGDesktop.Theme"/></summary>
        public Color DropdownArrowColor
        {
            get => _DropdownArrowColor;
            set
            {
                if (_DropdownArrowColor != value)
                {
                    _DropdownArrowColor = value;
                    NPC(nameof(DropdownArrowColor));
                }
            }
        }

        private void ManagedSetContent(MGElement Content)
        {
            using (AllowChangingContentTemporarily())
            {
                SetContent(Content);
            }
        }

        private MGContentPresenter DropdownHeaderPresenter { get; set; }
        private MGContentPresenter DropdownFooterPresenter { get; set; }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
        {
            BorderElement = Structure.Parts[BorderPartName] as MGBorder;
            DropdownArrowElement = Structure.Parts[DropdownArrowPartName] as MGContentPresenter;
            Dropdown = Structure.Parts[DropdownWindowPartName] as MGWindow;
            //  Excluded from window click-activation (decision utilisateur, Docs/input-window-activation-design.md section 3.a):
            //  the dropdown is a nested popup window; clicking an item inside it must not reorder nested windows.
            Dropdown.ActivatesOnClick = false;
            DropdownHeaderPresenter = Structure.Parts[DropdownHeaderPresenterPartName] as MGContentPresenter;
            DropdownFooterPresenter = Structure.Parts[DropdownFooterPresenterPartName] as MGContentPresenter;
            DropdownStackPanel = Structure.Parts[DropdownItemsPanelPartName] as MGStackPanel;
            DropdownScrollViewer = Structure.Parts[DropdownScrollViewerPartName] as MGScrollViewer;
            DropdownDockPanel = Structure.Parts[DropdownDockPanelPartName] as MGDockPanel;

            bool needsBorderNotifications = BorderComponent == null || !ReferenceEquals(BorderComponent.Element, BorderElement);
            EnsureComponentBinding(() => BorderComponent, value => BorderComponent = value, BorderElement, MGComponentBase.Create);
            if (needsBorderNotifications)
            {
                BorderElement.OnBorderBrushChanged += (sender, e) => { NPC(nameof(BorderBrush)); };
                BorderElement.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(BorderThickness)); };
                BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };
            }

            bool needsDropdownArrowHandlers = DropdownArrowComponent == null || !ReferenceEquals(DropdownArrowComponent.Element, DropdownArrowElement);
            EnsureComponentBinding(() => DropdownArrowComponent, value => DropdownArrowComponent = value, DropdownArrowElement,
                element => new(element, false, true, false, true, true, false, false,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Right, VerticalAlignment.Center, ComponentSize.Size)));

            if (needsDropdownArrowHandlers)
            {
                DropdownArrowElement.OnEndingDraw += (sender, e) =>
                {
                    Rectangle ArrowElementFullBounds = DropdownArrowElement.LayoutBounds;
                    Rectangle ArrowPartBounds = ApplyAlignment(ArrowElementFullBounds, HorizontalAlignment.Center, VerticalAlignment.Center, new Size(DropdownArrowWidth, DropdownArrowHeight));
                    UISymbolDrawing.DrawFilledTriangleArrow(e.DA.DT, e.DA.Offset.ToVector2(), ArrowPartBounds, UITriangleArrowDirection.Down,
                        DropdownArrowColor * e.DA.Opacity);
                };
            }

            Dropdown.WindowMouseHandler.LMBReleasedInside += (sender, e) =>
            {
                if (HoveredItem != null)
                {
                    e.SetHandledBy(Dropdown, false);
                    SelectedTemplatedItem = HoveredItem;
                    IsDropdownOpen = false;
                }
            };
            Dropdown.WindowMouseHandler.ReleasedOutside += (sender, e) =>
            {
                if (IsDropdownOpen)
                {
                    Point LayoutSpacePosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                    if (!Dropdown.RenderBounds.ContainsInclusive(LayoutSpacePosition))
                    {
                        IsDropdownOpen = false;
                        e.SetHandledBy(Dropdown, false);
                    }
                }
            };
            Dropdown.HoveredElementChanged += (sender, e) => { UpdateHoveredDropdownItem(); };

            if (TemplatedItems != null)
            {
                foreach (TemplatedElement<TItemType, MGButton> item in TemplatedItems)
                {
                    if (item?.Element != null)
                    {
                        ApplyDefaultDropdownButtonSettings(item.Element);
                    }
                }
            }
        }

        protected internal override void ApplyControlTemplate(bool IsThemeRefresh)
        {
            base.ApplyControlTemplate(IsThemeRefresh);

            //  Items are only added to DropdownStackPanel when the dropdown opens (UpdateDropdownContent). Until then (dropdown never opened, or items
            //  regenerated while it was closed) they are outside every visual tree and no theme refresh reaches them, so refresh their template here.
            //  Items already added stay in the panel after closing and are also refreshed through the dropdown window's own scope, where the second
            //  pass changes nothing. MGComboBox deliberately has no OnThemeChanged override: its theme-driven values flow through templates, see
            //  ControlTemplateInfrastructureTests.
            if (IsThemeRefresh && TemplatedItems != null)
            {
                foreach (TemplatedElement<TItemType, MGButton> item in TemplatedItems)
                {
                    item?.Element?.ApplyControlTemplate(true);
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _DropdownHeader;
        /// <summary>Optional content that is displayed at the top of the <see cref="Dropdown"/> window.<para/>
        /// Recommended to use a bottom margin or padding to visually separate the <see cref="DropdownHeader"/> from the items list.<para/>
        /// See also: <see cref="DropdownFooter"/></summary>
        public MGElement DropdownHeader
        {
            get => _DropdownHeader;
            set
            {
                if (_DropdownHeader != value)
                {
                    _DropdownHeader = value;
                    using (DropdownHeaderPresenter.AllowChangingContentTemporarily())
                    {
                        DropdownHeaderPresenter.SetContent(DropdownHeader);
                    }
                    NPC(nameof(DropdownHeader));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _DropdownFooter;
        /// <summary>Optional content that is displayed at the bottom of the <see cref="Dropdown"/> window.<para/>
        /// Recommended to use a top margin or padding to visually separate the <see cref="DropdownFooter"/> from the items list.<para/>
        /// See also: <see cref="DropdownHeader"/></summary>
        public MGElement DropdownFooter
        {
            get => _DropdownFooter;
            set
            {
                if (_DropdownFooter != value)
                {
                    _DropdownFooter = value;
                    using (DropdownFooterPresenter.AllowChangingContentTemporarily())
                    {
                        DropdownFooterPresenter.SetContent(DropdownFooter);
                    }
                    NPC(nameof(DropdownFooter));
                }
            }
        }
        #endregion Dropdown

        public MGComboBox(MGWindow Window)
            : this(Window, new(1), MGUniformBorderBrush.Black) { }

        public MGComboBox(MGWindow Window, Thickness BorderThickness, IFillBrush BorderBrush)
            : this(Window, BorderThickness, new MGUniformBorderBrush(BorderBrush)) { }

        public MGComboBox(MGWindow Window, Thickness BorderThickness, IBorderBrush BorderBrush)
            : base(Window, MGElementType.ComboBox)
        {
            using (BeginInitializing())
            {
                AutoWidthFromContent = GetTheme().DefaultComboBoxAutoWidthFromContent;
                IsFocusable = true;
                HorizontalContentAlignment = HorizontalAlignment.Left;
                VerticalContentAlignment = VerticalAlignment.Center;

                CanChangeContent = false;
                DefaultControlTemplateName = MGControlTemplateCatalog.ComboBoxTemplateName;

                SelfOrParentWindow.ScaleChanged += (sender, e) =>
                {
                    Dropdown.Scale = e.NewValue;
                };

                DropdownItemTemplate = item => MGControlTemplateCatalog.CreateDefaultComboBoxDropdownItem(this, item);
                SelectedItemTemplate = item => MGControlTemplateCatalog.CreateDefaultComboBoxSelectedItemContent(Window, item);

                MouseHandler.LMBReleasedInside += (sender, e) =>
                {
                    IsDropdownOpen = !IsDropdownOpen;
                    e.SetHandledBy(this, false);
                };

                DropdownItemControlTemplateName = MGControlTemplateCatalog.ComboBoxDropdownItemTemplateName;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TemplatedElement<TItemType, MGButton> _NavigationTarget;

        /// <summary>Keyboard navigation inside the open dropdown highlights its target with the item's Selected visual state (the same fill as the
        /// committed selection) rather than a spoofed hover. The committed selection is only displayed as selected while no other item is targeted.</summary>
        private void SetNavigationTarget(TemplatedElement<TItemType, MGButton> item)
        {
            _NavigationTarget = item;
            HoveredItem = item;
            RefreshDropdownItemSelectionVisuals();
        }

        private void RefreshDropdownItemSelectionVisuals()
        {
            if (TemplatedItems == null)
            {
                return;
            }

            MGButton highlighted = (_NavigationTarget ?? SelectedTemplatedItem)?.Element;
            foreach (TemplatedElement<TItemType, MGButton> item in TemplatedItems)
            {
                if (item?.Element != null)
                {
                    item.Element.IsSelected = ReferenceEquals(item.Element, highlighted);
                }
            }
        }

        void INavigationTargetVisibilityHandler.EnsureNavigationTargetVisible()
        {
            MGButton target = (_NavigationTarget ?? HoveredItem ?? SelectedTemplatedItem)?.Element;
            if (IsDropdownOpen && target != null)
            {
                DropdownScrollViewer?.EnsureElementVisible(target);
            }
        }

        private bool TryAdjustClosedSelection(UINavigationAction action)
        {
            if (TemplatedItems == null || TemplatedItems.Count == 0)
            {
                return false;
            }

            //  -1 when nothing is selected, so that MoveDown / MoveNext select the first row instead of skipping it.
            int currentIndex = SelectedIndex;
            int nextIndex = GetNextNavigationIndex(currentIndex, TemplatedItems.Count, action);
            if (nextIndex < 0 || nextIndex == currentIndex)
            {
                return false;
            }

            SelectedIndex = nextIndex;
            return true;
        }

        private bool TryAdjustOpenSelection(UINavigationAction action)
        {
            if (!IsDropdownOpen || TemplatedItems == null || TemplatedItems.Count == 0)
            {
                return false;
            }

            //  Start from the keyboard target, else the hovered row, else the committed selection: -1 when there is none, so that the first
            //  MoveDown / MoveNext targets the first row instead of skipping it.
            TemplatedElement<TItemType, MGButton> anchor = _NavigationTarget ?? HoveredItem;
            int currentIndex = anchor != null ? TemplatedItems.IndexOf(anchor) : SelectedIndex;
            int nextIndex = GetNextNavigationIndex(currentIndex, TemplatedItems.Count, action);
            if (nextIndex < 0)
            {
                return false;
            }

            SetNavigationTarget(TemplatedItems[nextIndex]);
            return true;
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            if (IsDropdownOpen)
            {
                return action switch
                {
                    UINavigationAction.Submit when (_NavigationTarget ?? HoveredItem) != null => (SelectedTemplatedItem = _NavigationTarget ?? HoveredItem) != null && !(IsDropdownOpen = false),
                    UINavigationAction.Cancel => !(IsDropdownOpen = false),
                    UINavigationAction.MoveUp or UINavigationAction.MoveDown or UINavigationAction.MoveNext or UINavigationAction.MovePrevious or UINavigationAction.Home or UINavigationAction.End or UINavigationAction.PageUp or UINavigationAction.PageDown => TryAdjustOpenSelection(action),
                    _ => false
                };
            }

            return action switch
            {
                UINavigationAction.Submit => (IsDropdownOpen = true),
                UINavigationAction.MoveUp or UINavigationAction.MoveDown or UINavigationAction.MoveNext or UINavigationAction.MovePrevious or UINavigationAction.Home or UINavigationAction.End or UINavigationAction.PageUp or UINavigationAction.PageDown => TryAdjustClosedSelection(action),
                _ => false
            };
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            base.UpdateSelf(UA);

            if (IsDropdownOpen)
            {
                if (!IsDropdownContentValid)
                {
                    UpdateDropdownContent();
                }

                //  Example scenario: ComboBox is inside a vertically-scrolling ScrollViewer
                //  ComboBox is visible. User clicks it to open dropdown, then they scroll the ScrollViewer up until the ComboBox is no longer visible
                //  Should we auto-hide (or close) the dropdown?
                //Dropdown.Visibility = RecentDrawWasClipped ? Visibility.Collapsed : Visibility.Visible;

                PositionDropdown(Dropdown.WindowWidth);
                Dropdown.ValidateWindowSizeAndPosition();
            }
        }

        //  This method is invoked via reflection in MGUI.Core.UI.XAML.Controls.ComboBox.ApplyDerivedSettings.
        //  Do not modify the method signature.
        internal void LoadSettings(ComboBox Settings, bool IncludeContent)
        {
            Settings.Border.ApplySettings(this, BorderComponent.Element, false);
            Settings.DropdownArrow.ApplySettings(this, DropdownArrowComponent.Element, IncludeContent);

            if (Settings.DropdownArrowColor.HasValue)
            {
                DropdownArrowColor = Settings.DropdownArrowColor.Value.ToXNAColor();
            }

            if (Settings.MinDropdownWidth.HasValue)
            {
                MinDropdownWidth = Settings.MinDropdownWidth.Value;
            }

            if (Settings.MaxDropdownWidth.HasValue)
            {
                MaxDropdownWidth = Settings.MaxDropdownWidth.Value;
            }

            if (Settings.MinDropdownHeight.HasValue)
            {
                MinDropdownHeight = Settings.MinDropdownHeight.Value;
            }

            if (Settings.MaxDropdownHeight.HasValue)
            {
                MaxDropdownHeight = Settings.MaxDropdownHeight.Value;
            }

            Settings.Dropdown?.ApplySettings(Dropdown.Parent, Dropdown, false);
            Settings.DropdownScrollViewer?.ApplySettings(DropdownScrollViewer.Parent, DropdownScrollViewer, false);
            Settings.DropdownStackPanel?.ApplySettings(DropdownStackPanel.Parent, DropdownStackPanel, false);

            if (Settings.DropdownHeader != null)
            {
                DropdownHeader = Settings.DropdownHeader.ToElement<MGElement>(Dropdown, DropdownHeaderPresenter);
            }

            if (Settings.DropdownFooter != null)
            {
                DropdownFooter = Settings.DropdownFooter.ToElement<MGElement>(Dropdown, DropdownFooterPresenter);
            }

            if (Settings.Items?.Any() == true)
            {
                List<TItemType> TempItems = new();
                Type TargetType = typeof(TItemType);
                foreach (object Item in Settings.Items)
                {
                    if (TargetType.IsAssignableFrom(Item.GetType()))
                    {
                        TItemType Value = (TItemType)Item;
                        TempItems.Add(Value);
                    }
                }

                if (TempItems.Any())
                {
                    SetItemsSource(TempItems);
                }
            }

            if (Settings.DropdownItemTemplate != null)
            {
                DropdownItemTemplate = (Item) =>
                {
                    MGElement Content = Settings.DropdownItemTemplate.GetContent(Dropdown, this, Item, x =>
                    {
                        if (x is MGButton ButtonContent)
                        {
                            ApplyDefaultDropdownButtonSettings(ButtonContent);
                        }
                    });

                    if (Content is MGButton ButtonContent)
                    {
                        return ButtonContent;
                    }
                    else
                    {
                        MGButton Button = CreateDefaultDropdownButton();
                        Button.SetContent(Content);
                        return Button;
                    }
                };
            }

            if (Settings.SelectedItemTemplate != null)
            {
                SelectedItemTemplate = (Item) => Settings.SelectedItemTemplate.GetContent(SelfOrParentWindow, this, Item);
            }
            //  Use the same template for the selected item if only a DropdownItemTemplate is specified
            else if (Settings.DropdownItemTemplate != null)
            {
                SelectedItemTemplate = (Item) => Settings.DropdownItemTemplate.GetContent(SelfOrParentWindow, this, Item);
            }

            if (Settings.SelectedIndex.HasValue)
            {
                SelectedIndex = Settings.SelectedIndex.Value;
            }
        }
    }
}
