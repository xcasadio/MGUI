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
using System.Diagnostics;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI
{
    public class MGTabControl : MGHeaderedContentPresenter
    {
        public const string BorderPartName = "PART_Border";
        public const string HeadersPanelPartName = "PART_HeadersPanel";
        internal const string HeaderTemplateOwnerMetadataKey = "TabControl.Owner";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(BorderPartName, typeof(MGBorder));
            yield return new(HeadersPanelPartName, typeof(MGStackPanel));
        }

        internal static int GetAdjacentTabIndex(int currentIndex, int count, UINavigationAction action)
        {
            if (count <= 0)
            {
                return -1;
            }

            int normalizedIndex = currentIndex < 0 ? 0 : currentIndex;
            return action switch
            {
                UINavigationAction.MoveLeft or UINavigationAction.MoveUp or UINavigationAction.ShoulderPrevious => Math.Max(0, normalizedIndex - 1),
                UINavigationAction.MoveRight or UINavigationAction.MoveDown or UINavigationAction.ShoulderNext => Math.Min(count - 1, normalizedIndex + 1),
                UINavigationAction.Home => 0,
                UINavigationAction.End => count - 1,
                _ => normalizedIndex
            };
        }

        protected override void SetContentVirtual(MGElement Value)
        {
            if (_Content != Value)
            {
                if (!CanChangeContent)
                {
                    throw new InvalidOperationException($"Cannot set {nameof(MGSingleContentHost)}.{nameof(Content)} while {nameof(CanChangeContent)} is false.");
                }

                //  ContentAdded/ContentRemoved is already invoked when AddTab or RemoveTab

                //_Content?.SetParent(null);
                //InvokeContentRemoved(_Content);
                _Content = Value;
                //_Content?.SetParent(this);
                //InvokeContentAdded(_Content);
                LayoutChanged(this, true);
                NPC(nameof(Content));
                NPC(nameof(HasContent));
            }
        }

        public override IReadOnlyList<MGElement> GetVisualTreeChildren(bool IncludeInactive, bool IncludeActive)
        {
            List<MGElement> result = new(_Tabs.Count + 1);
            if (IncludeInactive)
            {
                foreach (MGTabItem Item in _Tabs)
                {
                    if (!Item.IsTabSelected)
                    {
                        result.Add(Item);
                    }
                }
            }
            if (IncludeActive && SelectedTab != null)
            {
                result.Add(SelectedTab);
            }

            return result;
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

        #region Tab Headers
        /// <summary>The <see cref="MGStackPanel"/> that contains the tab headers.<para/>
        /// See also: <see cref="TabHeaderPosition"/></summary>
        public MGStackPanel HeadersPanelElement { get; private set; }

        public Dock TabHeaderPosition
        {
            get => HeaderPosition;
            set => HeaderPosition = value;
        }

        /// <summary>The background brush of the entire header region of this <see cref="MGTabControl"/>. This is rendered behind the tab headers.<para/>
        /// To change the background of a specific tab, consider setting the <see cref="UnselectedTabHeaderTemplate"/> and <see cref="SelectedTabHeaderTemplate"/>.</summary>
        public VisualStateFillBrush HeaderAreaBackground
        {
            get => HeadersPanelElement.BackgroundBrush;
            set
            {
                if (HeadersPanelElement.BackgroundBrush != value)
                {
                    HeadersPanelElement.BackgroundBrush = value;
                    NPC(nameof(HeaderAreaBackground));
                }
            }
        }

        private bool ManagedAddHeadersPanelChild(MGSingleContentHost NewItem)
        {
            using (HeadersPanelElement.AllowChangingContentTemporarily())
            {
                return HeadersPanelElement.TryAddChild(NewItem);
            }
        }

        private bool ManagedReplaceHeadersPanelChild(MGSingleContentHost OldItem, MGSingleContentHost NewItem)
        {
            using (HeadersPanelElement.AllowChangingContentTemporarily())
            {
                return HeadersPanelElement.TryReplaceChild(OldItem, NewItem);
            }
        }

        private bool ManagedRemoveHeadersPanelChild(MGSingleContentHost ToRemove)
        {
            using (HeadersPanelElement.AllowChangingContentTemporarily())
            {
                return HeadersPanelElement.TryRemoveChild(ToRemove);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Func<MGTabItem, MGButton> _SelectedTabHeaderTemplate;
        /// <summary>Optional override factory that creates the wrapper element hosting the given <see cref="MGTabItem"/>'s <see cref="MGTabItem.Header"/> for the selected tab.<para/>
        /// If null, the default header wrapper path is used and styled through <see cref="SelectedTabHeaderControlTemplateName"/>.</summary>
        public Func<MGTabItem, MGButton> SelectedTabHeaderTemplate
        {
            get => _SelectedTabHeaderTemplate;
            set
            {
                if (_SelectedTabHeaderTemplate != value)
                {
                    _SelectedTabHeaderTemplate = value;
                    foreach (KeyValuePair<MGTabItem, MGButton> KVP in ActualTabHeaders.ToList())
                    {
                        MGTabItem Tab = KVP.Key;
                        if (Tab.IsTabSelected)
                        {
                            UpdateHeaderWrapper(Tab);
                        }
                    }
                    NPC(nameof(SelectedTabHeaderTemplate));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Func<MGTabItem, MGButton> _UnselectedTabHeaderTemplate;
        /// <summary>Optional override factory that creates the wrapper element hosting the given <see cref="MGTabItem"/>'s <see cref="MGTabItem.Header"/> for tabs that aren't selected.<para/>
        /// If null, the default header wrapper path is used and styled through <see cref="UnselectedTabHeaderControlTemplateName"/>.</summary>
        public Func<MGTabItem, MGButton> UnselectedTabHeaderTemplate
        {
            get => _UnselectedTabHeaderTemplate;
            set
            {
                if (_UnselectedTabHeaderTemplate != value)
                {
                    _UnselectedTabHeaderTemplate = value;
                    foreach (KeyValuePair<MGTabItem, MGButton> KVP in ActualTabHeaders.ToList())
                    {
                        MGTabItem Tab = KVP.Key;
                        if (!Tab.IsTabSelected)
                        {
                            UpdateHeaderWrapper(Tab);
                        }
                    }
                    NPC(nameof(UnselectedTabHeaderTemplate));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _SelectedTabHeaderControlTemplateName;
        public string SelectedTabHeaderControlTemplateName
        {
            get => _SelectedTabHeaderControlTemplateName;
            set
            {
                if (_SelectedTabHeaderControlTemplateName != value)
                {
                    _SelectedTabHeaderControlTemplateName = value;
                    foreach (MGTabItem tab in ActualTabHeaders.Keys.ToList())
                    {
                        if (tab.IsTabSelected)
                        {
                            UpdateHeaderWrapper(tab);
                        }
                    }
                    NPC(nameof(SelectedTabHeaderControlTemplateName));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _UnselectedTabHeaderControlTemplateName;
        public string UnselectedTabHeaderControlTemplateName
        {
            get => _UnselectedTabHeaderControlTemplateName;
            set
            {
                if (_UnselectedTabHeaderControlTemplateName != value)
                {
                    _UnselectedTabHeaderControlTemplateName = value;
                    foreach (MGTabItem tab in ActualTabHeaders.Keys.ToList())
                    {
                        if (!tab.IsTabSelected)
                        {
                            UpdateHeaderWrapper(tab);
                        }
                    }
                    NPC(nameof(UnselectedTabHeaderControlTemplateName));
                }
            }
        }

        private bool UsesCustomHeaderFactories => SelectedTabHeaderTemplate != null || UnselectedTabHeaderTemplate != null;

        private MGButton CreateDefaultHeaderWrapper(MGTabItem Tab)
        {
            MGButton button = new(Tab.SelfOrParentWindow, _ => Tab.IsTabSelected = true)
            {
                IsFocusable = false,
            };
            button.Metadata[HeaderTemplateOwnerMetadataKey] = this;
            ApplyHeaderWrapperTemplate(button, Tab.IsTabSelected);
            return button;
        }

        private MGButton CreateHeaderWrapper(MGTabItem Tab)
        {
            MGButton wrapper = Tab.IsTabSelected
                ? SelectedTabHeaderTemplate?.Invoke(Tab)
                : UnselectedTabHeaderTemplate?.Invoke(Tab);

            wrapper ??= CreateDefaultHeaderWrapper(Tab);
            wrapper.Metadata[HeaderTemplateOwnerMetadataKey] = this;
            wrapper.IsFocusable = false;
            ApplyHeaderWrapperTemplate(wrapper, Tab.IsTabSelected);
            return wrapper;
        }

        private void ApplyHeaderWrapperTemplate(MGButton HeaderWrapper, bool IsSelected)
        {
            if (HeaderWrapper == null)
            {
                return;
            }

            if (UsesCustomHeaderFactories)
            {
                HeaderWrapper.IsSelected = IsSelected;
                return;
            }

            HeaderWrapper.ControlTemplateName = IsSelected
                ? SelectedTabHeaderControlTemplateName
                : UnselectedTabHeaderControlTemplateName;

            HeaderWrapper.IsSelected = IsSelected;
            HeaderWrapper.ApplyControlTemplate(false);
        }

        private void UpdateHeaderWrapper(MGTabItem Tab)
        {
            if (Tab != null && ActualTabHeaders.TryGetValue(Tab, out MGButton OldHeaderWrapper))
            {
                MGButton NewHeaderWrapper = CreateHeaderWrapper(Tab);
                if (ManagedReplaceHeadersPanelChild(OldHeaderWrapper, NewHeaderWrapper))
                {
                    OldHeaderWrapper.SetContent(null as MGElement);
                    NewHeaderWrapper.SetContent(Tab.Header);
                    NewHeaderWrapper.InvalidateLayoutTree();
                    ActualTabHeaders[Tab] = NewHeaderWrapper;
                }

                UpdateHeadersPanelPreferredSize();
            }
        }

        private Dictionary<MGTabItem, MGButton> ActualTabHeaders { get; }

        private void UpdateHeadersPanelPreferredSize()
        {
            if (HeadersPanelElement == null)
            {
                return;
            }

            const int HeaderMeasurementLimit = 8192;
            Size measurementBounds = new(HeaderMeasurementLimit, HeaderMeasurementLimit);

            if (TabHeaderPosition == Dock.Left || TabHeaderPosition == Dock.Right)
            {
                int maxWidth = 0;
                foreach (MGButton HeaderWrapper in ActualTabHeaders.Values)
                {
                    HeaderWrapper.UpdateMeasurement(measurementBounds, out _, out Thickness FullSize, out _, out _);
                    maxWidth = Math.Max(maxWidth, FullSize.Width);
                }

                HeadersPanelElement.PreferredWidth = maxWidth > 0 ? maxWidth : null;
                HeadersPanelElement.PreferredHeight = null;
            }
            else
            {
                int maxHeight = 0;
                foreach (MGButton HeaderWrapper in ActualTabHeaders.Values)
                {
                    HeaderWrapper.UpdateMeasurement(measurementBounds, out _, out Thickness FullSize, out _, out _);
                    maxHeight = Math.Max(maxHeight, FullSize.Height);
                }

                HeadersPanelElement.PreferredWidth = null;
                HeadersPanelElement.PreferredHeight = maxHeight > 0 ? maxHeight : null;
            }
        }
        #endregion Tab Headers

        private void ApplyHeadersPanelSettings()
        {
            if (HeadersPanelElement == null)
            {
                return;
            }

            Dock Position = TabHeaderPosition;
            switch (Position)
            {
                case Dock.Left:
                    HeadersPanelElement.Orientation = Orientation.Vertical;
                    HeadersPanelElement.HorizontalAlignment = HorizontalAlignment.Right;
                    HeadersPanelElement.VerticalAlignment = VerticalAlignment.Stretch;
                    HeadersPanelElement.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    HeadersPanelElement.VerticalContentAlignment = VerticalAlignment.Top;
                    break;
                case Dock.Top:
                    HeadersPanelElement.Orientation = Orientation.Horizontal;
                    HeadersPanelElement.HorizontalAlignment = HorizontalAlignment.Stretch;
                    HeadersPanelElement.VerticalAlignment = VerticalAlignment.Bottom;
                    HeadersPanelElement.HorizontalContentAlignment = HorizontalAlignment.Left;
                    HeadersPanelElement.VerticalContentAlignment = VerticalAlignment.Stretch;
                    break;
                case Dock.Right:
                    HeadersPanelElement.Orientation = Orientation.Vertical;
                    HeadersPanelElement.HorizontalAlignment = HorizontalAlignment.Left;
                    HeadersPanelElement.VerticalAlignment = VerticalAlignment.Stretch;
                    HeadersPanelElement.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    HeadersPanelElement.VerticalContentAlignment = VerticalAlignment.Top;
                    break;
                case Dock.Bottom:
                    HeadersPanelElement.Orientation = Orientation.Horizontal;
                    HeadersPanelElement.HorizontalAlignment = HorizontalAlignment.Stretch;
                    HeadersPanelElement.VerticalAlignment = VerticalAlignment.Top;
                    HeadersPanelElement.HorizontalContentAlignment = HorizontalAlignment.Left;
                    HeadersPanelElement.VerticalContentAlignment = VerticalAlignment.Stretch;
                    break;
            }
        }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
        {
            HeadersPanelElement = Structure.Parts[HeadersPanelPartName] as MGStackPanel;
            BorderElement = Structure.Parts[BorderPartName] as MGBorder;

            bool needsBorderNotifications = BorderComponent == null || !ReferenceEquals(BorderComponent.Element, BorderElement);
            EnsureComponentBinding(() => BorderComponent, value => BorderComponent = value, BorderElement, MGComponentBase.Create);
            if (needsBorderNotifications)
            {
                BorderElement.OnBorderBrushChanged += (sender, e) => { NPC(nameof(BorderBrush)); };
                BorderElement.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(BorderThickness)); };
                BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };
            }

            using (HeaderPresenter.AllowChangingContentTemporarily())
            {
                HeaderPresenter.SetContent(HeadersPanelElement);
            }

            if (_Tabs != null && ActualTabHeaders != null)
            {
                using (HeadersPanelElement.AllowChangingContentTemporarily())
                {
                    _ = HeadersPanelElement.TryRemoveAll();
                    foreach (MGTabItem tab in _Tabs)
                    {
                        if (ActualTabHeaders.TryGetValue(tab, out MGButton headerWrapper))
                        {
                            _ = HeadersPanelElement.TryAddChild(headerWrapper);
                        }
                    }
                }
            }

            HeadersPanelElement.CanChangeContent = false;
            ApplyHeadersPanelSettings();
            UpdateHeadersPanelPreferredSize();
        }

        #region Tabs
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<MGTabItem> _Tabs { get; }
        public IReadOnlyList<MGTabItem> Tabs => _Tabs;

        public void RemoveTab(MGTabItem Tab)
        {
            if (_Tabs.Contains(Tab))
            {
                MGButton TabHeader = ActualTabHeaders[Tab];
                ActualTabHeaders.Remove(Tab);
                int TabIndex = _Tabs.IndexOf(Tab);

                _Tabs.Remove(Tab);
                InvokeContentRemoved(Tab);

                ManagedRemoveHeadersPanelChild(TabHeader);
                UpdateHeadersPanelPreferredSize();

                if (SelectedTab == Tab)
                {
                    int NewSelectedTabIndex = Math.Max(0, TabIndex - 1); // Focus to left of the closed tab
                    _ = TrySelectTabAtIndex(NewSelectedTabIndex);
                }
            }
        }

        public MGTabItem AddTab(string TabHeader, MGElement TabContent)
            => AddTab(new MGTextBlock(ParentWindow, TabHeader), TabContent);

        public MGTabItem AddTab(MGElement TabHeader, MGElement TabContent)
        {
            MGTabItem Tab = new(this, TabHeader, TabContent);

            MGButton HeaderWrapper = CreateHeaderWrapper(Tab);
            HeaderWrapper.SetContent(TabHeader);
            ActualTabHeaders.Add(Tab, HeaderWrapper);

            _Tabs.Add(Tab);
            InvokeContentAdded(Tab);

            ManagedAddHeadersPanelChild(HeaderWrapper);
            UpdateHeadersPanelPreferredSize();

            if (SelectedTab == null)
            {
                _ = TrySelectTab(Tab);
            }

            return Tab;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGTabItem _SelectedTab;
        public MGTabItem SelectedTab { get => _SelectedTab; }
        public int SelectedTabIndex => _SelectedTab == null ? -1 : _Tabs.IndexOf(_SelectedTab);

        /// <summary>Invoked just before <see cref="SelectedTab"/> changes. Argument value is the new tab being selected. This event allows cancellation.</summary>
        public event EventHandler<CancelEventArgs<MGTabItem>> SelectedTabChanging;
        /// <summary>Invoked when <see cref="SelectedTab"/> changes to a different <see cref="MGTabItem"/></summary>
        public event EventHandler<EventArgs<MGTabItem>> SelectedTabChanged;

        /// <summary>Attempts to set the given <paramref name="Tab"/> as the <see cref="SelectedTab"/>.<para/>
        /// To deselect a tab, use <see cref="TryDeselectTab(MGTabItem, bool)"/> rather than a null <paramref name="Tab"/> parameter.</summary>
        /// <param name="Tab">Cannot be null, and should be a tab that belongs to this <see cref="MGTabControl"/> (I.E. it was created via <see cref="AddTab(MGElement, MGElement)"/>)</param>
        /// <returns>False if unable to select the given <paramref name="Tab"/>, such as if the value was null, or it belongs to a different <see cref="MGTabControl"/>, or the action was cancelled by <see cref="SelectedTabChanging"/> event.</returns>
        public bool TrySelectTab(MGTabItem Tab)
        {
            if (Tab != null && _Tabs.Contains(Tab) && Tab.TabControl == this && Tab != SelectedTab)
            {
                if (SelectedTabChanging != null)
                {
                    CancelEventArgs<MGTabItem> CancelArgs = new(Tab);
                    SelectedTabChanging.Invoke(this, CancelArgs);
                    if (CancelArgs.Cancel)
                    {
                        return false;
                    }
                }

                MGTabItem Previous = SelectedTab;
                _SelectedTab = Tab;

                UpdateHeaderWrapper(Previous);
                UpdateHeaderWrapper(SelectedTab);
                HeadersPanelElement?.InvalidateLayoutTree();
                LayoutChanged(this, true);

                SetContent(SelectedTab);
                NPC(nameof(SelectedTab));
                NPC(nameof(SelectedTabIndex));
                Previous?.NPC(nameof(MGTabItem.IsTabSelected));
                _SelectedTab?.NPC(nameof(MGTabItem.IsTabSelected));
                SelectedTabChanged?.Invoke(this, new(Previous, SelectedTab));
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TrySelectTabAtIndex(int Index)
        {
            if (Index >= 0 && Index < _Tabs.Count)
            {
                return TrySelectTab(_Tabs[Index]);
            }
            else
            {
                return false;
            }
        }

        /// <summary>Attempts to deselect the given <paramref name="Tab"/>. Does nothing if the <paramref name="Tab"/> is not already selected or if there are no other tabs to select in place of it.</summary>
        /// <param name="Tab">The tab to deselect.</param>
        /// <param name="FocusTabToRight">If true, will attempt to select the tab to the right of the tab being deselected.<br/>
        /// If false, will attempt to select the tab to the left of the tab being deselected.</param>
        public bool TryDeselectTab(MGTabItem Tab, bool FocusTabToRight)
        {
            if (Tab == null || Tab != SelectedTab || _Tabs.Count <= 1)
            {
                return false;
            }

            int TabIndex = _Tabs.IndexOf(Tab);
            if (TabIndex < 0)
            {
                return false;
            }

            int DesiredIndex = FocusTabToRight ? TabIndex + 1 : TabIndex - 1;
            int ActualIndex = (DesiredIndex + _Tabs.Count) % _Tabs.Count;
            return TrySelectTabAtIndex(ActualIndex);
        }
        #endregion Tabs

        public MGTabControl(MGWindow Window)
            : base(Window, MGElementType.TabControl)
        {
            using (BeginInitializing())
            {
                HeaderChanging += (sender, e) => { e.Cancel = true; }; // Disallow changing the Header

                HeaderPresenter.HorizontalAlignment = HorizontalAlignment.Stretch;
                HeaderPresenter.VerticalAlignment = VerticalAlignment.Stretch;
                Spacing = 0;

                HeaderPosition = Dock.Top;
                HeaderPositionChanged += (sender, e) =>
                {
                    ApplyHeadersPanelSettings();
                    foreach (MGTabItem tab in ActualTabHeaders.Keys.ToList())
                    {
                        UpdateHeaderWrapper(tab);
                    }
                };

                IsFocusable = true;

                ActualTabHeaders = new();

                _Tabs = new();
                _Tabs.CollectionChanged += (sender, e) =>
                {
                    if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace)
                    {
                        if (e.NewItems != null)
                        {
                            foreach (MGTabItem Item in e.NewItems)
                            {
                                Item.HeaderChanged += Tab_HeaderChanged;
                            }
                        }
                    }

                    if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset)
                    {
                        if (e.OldItems != null)
                        {
                            foreach (MGTabItem Item in e.OldItems)
                            {
                                Item.HeaderChanged -= Tab_HeaderChanged;
                            }
                        }
                    }
                };

                DefaultControlTemplateName = MGControlTemplateCatalog.TabControlTemplateName;
                SelectedTabHeaderControlTemplateName = MGControlTemplateCatalog.SelectedTabHeaderTemplateName;
                UnselectedTabHeaderControlTemplateName = MGControlTemplateCatalog.UnselectedTabHeaderTemplateName;
            }
        }

        protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
        {
            base.OnThemeChanged(PreviousTheme, CurrentTheme);

            if (CurrentTheme == null)
            {
                return;
            }

            BackgroundBrush = CurrentTheme.GetBackgroundBrush(MGElementType.TabControl);
        }

        private void Tab_HeaderChanged(object sender, EventArgs<MGElement> e)
        {
            MGTabItem TabItem = sender as MGTabItem;
            ActualTabHeaders[TabItem].SetContent(e.NewValue);
        }

        public override void DrawBackground(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            //  The background only spans the content region of this TabControl,
            //  Does not fill the region with the tab headers
            Rectangle TabHeadersBounds = HeadersPanelElement.LayoutBounds;
            Rectangle TabContentBounds = TabHeaderPosition switch
            {
                Dock.Left => new(TabHeadersBounds.Right, LayoutBounds.Top, LayoutBounds.Width - TabHeadersBounds.Width, LayoutBounds.Height),
                Dock.Top => new(LayoutBounds.Left, TabHeadersBounds.Bottom, LayoutBounds.Width, LayoutBounds.Height - TabHeadersBounds.Height),
                Dock.Right => new(LayoutBounds.Left, LayoutBounds.Top, LayoutBounds.Width - TabHeadersBounds.Width, LayoutBounds.Height),
                Dock.Bottom => new(LayoutBounds.Left, LayoutBounds.Top, LayoutBounds.Width, LayoutBounds.Height - TabHeadersBounds.Height),
                _ => throw new NotImplementedException($"Unrecognized {nameof(Dock)}: {TabHeaderPosition}")
            };
            base.DrawBackground(DA, TabContentBounds);
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            if (_Tabs.Count == 0)
            {
                return false;
            }

            bool usesHorizontalHeaderNavigation = TabHeaderPosition is Dock.Top or Dock.Bottom;
            bool usesVerticalHeaderNavigation = TabHeaderPosition is Dock.Left or Dock.Right;

            bool isTabNavigationAction = action is UINavigationAction.Home or UINavigationAction.End or UINavigationAction.ShoulderPrevious or UINavigationAction.ShoulderNext
                || (usesHorizontalHeaderNavigation && action is UINavigationAction.MoveLeft or UINavigationAction.MoveRight)
                || (usesVerticalHeaderNavigation && action is UINavigationAction.MoveUp or UINavigationAction.MoveDown);
            if (!isTabNavigationAction)
            {
                return action == UINavigationAction.Submit && SelectedTab != null;
            }

            int nextIndex = GetAdjacentTabIndex(SelectedTabIndex, _Tabs.Count, action);
            return nextIndex >= 0 && TrySelectTabAtIndex(nextIndex);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds) { }
    }
}
