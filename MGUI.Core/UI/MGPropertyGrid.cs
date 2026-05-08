using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Keyboard;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MGUI.Core.UI
{
    public class MGPropertyGrid : MGSingleContentHost
    {
        public const string OuterBorderPartName = "PART_OuterBorder";
        public const string ScrollViewerPartName = "PART_ScrollViewer";
        public const string CategoriesPanelPartName = "PART_CategoriesPanel";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(OuterBorderPartName, typeof(MGBorder));
            yield return new(ScrollViewerPartName, typeof(MGScrollViewer));
            yield return new(CategoriesPanelPartName, typeof(MGStackPanel));
        }

        private object _SelectedObject;
        private Type _SelectedObjectType;
        private readonly List<PropertyGridCategoryView> _CategoryViews;
        private IReadOnlyList<MGPropertyGridDescriptor> _Descriptors;
        private readonly Dictionary<string, bool> _CategoryCollapsedStates;
        private int _LabelColumnWidth = 180;

        public MGBorder OuterBorder { get; private set; }
        public MGScrollViewer ScrollViewer { get; private set; }
        public MGStackPanel CategoriesPanel { get; private set; }

        public object SelectedObject
        {
            get => _SelectedObject;
            set
            {
                if (ReferenceEquals(_SelectedObject, value))
                {
                    return;
                }

                _SelectedObject = value;
                Type newType = value?.GetType();
                bool typeChanged = _SelectedObjectType != newType;
                _SelectedObjectType = newType;

                NPC(nameof(SelectedObject));
                NPC(nameof(SelectedObjectType));

                if (CategoriesPanel == null)
                {
                    return;
                }

                if (typeChanged)
                {
                    RebuildView();
                }
                else
                {
                    RefreshAllValues();
                }
            }
        }

        public Type SelectedObjectType => _SelectedObjectType;

        public int LabelColumnWidth
        {
            get => _LabelColumnWidth;
            set
            {
                int clamped = Math.Max(0, value);
                if (_LabelColumnWidth != clamped)
                {
                    _LabelColumnWidth = clamped;
                    for (int categoryIndex = 0; categoryIndex < _CategoryViews.Count; categoryIndex++)
                    {
                        PropertyGridCategoryView category = _CategoryViews[categoryIndex];
                        for (int rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
                        {
                            category.Rows[rowIndex].SetLabelColumnWidth(_LabelColumnWidth);
                        }
                    }

                    NPC(nameof(LabelColumnWidth));
                }
            }
        }

        public MGPropertyGrid(MGWindow window)
            : base(window, MGElementType.PropertyGrid)
        {
            using (BeginInitializing())
            {
                _CategoryViews = new();
                _CategoryCollapsedStates = new(StringComparer.Ordinal);
                _Descriptors = Array.Empty<MGPropertyGridDescriptor>();
                DefaultControlTemplateName = MGControlTemplateCatalog.PropertyGridTemplateName;
            }
        }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
        {
            OuterBorder = structure.Parts[OuterBorderPartName] as MGBorder;
            ScrollViewer = structure.Parts[ScrollViewerPartName] as MGScrollViewer;
            CategoriesPanel = structure.Parts[CategoriesPanelPartName] as MGStackPanel;

            CategoriesPanel.CanChangeContent = false;

            using (ScrollViewer.AllowChangingContentTemporarily())
            {
                ScrollViewer.SetContent(CategoriesPanel);
            }

            using (OuterBorder.AllowChangingContentTemporarily())
            {
                OuterBorder.SetContent(ScrollViewer);
            }

            using (AllowChangingContentTemporarily())
            {
                SetContent(OuterBorder);
            }

            RebuildView();
            ApplyGeneratedViewTheme();
        }

        protected internal override void OnThemeChanged(MGTheme previousTheme, MGTheme currentTheme)
        {
            base.OnThemeChanged(previousTheme, currentTheme);
            ApplyGeneratedViewTheme();
        }

        public void RefreshVisibleValues()
            => RefreshValues(visibleOnly: true, forceAll: false);

        private void RefreshAllValues()
            => RefreshValues(visibleOnly: false, forceAll: true);

        private void RefreshValues(bool visibleOnly, bool forceAll)
        {
            if (SelectedObject == null)
            {
                return;
            }

            Rectangle viewport = ScrollViewer?.ContentViewport ?? Rectangle.Empty;
            for (int categoryIndex = 0; categoryIndex < _CategoryViews.Count; categoryIndex++)
            {
                PropertyGridCategoryView category = _CategoryViews[categoryIndex];
                if (visibleOnly && category.IsCollapsed)
                {
                    continue;
                }

                for (int rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
                {
                    PropertyGridRowView row = category.Rows[rowIndex];

                    if (!forceAll)
                    {
                        if (row.Editor.IsEditing)
                        {
                            continue;
                        }

                        if (visibleOnly && !IsRowVisible(row, viewport))
                        {
                            continue;
                        }
                    }

                    object currentValue = row.Descriptor.Getter(SelectedObject);
                    if (!forceAll && Equals(row.LastKnownValue, currentValue))
                    {
                        continue;
                    }

                    row.ApplyValue(currentValue, forceAll);
                }
            }
        }

        private bool IsRowVisible(PropertyGridRowView row, Rectangle viewport)
        {
            if (row == null || row.Category?.IsCollapsed == true || row.Root.Visibility != Visibility.Visible)
            {
                return false;
            }

            Rectangle bounds = row.Root.ActualLayoutBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                return true;
            }

            return bounds.Intersects(viewport);
        }

        private void RebuildView()
        {
            CaptureCollapsedStates();
            ClearView();

            if (SelectedObjectType == null || CategoriesPanel == null)
            {
                _Descriptors = Array.Empty<MGPropertyGridDescriptor>();
                return;
            }

            _Descriptors = MGPropertyGridDescriptorCache.GetDescriptors(SelectedObjectType);
            List<MGPropertyGridCategoryModel> categories = BuildCategories(_Descriptors);

            using (CategoriesPanel.AllowChangingContentTemporarily())
            {
                for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
                {
                    PropertyGridCategoryView categoryView = new(this, categories[categoryIndex]);
                    _CategoryViews.Add(categoryView);
                    CategoriesPanel.TryAddChild(categoryView.Root);
                }
            }

            ApplyGeneratedViewTheme();
            RefreshAllValues();
        }

        private void CaptureCollapsedStates()
        {
            for (int index = 0; index < _CategoryViews.Count; index++)
            {
                PropertyGridCategoryView category = _CategoryViews[index];
                _CategoryCollapsedStates[category.Name] = category.IsCollapsed;
            }
        }

        private void ClearView()
        {
            for (int index = 0; index < _CategoryViews.Count; index++)
            {
                _CategoryViews[index].Dispose();
            }
            _CategoryViews.Clear();

            if (CategoriesPanel != null)
            {
                using (CategoriesPanel.AllowChangingContentTemporarily())
                {
                    CategoriesPanel.TryRemoveAll();
                }
            }
        }

        private List<MGPropertyGridCategoryModel> BuildCategories(IReadOnlyList<MGPropertyGridDescriptor> descriptors)
        {
            List<MGPropertyGridCategoryModel> result = new();
            Dictionary<string, MGPropertyGridCategoryModel> lookup = new(StringComparer.Ordinal);

            for (int index = 0; index < descriptors.Count; index++)
            {
                MGPropertyGridDescriptor descriptor = descriptors[index];
                string categoryName = string.IsNullOrWhiteSpace(descriptor.Category) ? "Misc" : descriptor.Category;
                if (!lookup.TryGetValue(categoryName, out MGPropertyGridCategoryModel category))
                {
                    category = new MGPropertyGridCategoryModel
                    {
                        Name = categoryName,
                        Descriptors = new List<MGPropertyGridDescriptor>(),
                        IsCollapsed = _CategoryCollapsedStates.TryGetValue(categoryName, out bool isCollapsed) && isCollapsed,
                    };

                    lookup.Add(categoryName, category);
                    result.Add(category);
                }

                ((List<MGPropertyGridDescriptor>)category.Descriptors).Add(descriptor);
            }

            return result;
        }

        private void CommitRowValue(PropertyGridRowView row, object candidateValue)
        {
            if (row == null || SelectedObject == null || row.Descriptor.Setter == null || row.Descriptor.IsReadOnly)
            {
                return;
            }

            object currentValue = row.Descriptor.Getter(SelectedObject);
            if (Equals(currentValue, candidateValue))
            {
                row.ApplyValue(currentValue, true);
                return;
            }

            row.Descriptor.Setter(SelectedObject, candidateValue);
            object actualValue = row.Descriptor.Getter(SelectedObject);
            row.ApplyValue(actualValue, true);
        }

        internal void SetCategoryCollapsedState(string categoryName, bool isCollapsed)
        {
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                _CategoryCollapsedStates[categoryName] = isCollapsed;
            }
        }

        private void ApplyGeneratedViewTheme()
        {
            if (_CategoryViews.Count == 0)
            {
                return;
            }

            MGThemePropertyGridSettings settings = GetTheme().PropertyGrid;
            for (int categoryIndex = 0; categoryIndex < _CategoryViews.Count; categoryIndex++)
            {
                PropertyGridCategoryView category = _CategoryViews[categoryIndex];
                category.ApplyTheme(settings);

                for (int rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
                {
                    category.Rows[rowIndex].ApplyTheme(settings);
                }
            }
        }

        private static string FormatValue(MGPropertyGridEditorKind editorKind, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return editorKind switch
            {
                MGPropertyGridEditorKind.Int => ((int)value).ToString(CultureInfo.InvariantCulture),
                MGPropertyGridEditorKind.Float => ((float)value).ToString("R", CultureInfo.InvariantCulture),
                MGPropertyGridEditorKind.Double => ((double)value).ToString("R", CultureInfo.InvariantCulture),
                MGPropertyGridEditorKind.String => value as string ?? string.Empty,
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }

        private static bool TryParseValue(MGPropertyGridEditorKind editorKind, object previousValue, string text, out object value)
        {
            switch (editorKind)
            {
                case MGPropertyGridEditorKind.Int:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                    {
                        value = intValue;
                        return true;
                    }
                    break;
                case MGPropertyGridEditorKind.Float:
                    if (float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float floatValue))
                    {
                        value = floatValue;
                        return true;
                    }
                    break;
                case MGPropertyGridEditorKind.Double:
                    if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
                    {
                        value = doubleValue;
                        return true;
                    }
                    break;
                case MGPropertyGridEditorKind.String:
                    value = previousValue == null && string.IsNullOrEmpty(text) ? null : text ?? string.Empty;
                    return true;
            }

            value = null;
            return false;
        }

        private static VisualStateSetting<Color?> ToTextColorSetting(VisualStateColorBrush brush)
            => brush == null ? new VisualStateSetting<Color?>((Color?)null, null, null, null) : new VisualStateSetting<Color?>(brush.NormalValue, brush.SelectedValue, brush.FocusedValue, brush.DisabledValue);

        private sealed class PropertyGridCategoryView : IDisposable
        {
            public string Name { get; }
            public MGStackPanel Root { get; }
            public MGButton HeaderButton { get; }
            public MGTriangleArrowIcon ArrowIcon { get; }
            public MGTextBlock HeaderText { get; }
            public MGStackPanel RowsPanel { get; }
            public List<PropertyGridRowView> Rows { get; }

            private readonly MGPropertyGrid Owner;
            private bool _IsCollapsed;
            public bool IsCollapsed
            {
                get => _IsCollapsed;
                set
                {
                    if (_IsCollapsed != value)
                    {
                        _IsCollapsed = value;
                        RowsPanel.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                        ArrowIcon.Direction = value ? UITriangleArrowDirection.Right : UITriangleArrowDirection.Down;
                        Owner.SetCategoryCollapsedState(Name, value);
                    }
                }
            }

            public PropertyGridCategoryView(MGPropertyGrid owner, MGPropertyGridCategoryModel model)
            {
                Owner = owner;
                Name = model?.Name ?? "Misc";
                Rows = new();

                MGWindow window = owner.SelfOrParentWindow;
                HeaderButton = new(window, _ => IsCollapsed = !IsCollapsed)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                };

                MGGrid headerGrid = new(window)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    ColumnSpacing = 8,
                    CanChangeContent = false,
                };
                headerGrid.AddRow(GridLength.Auto);
                headerGrid.AddColumn(GridLength.CreatePixelLength(12));
                headerGrid.AddColumn(GridLength.CreateWeightedLength(1.0));

                ArrowIcon = new(window)
                {
                    PreferredWidth = 10,
                    PreferredHeight = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                HeaderText = new(window, Name)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0),
                };

                headerGrid.TryAddChild(0, 0, ArrowIcon);
                headerGrid.TryAddChild(0, 1, HeaderText);

                using (HeaderButton.AllowChangingContentTemporarily())
                {
                    HeaderButton.SetContent(headerGrid);
                }

                RowsPanel = new(window, Orientation.Vertical)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    CanChangeContent = false,
                };

                IReadOnlyList<MGPropertyGridDescriptor> descriptors = model?.Descriptors ?? Array.Empty<MGPropertyGridDescriptor>();
                using (RowsPanel.AllowChangingContentTemporarily())
                {
                    for (int index = 0; index < descriptors.Count; index++)
                    {
                        PropertyGridRowView row = new(owner, this, descriptors[index]);
                        Rows.Add(row);
                        RowsPanel.TryAddChild(row.Root);
                    }
                }

                Root = new(window, Orientation.Vertical)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    CanChangeContent = false,
                };

                using (Root.AllowChangingContentTemporarily())
                {
                    Root.TryAddChild(HeaderButton);
                    Root.TryAddChild(RowsPanel);
                }

                IsCollapsed = model?.IsCollapsed == true;
            }

            public void ApplyTheme(MGThemePropertyGridSettings settings)
            {
                HeaderButton.BackgroundBrush = settings.CategoryHeaderBackground?.Copy();
                HeaderButton.DefaultTextForeground = ToTextColorSetting(settings.CategoryHeaderForeground?.Copy());
                HeaderButton.Padding = settings.CategoryHeaderPadding;
                HeaderButton.MinHeight = settings.CategoryHeaderMinHeight;
                ArrowIcon.Color = settings.CategoryArrowColor;
                HeaderText.Foreground = new VisualStateSetting<Color?>((Color?)null, null, null, null);
                RowsPanel.Spacing = settings.RowsSpacing;
            }

            public void Dispose()
            {
                for (int index = 0; index < Rows.Count; index++)
                {
                    Rows[index].Dispose();
                }

                Rows.Clear();
            }
        }

        private sealed class PropertyGridRowView : IDisposable
        {
            public PropertyGridCategoryView Category { get; }
            public MGPropertyGridDescriptor Descriptor { get; }
            public MGBorder Root { get; }
            public object LastKnownValue { get; private set; }
            public IPropertyGridEditor Editor { get; }

            private readonly MGGrid LayoutGrid;
            private readonly ColumnDefinition LabelColumn;
            private readonly MGPropertyGrid Owner;

            public PropertyGridRowView(MGPropertyGrid owner, PropertyGridCategoryView category, MGPropertyGridDescriptor descriptor)
            {
                Owner = owner;
                Category = category;
                Descriptor = descriptor;

                MGWindow window = owner.SelfOrParentWindow;
                Root = new(window)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                };

                LayoutGrid = new(window)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    ColumnSpacing = 8,
                    CanChangeContent = false,
                };
                LayoutGrid.AddRow(GridLength.Auto);
                LabelColumn = LayoutGrid.AddColumn(GridLength.CreatePixelLength(owner.LabelColumnWidth));
                LayoutGrid.AddColumn(GridLength.CreateWeightedLength(1.0));

                MGTextBlock label = new(window, descriptor.DisplayName ?? descriptor.Name)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0),
                };
                LayoutGrid.TryAddChild(0, 0, label);

                Editor = CreateEditor(owner, descriptor);
                Editor.SetReadOnly(descriptor.IsReadOnly);
                Editor.ValueCommitted += Editor_ValueCommitted;
                LayoutGrid.TryAddChild(0, 1, Editor.Element);

                using (Root.AllowChangingContentTemporarily())
                {
                    Root.SetContent(LayoutGrid);
                }
            }

            private void Editor_ValueCommitted(object sender, object value)
                => Owner.CommitRowValue(this, value);

            public void SetLabelColumnWidth(int value)
                => LabelColumn.Length = GridLength.CreatePixelLength(Math.Max(0, value));

            public void ApplyTheme(MGThemePropertyGridSettings settings)
            {
                Root.Padding = settings.RowPadding;
                Root.BorderThickness = settings.RowSeparatorBrush != null ? new Thickness(0, 0, 0, 1) : new Thickness(0);
                Root.BorderBrush = settings.RowSeparatorBrush?.AsUniformBorderBrush();
                Editor.ApplyTheme(settings);
            }

            public void ApplyValue(object value, bool force)
            {
                LastKnownValue = value;
                Editor.ApplyValue(value, force);
            }

            public void Dispose()
            {
                Editor.ValueCommitted -= Editor_ValueCommitted;
                Editor.Dispose();
            }

            private static IPropertyGridEditor CreateEditor(MGPropertyGrid owner, MGPropertyGridDescriptor descriptor)
                => descriptor.EditorKind switch
                {
                    MGPropertyGridEditorKind.Bool => new BoolPropertyGridEditor(owner),
                    MGPropertyGridEditorKind.Int => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.Int),
                    MGPropertyGridEditorKind.Float => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.Float),
                    MGPropertyGridEditorKind.Double => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.Double),
                    MGPropertyGridEditorKind.String => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.String),
                    _ => throw new NotSupportedException($"Unsupported {nameof(MGPropertyGridEditorKind)} '{descriptor.EditorKind}'."),
                };
        }

        internal interface IPropertyGridEditor : IDisposable
        {
            MGElement Element { get; }
            bool IsEditing { get; }
            event EventHandler<object> ValueCommitted;
            void ApplyValue(object value, bool force);
            void SetReadOnly(bool isReadOnly);
            void ApplyTheme(MGThemePropertyGridSettings settings);
        }

        private abstract class PropertyGridEditorBase : IPropertyGridEditor
        {
            protected MGPropertyGrid Owner { get; }
            protected object LastPresentedValue { get; private set; }

            public abstract MGElement Element { get; }
            public abstract bool IsEditing { get; }
            public event EventHandler<object> ValueCommitted;

            protected PropertyGridEditorBase(MGPropertyGrid owner)
            {
                Owner = owner;
            }

            public void ApplyValue(object value, bool force)
            {
                if (!force && Equals(LastPresentedValue, value))
                {
                    return;
                }

                LastPresentedValue = value;
                ApplyValueCore(value, force);
            }

            protected void RaiseValueCommitted(object value)
                => ValueCommitted?.Invoke(this, value);

            protected abstract void ApplyValueCore(object value, bool force);
            public abstract void SetReadOnly(bool isReadOnly);
            public abstract void ApplyTheme(MGThemePropertyGridSettings settings);
            public abstract void Dispose();
        }

        private sealed class BoolPropertyGridEditor : PropertyGridEditorBase
        {
            private readonly MGCheckBox CheckBox;
            private bool IsSynchronizing;

            public override MGElement Element => CheckBox;
            public override bool IsEditing => false;

            public BoolPropertyGridEditor(MGPropertyGrid owner)
                : base(owner)
            {
                CheckBox = new(owner.SelfOrParentWindow)
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    SpacingWidth = 0,
                    IsThreeState = false,
                };
                CheckBox.OnCheckStateChanged += CheckBox_OnCheckStateChanged;
            }

            private void CheckBox_OnCheckStateChanged(object sender, EventArgs<bool?> e)
            {
                if (!IsSynchronizing && CheckBox.IsChecked.HasValue)
                {
                    RaiseValueCommitted(CheckBox.IsChecked.Value);
                }
            }

            protected override void ApplyValueCore(object value, bool force)
            {
                IsSynchronizing = true;
                try
                {
                    CheckBox.IsChecked = value is bool booleanValue && booleanValue;
                }
                finally
                {
                    IsSynchronizing = false;
                }
            }

            public override void SetReadOnly(bool isReadOnly)
                => CheckBox.IsReadonly = isReadOnly;

            public override void ApplyTheme(MGThemePropertyGridSettings settings)
            {
            }

            public override void Dispose()
                => CheckBox.OnCheckStateChanged -= CheckBox_OnCheckStateChanged;
        }

        private sealed class TextPropertyGridEditor : PropertyGridEditorBase
        {
            private readonly MGPropertyGridEditorKind EditorKind;
            private readonly MGBorder HostBorder;
            private readonly MGTextBox TextBox;
            private IBorderBrush InvalidBorderBrush;
            private bool HasValidationError;
            private readonly EventHandler<EventArgs<string>> TextChangedHandler;
            private readonly EventHandler<BaseKeyPressedEventArgs> KeyPressedHandler;
            private readonly EventHandler<EventArgs<MGElement>> FocusChangedHandler;

            public override MGElement Element => HostBorder;
            public override bool IsEditing => Owner.GetDesktop().FocusedKeyboardHandler == TextBox;

            public TextPropertyGridEditor(MGPropertyGrid owner, MGPropertyGridEditorKind editorKind)
                : base(owner)
            {
                EditorKind = editorKind;

                TextBox = new(owner.SelfOrParentWindow, editorKind == MGPropertyGridEditorKind.String ? 1024 : 64)
                {
                    AcceptsReturn = false,
                    AcceptsTab = false,
                    MinLines = 1,
                    MaxLines = 1,
                    WrapText = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                HostBorder = new(owner.SelfOrParentWindow)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                };
                using (HostBorder.AllowChangingContentTemporarily())
                {
                    HostBorder.SetContent(TextBox);
                }

                TextChangedHandler = (sender, e) => UpdateValidationVisual();
                KeyPressedHandler = (sender, e) =>
                {
                    if (!e.IsHandled && e.Key == Keys.Enter && IsEditing && TryCommit(revertIfInvalid: false))
                    {
                        e.SetHandledBy(TextBox, false);
                    }
                };
                FocusChangedHandler = (sender, e) =>
                {
                    if (ReferenceEquals(e.PreviousValue, TextBox) && !ReferenceEquals(e.NewValue, TextBox))
                    {
                        _ = TryCommit(revertIfInvalid: true);
                    }
                };

                TextBox.TextChanged += TextChangedHandler;
                TextBox.KeyboardHandler.Pressed += KeyPressedHandler;
                owner.GetDesktop().FocusedKeyboardHandlerChanged += FocusChangedHandler;
            }

            protected override void ApplyValueCore(object value, bool force)
            {
                string formattedValue = FormatValue(EditorKind, value);
                if (force || TextBox.Text != formattedValue)
                {
                    TextBox.SetText(formattedValue);
                }

                HasValidationError = false;
                UpdateValidationVisual();
            }

            public override void SetReadOnly(bool isReadOnly)
                => TextBox.IsReadonly = isReadOnly;

            public override void ApplyTheme(MGThemePropertyGridSettings settings)
            {
                InvalidBorderBrush = settings.InvalidEditorBorderBrush?.Copy();
                UpdateValidationVisual();
            }

            private bool TryCommit(bool revertIfInvalid)
            {
                if (TryParseValue(EditorKind, LastPresentedValue, TextBox.Text, out object parsedValue))
                {
                    HasValidationError = false;
                    UpdateValidationVisual();
                    RaiseValueCommitted(parsedValue);
                    return true;
                }

                HasValidationError = !string.IsNullOrWhiteSpace(TextBox.Text);
                UpdateValidationVisual();

                if (revertIfInvalid)
                {
                    ApplyValue(LastPresentedValue, true);
                }

                return false;
            }

            private void UpdateValidationVisual()
            {
                if (EditorKind == MGPropertyGridEditorKind.String)
                {
                    HasValidationError = false;
                }
                else if (IsEditing)
                {
                    HasValidationError = !string.IsNullOrWhiteSpace(TextBox.Text)
                        && !TryParseValue(EditorKind, LastPresentedValue, TextBox.Text, out _);
                }

                HostBorder.BorderThickness = HasValidationError ? new Thickness(1) : new Thickness(0);
                HostBorder.BorderBrush = HasValidationError ? InvalidBorderBrush : null;
            }

            public override void Dispose()
            {
                TextBox.TextChanged -= TextChangedHandler;
                TextBox.KeyboardHandler.Pressed -= KeyPressedHandler;
                Owner.GetDesktop().FocusedKeyboardHandlerChanged -= FocusChangedHandler;
            }
        }
    }
}