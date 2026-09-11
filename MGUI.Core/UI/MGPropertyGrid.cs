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
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;

namespace MGUI.Core.UI
{
    public class MGPropertyGrid : MGSingleContentHost
    {
        private const int DefaultRowControlMinHeight = 24;

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
                bool descriptorsCanVaryPerInstance = value is ICustomTypeDescriptor || _SelectedObject is ICustomTypeDescriptor;
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

                if (typeChanged || descriptorsCanVaryPerInstance)
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

            Rectangle bounds = GetElementViewportBounds(row.Root);
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

        private Rectangle GetElementViewportBounds(MGElement element)
        {
            if (element == null)
            {
                return Rectangle.Empty;
            }

            MGElement parent = element.Parent;
            if (parent == null)
            {
                return element.ActualLayoutBounds;
            }

            return parent.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.UnscaledScreen, element.LayoutBounds);
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

            _Descriptors = MGPropertyGridDescriptorCache.GetDescriptors(SelectedObject);
            List<MGPropertyGridCategoryModel> categories = BuildCategories(_Descriptors);
            MGThemePropertyGridSettings settings = GetTheme().PropertyGrid;

            using (CategoriesPanel.AllowChangingContentTemporarily())
            {
                for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
                {
                    PropertyGridCategoryView categoryView = new(this, categories[categoryIndex]);
                    ApplyTheme(categoryView, settings);
                    _CategoryViews.Add(categoryView);
                    CategoriesPanel.TryAddChild(categoryView.Root);
                }
            }

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
                ApplyTheme(_CategoryViews[categoryIndex], settings);
            }
        }

        private static void ApplyTheme(PropertyGridCategoryView category, MGThemePropertyGridSettings settings)
        {
            category.ApplyTheme(settings);

            for (int rowIndex = 0; rowIndex < category.Rows.Count; rowIndex++)
            {
                category.Rows[rowIndex].ApplyTheme(settings);
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
                MGPropertyGridEditorKind.Int => FormatIntegerValue(value),
                MGPropertyGridEditorKind.Float => ((float)value).ToString("R", CultureInfo.InvariantCulture),
                MGPropertyGridEditorKind.Double => ((double)value).ToString("R", CultureInfo.InvariantCulture),
                MGPropertyGridEditorKind.String => value as string ?? string.Empty,
                MGPropertyGridEditorKind.Color when PropertyGridColorAdapter.TryToColorValue(value, out ColorValue colorValue) => ColorFormatter.Format(colorValue, ColorValueFormat.HexRgba),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }

        private static bool TryParseValue(MGPropertyGridEditorKind editorKind, object previousValue, string text, out object value)
        {
            switch (editorKind)
            {
                case MGPropertyGridEditorKind.Int:
                    if (TryParseIntegerValue(previousValue?.GetType() ?? typeof(int), text, out object integerValue))
                    {
                        value = integerValue;
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

        private static string FormatIntegerValue(object value)
        {
            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool TryParseIntegerValue(Type targetType, string text, out object value)
        {
            Type actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            switch (Type.GetTypeCode(actualType))
            {
                case TypeCode.Byte:
                    if (byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte byteValue))
                    {
                        value = byteValue;
                        return true;
                    }
                    break;
                case TypeCode.SByte:
                    if (sbyte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte sbyteValue))
                    {
                        value = sbyteValue;
                        return true;
                    }
                    break;
                case TypeCode.Int16:
                    if (short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out short shortValue))
                    {
                        value = shortValue;
                        return true;
                    }
                    break;
                case TypeCode.UInt16:
                    if (ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort ushortValue))
                    {
                        value = ushortValue;
                        return true;
                    }
                    break;
                case TypeCode.Int32:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                    {
                        value = intValue;
                        return true;
                    }
                    break;
                case TypeCode.UInt32:
                    if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint uintValue))
                    {
                        value = uintValue;
                        return true;
                    }
                    break;
                case TypeCode.Int64:
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
                    {
                        value = longValue;
                        return true;
                    }
                    break;
                case TypeCode.UInt64:
                    if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong ulongValue))
                    {
                        value = ulongValue;
                        return true;
                    }
                    break;
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
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                HeaderButton.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                // ADR-0005: DefaultValue (not LocalValue) so that ApplyTheme's Theme write of the same Padding always wins;
                // HeaderButton is not a template part (the PropertyGrid template only covers the outer border, scroll
                // viewer and categories panel), so this construction-time value is a placeholder, never a competing source.
                HeaderButton.SetPadding(new Thickness(0), UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                HeaderButton.SetBorderThicknessTagged(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

                MGStackPanel headerPanel = new(window, Orientation.Horizontal)
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 8,
                    CanChangeContent = false,
                };
                headerPanel.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                headerPanel.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

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
                };
                HeaderText.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

                using (headerPanel.AllowChangingContentTemporarily())
                {
                    headerPanel.TryAddChild(ArrowIcon);
                    headerPanel.TryAddChild(HeaderText);
                }

                using (HeaderButton.AllowChangingContentTemporarily())
                {
                    HeaderButton.SetContent(headerPanel);
                }

                RowsPanel = new PropertyGridRowsPanel(owner, window)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    CanChangeContent = false,
                };
                RowsPanel.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                RowsPanel.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

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
                    CanChangeContent = false,
                };
                Root.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                Root.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

                using (Root.AllowChangingContentTemporarily())
                {
                    Root.TryAddChild(HeaderButton);
                    Root.TryAddChild(RowsPanel);
                }

                IsCollapsed = model?.IsCollapsed == true;
            }

            public void ApplyTheme(MGThemePropertyGridSettings settings)
            {
                VisualStateSetting<Color?> headerForeground = ToTextColorSetting(settings.CategoryHeaderForeground?.Copy());
                HeaderButton.BackgroundBrush = settings.CategoryHeaderBackground?.Copy();
                HeaderButton.DefaultTextForeground = headerForeground;
                HeaderButton.SetPadding(settings.CategoryHeaderPadding, UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                HeaderButton.SetMinHeight(settings.CategoryHeaderMinHeight, UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                ArrowIcon.Color = settings.CategoryArrowColor;
                HeaderText.Foreground = new VisualStateSetting<Color?>((Color?)null, null, null, null);
                HeaderText.DefaultTextForeground = headerForeground.GetCopy();
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

        private sealed class PropertyGridRowsPanel : MGStackPanel
        {
            private readonly MGPropertyGrid Owner;
            private readonly List<MGElement> VisibleChildrenBuffer = new();

            public PropertyGridRowsPanel(MGPropertyGrid owner, MGWindow window)
                : base(window, Orientation.Vertical)
            {
                Owner = owner;
            }

            public override IReadOnlyList<MGElement> GetVisualTreeChildren(bool IncludeInactive, bool IncludeActive)
            {
                if (!IncludeActive)
                {
                    return base.GetVisualTreeChildren(IncludeInactive, IncludeActive);
                }

                Rectangle viewport = Owner.ScrollViewer?.ContentViewport ?? Rectangle.Empty;
                if (viewport.Width <= 0 || viewport.Height <= 0)
                {
                    return base.GetVisualTreeChildren(IncludeInactive, IncludeActive);
                }

                VisibleChildrenBuffer.Clear();
                for (int i = 0; i < Children.Count; i++)
                {
                    MGElement child = Children[i];
                    Rectangle bounds = Owner.GetElementViewportBounds(child);
                    if (bounds.Width > 0 && bounds.Height > 0 && bounds.Intersects(viewport))
                    {
                        VisibleChildrenBuffer.Add(child);
                    }
                }

                return VisibleChildrenBuffer;
            }

            protected override void UpdateContents(ElementUpdateArgs UA)
            {
                Rectangle viewport = Owner.ScrollViewer?.ContentViewport ?? Rectangle.Empty;
                if (viewport.Width <= 0 || viewport.Height <= 0)
                {
                    base.UpdateContents(UA);
                    return;
                }

                IReadOnlyList<MGElement> activeChildren = GetVisualTreeChildren(false, true);
                for (int i = activeChildren.Count - 1; i >= 0; i--)
                {
                    MGElement child = activeChildren[i];
                    Rectangle bounds = Owner.GetElementViewportBounds(child);
                    if (bounds.Width > 0 && bounds.Height > 0 && bounds.Intersects(viewport))
                    {
                        child.Update(UA);
                    }
                }
            }
        }

        private sealed class PropertyGridRowView : IDisposable
        {
            public PropertyGridCategoryView Category { get; }
            public MGPropertyGridDescriptor Descriptor { get; }
            public MGBorder Root { get; }
            public object LastKnownValue { get; private set; }
            public IPropertyGridEditor Editor { get; }

            private readonly MGDockPanel LayoutPanel;
            private readonly MGPropertyGrid Owner;
            private readonly MGTextBlock Label;

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
                };
                Root.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                // ADR-0005: DefaultValue (not LocalValue) so that ApplyTheme's Theme write of the row Padding always wins;
                // Root is not a template part, so this construction-time value is a placeholder, never a competing source.
                Root.SetPadding(new Thickness(0), UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

                LayoutPanel = new(window)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    CanChangeContent = false,
                };
                LayoutPanel.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                LayoutPanel.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

                Label = new(window, descriptor.DisplayName ?? descriptor.Name)
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    PreferredWidth = owner.LabelColumnWidth,
                };
                Label.SetMargin(new Thickness(0, 0, 8, 0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                Label.SetMinHeight(DefaultRowControlMinHeight, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

                Editor = CreateEditor(owner, descriptor);
                Editor.SetReadOnly(descriptor.IsReadOnly);
                Editor.ValueCommitted += Editor_ValueCommitted;

                using (LayoutPanel.AllowChangingContentTemporarily())
                {
                    LayoutPanel.TryAddChild(Label, Dock.Left);
                    LayoutPanel.TryAddChild(Editor.Element, Dock.Left);
                }

                using (Root.AllowChangingContentTemporarily())
                {
                    Root.SetContent(LayoutPanel);
                }
            }

            private void Editor_ValueCommitted(object sender, object value)
                => Owner.CommitRowValue(this, value);

            public void SetLabelColumnWidth(int value)
                => Label.PreferredWidth = Math.Max(0, value);

            public void ApplyTheme(MGThemePropertyGridSettings settings)
            {
                VisualStateColorBrush themeText = Owner.GetTheme().TextBlockFallbackForeground.GetValue(true);
                VisualStateSetting<Color?> textForeground = ToTextColorSetting(themeText?.Copy());

                Root.SetPadding(settings.RowPadding, UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                Root.SetBorderThickness(settings.RowSeparatorBrush != null ? new Thickness(0, 0, 0, 1) : new Thickness(0), UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                Root.SetBorderBrush(settings.RowSeparatorBrush?.AsUniformBorderBrush(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
                Root.DefaultTextForeground = textForeground.GetCopy();
                Label.DefaultTextForeground = textForeground.GetCopy();
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
            {
                if (descriptor.EditorKind == MGPropertyGridEditorKind.Color)
                {
                    ColorPropertyGridEditor colorEditor = new(owner, descriptor.PropertyType);
                    colorEditor.SetReadOnly(descriptor.IsReadOnly);
                    return colorEditor;
                }

                if (descriptor.IsReadOnly)
                {
                    return new ReadOnlyPropertyGridEditor(owner, descriptor.EditorKind);
                }

                return descriptor.EditorKind switch
                {
                    MGPropertyGridEditorKind.Bool => new BoolPropertyGridEditor(owner),
                    MGPropertyGridEditorKind.Int => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.Int),
                    MGPropertyGridEditorKind.Float => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.Float),
                    MGPropertyGridEditorKind.Double => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.Double),
                    MGPropertyGridEditorKind.String => new TextPropertyGridEditor(owner, MGPropertyGridEditorKind.String),
                    _ => throw new NotSupportedException($"Unsupported {nameof(MGPropertyGridEditorKind)} '{descriptor.EditorKind}'."),
                };
            }
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
                CheckBox.SetMinHeight(DefaultRowControlMinHeight, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
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
                VisualStateColorBrush themeText = Owner.GetTheme().TextBlockFallbackForeground.GetValue(true);
                CheckBox.DefaultTextForeground = ToTextColorSetting(themeText?.Copy());
            }

            public override void Dispose()
                => CheckBox.OnCheckStateChanged -= CheckBox_OnCheckStateChanged;
        }

        private sealed class ReadOnlyPropertyGridEditor : PropertyGridEditorBase
        {
            private readonly MGPropertyGridEditorKind EditorKind;
            private readonly MGBorder HostBorder;
            private readonly MGTextBlock DisplayText;

            public override MGElement Element => HostBorder;
            public override bool IsEditing => false;

            public ReadOnlyPropertyGridEditor(MGPropertyGrid owner, MGPropertyGridEditorKind editorKind)
                : base(owner)
            {
                EditorKind = editorKind;

                DisplayText = new(owner.SelfOrParentWindow, string.Empty)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                    HasStableTextFootprint = true,
                };
                DisplayText.SetMargin(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                DisplayText.SetMinHeight(DefaultRowControlMinHeight, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

                HostBorder = new(owner.SelfOrParentWindow)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                };
                HostBorder.SetMinHeight(DefaultRowControlMinHeight, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                HostBorder.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                HostBorder.SetBorderThickness(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                using (HostBorder.AllowChangingContentTemporarily())
                {
                    HostBorder.SetContent(DisplayText);
                }
            }

            protected override void ApplyValueCore(object value, bool force)
            {
                string formattedValue = FormatValue(EditorKind, value);
                if (force || DisplayText.Text != formattedValue)
                {
                    DisplayText.SetText(formattedValue, MGTextInvalidationMode.ReflowLocal);
                }
            }

            public override void SetReadOnly(bool isReadOnly)
            {
            }

            public override void ApplyTheme(MGThemePropertyGridSettings settings)
            {
                VisualStateColorBrush themeText = Owner.GetTheme().TextBlockFallbackForeground.GetValue(true);
                VisualStateSetting<Color?> textForeground = ToTextColorSetting(themeText?.Copy());
                HostBorder.DefaultTextForeground = textForeground.GetCopy();
                DisplayText.DefaultTextForeground = textForeground.GetCopy();
            }

            public override void Dispose()
            {
            }
        }

        private sealed class TextPropertyGridEditor : PropertyGridEditorBase
        {
            private readonly MGPropertyGridEditorKind EditorKind;
            private readonly MGBorder HostBorder;
            private readonly MGTextBox TextBox;
            private readonly IBorderBrush DefaultTextBoxBorderBrush;
            private IBorderBrush InvalidBorderBrush;
            private bool HasValidationError;
            private readonly EventHandler<EventArgs<string>> TextChangedHandler;
            private readonly EventHandler<BaseKeyPressedEventArgs> KeyPressedHandler;
            private readonly EventHandler<EventArgs<MGElement>> FocusChangedHandler;

            public override MGElement Element => HostBorder;
            public override bool IsEditing
            {
                get
                {
                    MGDesktop desktop = Owner.GetDesktop();
                    return ReferenceEquals(desktop.FocusedKeyboardHandler, TextBox)
                        || ReferenceEquals(desktop.QueuedFocusedKeyboardHandler, TextBox);
                }
            }

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
                DefaultTextBoxBorderBrush = TextBox.BorderBrush;

                HostBorder = new(owner.SelfOrParentWindow)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                HostBorder.SetMinHeight(DefaultRowControlMinHeight, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                HostBorder.SetPadding(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                HostBorder.SetBorderThickness(new Thickness(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                using (HostBorder.AllowChangingContentTemporarily())
                {
                    HostBorder.SetContent(TextBox);
                }

                TextChangedHandler = (sender, e) =>
                {
                    UpdateValidationVisual();
                };
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
                    TextBox.SetText(formattedValue, SuppressLayoutChanged: true);
                }

                HasValidationError = false;
                UpdateValidationVisual();
            }

            public override void SetReadOnly(bool isReadOnly)
                => TextBox.IsReadonly = isReadOnly;

            public override void ApplyTheme(MGThemePropertyGridSettings settings)
            {
                VisualStateColorBrush themeText = Owner.GetTheme().TextBlockFallbackForeground.GetValue(true);
                InvalidBorderBrush = settings.InvalidEditorBorderBrush?.Copy();
                HostBorder.DefaultTextForeground = ToTextColorSetting(themeText?.Copy());
                TextBox.DefaultTextForeground = ToTextColorSetting(themeText?.Copy());
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

                // ADR-0005: the BorderThickness write below is intentionally redundant with the construction-time
                // LocalValue(0) contribution (see HostBorder's constructor above) -- kept so the validation visual
                // is self-contained (it always (re)poses its own VisualState contribution here on every call,
                // independent of what construction happened to write), even though it never changes the winner.
                HostBorder.SetBorderThickness(new Thickness(0), UIValueResolutionSource.VisualState(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                HostBorder.SetBorderBrush(null, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
                TextBox.SetBorderBrushTagged(HasValidationError ? InvalidBorderBrush ?? MGUniformBorderBrush.Transparent : DefaultTextBoxBorderBrush, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
            }

            public override void Dispose()
            {
                TextBox.TextChanged -= TextChangedHandler;
                TextBox.KeyboardHandler.Pressed -= KeyPressedHandler;
                Owner.GetDesktop().FocusedKeyboardHandlerChanged -= FocusChangedHandler;
            }
        }

        private sealed class ColorPropertyGridEditor : PropertyGridEditorBase
        {
            private readonly Type PropertyType;
            private readonly MGColorField Field;
            private bool IsSynchronizing;

            public override MGElement Element => Field;
            public override bool IsEditing => Field.Popup.IsOpen;

            public ColorPropertyGridEditor(MGPropertyGrid owner, Type propertyType)
                : base(owner)
            {
                PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
                Field = new MGColorField(owner.SelfOrParentWindow, null, new ColorPickerOptions
                {
                    AllowNull = Nullable.GetUnderlyingType(propertyType) != null,
                    ShowTextInput = true,
                    ShowAlpha = propertyType != typeof(Vector3) && propertyType != typeof(System.Numerics.Vector3),
                    DisplayFormat = ColorValueFormat.HexRgba,
                    CommitMode = ColorEditCommitMode.ExplicitOkCancel,
                })
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    FieldHeight = DefaultRowControlMinHeight,
                };
                Field.ValueChanged += Field_ValueChanged;
            }

            private void Field_ValueChanged(object sender, ColorFieldValueChangedEventArgs e)
            {
                if (IsSynchronizing)
                {
                    return;
                }

                if (e.NewValue.HasValue)
                {
                    RaiseValueCommitted(PropertyGridColorAdapter.ToPropertyValue(e.NewValue.Value, PropertyType));
                }
                else if (Nullable.GetUnderlyingType(PropertyType) != null)
                {
                    RaiseValueCommitted(null);
                }
            }

            protected override void ApplyValueCore(object value, bool force)
            {
                IsSynchronizing = true;
                try
                {
                    Field.AllowNull = Nullable.GetUnderlyingType(PropertyType) != null;
                    Field.IsMixed = false;
                    if (value == null)
                    {
                        Field.Model.SetValueFromSource(null);
                    }
                    else if (PropertyGridColorAdapter.TryToColorValue(value, out ColorValue colorValue))
                    {
                        Field.Model.SetValueFromSource(colorValue);
                    }
                }
                finally
                {
                    IsSynchronizing = false;
                }
            }

            public override void SetReadOnly(bool isReadOnly)
                => Field.IsReadOnly = isReadOnly;

            public override void ApplyTheme(MGThemePropertyGridSettings settings)
            {
            }

            public override void Dispose()
                => Field.ValueChanged -= Field_ValueChanged;
        }
    }
}