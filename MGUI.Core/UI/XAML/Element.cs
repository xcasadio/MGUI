using MGUI.Core.UI.Data_Binding;
using MGUI.Core.UI.Data_Binding.Converters;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using XNAColor = Microsoft.Xna.Framework.Color;
using MGUI.Core.UI.Responsive;

namespace MGUI.Core.UI.XAML
{
    [TypeConverter(typeof(ElementStringConverter))]
    public abstract class Element : XAMLBindableBase
    {
        public abstract MGElementType ElementType { get; }

        /// <summary>Tracks the names of properties that were explicitly assigned in XAML by the parser (i.e., the property setter was called during <c>XamlServices.Parse</c>).<br/>
        /// Used by <see cref="ProcessStyles(MGResources)"/> to distinguish an intentional XAML assignment from a C#-level default.<para/>
        /// <b>Note:</b> Only non-nullable value-type properties (<c>bool</c>, <c>int</c>, enums, …) need to register here, because
        /// nullable and reference-type properties already use <c>null</c> as a reliable "not-set" sentinel.</summary>
        internal HashSet<string> ExplicitlySetProperties { get; } = new();

        /// <summary>If false, implicit and named styles inherited from ancestor XAML elements are not propagated to this node.
        /// Local styles declared directly on this node still apply when this node is processed.</summary>
        [Browsable(false)]
        public bool InheritsParentStyles { get; set; } = true;

        public string Name { get; set; }

        [Category("Layout")]
        public Thickness? Margin { get; set; }
        [Category("Layout")]
        public Thickness? Padding { get; set; }

        [Category("Layout")]public HorizontalAlignment? HorizontalAlignment { get; set; }
        [Category("Layout")]public VerticalAlignment? VerticalAlignment { get; set; }
        [Category("Layout")]public HorizontalAlignment? HorizontalContentAlignment { get; set; }
        [Category("Layout")]public VerticalAlignment? VerticalContentAlignment { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Browsable(false)]
        public HorizontalAlignment? HA { get => HorizontalAlignment; set => HorizontalAlignment = value; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Browsable(false)]
        public VerticalAlignment? VA { get => VerticalAlignment; set => VerticalAlignment = value; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Browsable(false)]
        public HorizontalAlignment? HCA { get => HorizontalContentAlignment; set => HorizontalContentAlignment = value; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Browsable(false)]
        public VerticalAlignment? VCA { get => VerticalContentAlignment; set => VerticalContentAlignment = value; }

        [Category("Layout")]
        public int? MinWidth { get; set; }
        [Category("Layout")]
        public int? MinHeight { get; set; }
        [Category("Layout")]
        public int? MaxWidth { get; set; }
        [Category("Layout")]
        public int? MaxHeight { get; set; }

        [Category("Layout")]
        public bool? UseResponsiveLayout { get; set; }
        [Category("Layout")]
        public bool? ScaleSpacingWithResponsive { get; set; }
        [Category("Layout")]
        public bool? ScaleDimensionsWithResponsive { get; set; }

        [Browsable(false)]
        public int? PreferredWidth { get; set; }
        [Browsable(false)]
        public int? PreferredHeight { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Category("Layout")]
        public int? Width { get => PreferredWidth; set => PreferredWidth = value; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Category("Layout")]
        public int? Height { get => PreferredHeight; set => PreferredHeight = value; }

        public ToolTip ToolTip { get; set; }
        public ContextMenu ContextMenu { get; set; }

        [Category("Behavior")]
        public bool? CanHandleInputsWhileHidden { get; set; }
        [Category("Behavior")]
        public bool? IsHitTestVisible { get; set; }

        [Category("Behavior")]
        public bool? IsSelected { get; set; }
        [Category("Behavior")]
        public bool? IsEnabled { get; set; }

        [Category("Appearance")]
        public Thickness? BackgroundRenderPadding { get; set; }
        [Category("Appearance")]
        public FillBrush Background { get; set; }
        [Category("Appearance")]
        public FillBrush Overlay { get; set; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Browsable(false)]
        public FillBrush BG { get => Background; set => Background = value; }
        [Category("Appearance")]
        public FillBrush DisabledBackground { get; set; }
        [Category("Appearance")]
        public FillBrush SelectedBackground { get; set; }
        [Category("Appearance")]
        public XAMLColor? BackgroundFocusedColor { get; set; }

        [Category("Appearance")]
        public XAMLColor? TextForeground { get; set; }
        [Category("Appearance")]
        public XAMLColor? DisabledTextForeground { get; set; }
        [Category("Appearance")]
        public XAMLColor? SelectedTextForeground { get; set; }

        [Category("Appearance")]
        public Visibility? Visibility { get; set; }

        [Category("Layout")]
        public bool? ClipToBounds { get; set; }

        [Category("Appearance")]
        public float? Opacity { get; set; }

        [Category("Appearance")]
        public float? RenderScale { get; set; }

        /// <summary>Used by <see cref="DockPanel"/>'s children</summary>
        [Category("Attached")]
        public Dock Dock
        {
            get => _dock;
            set { _dock = value; ExplicitlySetProperties.Add(nameof(Dock)); }
        }
        private Dock _dock = Dock.Top;

        /// <summary>Used by <see cref="Grid"/>'s children</summary>
        [Browsable(false)]
        public int GridRow
        {
            get => _gridRow;
            set { _gridRow = value; ExplicitlySetProperties.Add(nameof(GridRow)); }
        }
        private int _gridRow = 0;
        /// <summary>Used by <see cref="Grid"/>'s children</summary>
        [Browsable(false)]
        public int GridColumn
        {
            get => _gridColumn;
            set { _gridColumn = value; ExplicitlySetProperties.Add(nameof(GridColumn)); }
        }
        private int _gridColumn = 0;
        /// <summary>Used by <see cref="Grid"/>'s children</summary>
        [Browsable(false)]
        public int GridRowSpan
        {
            get => _gridRowSpan;
            set { _gridRowSpan = value; ExplicitlySetProperties.Add(nameof(GridRowSpan)); }
        }
        private int _gridRowSpan = 1;
        /// <summary>Used by <see cref="Grid"/>'s children</summary>
        [Browsable(false)]
        public int GridColumnSpan
        {
            get => _gridColumnSpan;
            set { _gridColumnSpan = value; ExplicitlySetProperties.Add(nameof(GridColumnSpan)); }
        }
        private int _gridColumnSpan = 1;
        /// <summary>Used by <see cref="Grid"/>'s children</summary>
        [Category("Attached")]
        public bool GridAffectsMeasure
        {
            get => _gridAffectsMeasure;
            set { _gridAffectsMeasure = value; ExplicitlySetProperties.Add(nameof(GridAffectsMeasure)); }
        }
        private bool _gridAffectsMeasure = true;

        /// <summary>Used by <see cref="OverlayPanel"/>'s children</summary>
        [Category("Attached")]
        public Thickness Offset { get; set; } = new();
        /// <summary>Used by <see cref="OverlayPanel"/> and <see cref="ResponsiveRoot"/> children.</summary>
        [Category("Attached")]
        public ResponsiveAnchor? ResponsiveAnchor { get; set; }
        /// <summary>Used by <see cref="OverlayPanel"/>'s children and by <see cref="Overlay"/>s.</summary>
        [Category("Attached")]
        public double? ZIndex { get; set; } = null;
        /// <summary>Used by <see cref="Canvas"/>'s children.</summary>
        [Category("Attached")]
        public int? CanvasLeft { get; set; }
        /// <summary>Used by <see cref="Canvas"/>'s children.</summary>
        [Category("Attached")]
        public int? CanvasTop { get; set; }
        /// <summary>Used by <see cref="Canvas"/>'s children.</summary>
        [Category("Attached")]
        public int? CanvasRight { get; set; }
        /// <summary>Used by <see cref="Canvas"/>'s children.</summary>
        [Category("Attached")]
        public int? CanvasBottom { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Category("Attached")]
        public int Row { get => GridRow; set => GridRow = value; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Category("Attached")]
        public int Column { get => GridColumn; set => GridColumn = value; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Category("Attached")]
        public int RowSpan { get => GridRowSpan; set => GridRowSpan = value; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [Category("Attached")]
        public int ColumnSpan { get => GridColumnSpan; set => GridColumnSpan = value; }

        /// <summary>If true, this object can have <see cref="Setter"/>s applied to its properties.<para/>
        /// Default value: true</summary>
        [Category("Appearance")]
        public bool IsStyleable
        {
            get => _isStyleable;
            set { _isStyleable = value; ExplicitlySetProperties.Add(nameof(IsStyleable)); }
        }
        private bool _isStyleable = true;
        [Category("Appearance")]
        public List<Style> Styles { get; set; } = new();
        /// <summary>The names of the named <see cref="Style"/>s that should be applied to this <see cref="Element"/>.<br/>
        /// Use a comma to delimit multiple names, such as: "Style1,Style2<br/>
        /// to apply <see cref="Style"/> with <see cref="Style.Name"/>="Style1" and <see cref="Style"/> with <see cref="Style.Name"/>="Style2" to this <see cref="Element"/><para/>
        /// See also: <see cref="Style.Name"/></summary>
        [Category("Appearance")]
        public string StyleNames { get; set; }

        [Category("Appearance")]
        public string ControlTemplate { get; set; }

        [Category("Attached")]
        public Dictionary<string, object> AttachedProperties { get; set; } = new();

        [Category("Attached")]
        public object Tag { get; set; }

        /// <param name="ApplyBaseSettings">If not null, this action will be invoked before <see cref="ApplySettings(MGElement, MGElement, bool)"/> executes.</param>
        public T ToElement<T>(MGWindow Window, MGElement Parent, Action<T> ApplyBaseSettings = null) 
            where T : MGElement
        {
            T Element = CreateElementInstance(Window, Parent) as T;
            ApplyBaseSettings?.Invoke(Element);
            ApplySettings(Parent, Element, true);
            return Element;
        }

        /// <param name="IncludeContent">Recommended value: true. If true, the child XAML content, if any, will also be processed.</param>
        protected internal void ApplySettings(MGElement Parent, MGElement Element, bool IncludeContent)
        {
            using (Element.BeginInitializing())
            {
                ApplyBaseSettings(Parent, Element, true);
                ApplyDerivedSettings(Parent, Element, IncludeContent);
            }
        }

        internal void ApplyBaseSettings(MGElement Parent, MGElement Element, bool IncludeBindings)
        {
            using (Element.BeginInitializing())
            {
                MGDesktop Desktop = Element.GetDesktop();

                if (Name != null)
                {
                    Element.Name = Name;
                }

                if (!string.IsNullOrWhiteSpace(ControlTemplate))
                {
                    Element.ControlTemplateName = ControlTemplate;
                }

                if (Margin.HasValue)
                {
                    Element.Margin = Margin.Value.ToThickness();
                }

                if (Padding.HasValue)
                {
                    Element.Padding = Padding.Value.ToThickness();
                }

                if (HorizontalAlignment.HasValue)
                {
                    Element.HorizontalAlignment = HorizontalAlignment.Value;
                }

                if (VerticalAlignment.HasValue)
                {
                    Element.VerticalAlignment = VerticalAlignment.Value;
                }

                if (HorizontalContentAlignment.HasValue)
                {
                    Element.HorizontalContentAlignment = HorizontalContentAlignment.Value;
                }

                if (VerticalContentAlignment.HasValue)
                {
                    Element.VerticalContentAlignment = VerticalContentAlignment.Value;
                }

                if (MinWidth.HasValue)
                {
                    Element.MinWidth = MinWidth.Value;
                }

                if (MinHeight.HasValue)
                {
                    Element.MinHeight = MinHeight.Value;
                }

                if (MaxWidth.HasValue)
                {
                    Element.MaxWidth = MaxWidth.Value;
                }

                if (MaxHeight.HasValue)
                {
                    Element.MaxHeight = MaxHeight.Value;
                }

                if (UseResponsiveLayout.HasValue)
                {
                    Element.UseResponsiveLayout = UseResponsiveLayout.Value;
                }

                if (ScaleSpacingWithResponsive.HasValue)
                {
                    Element.ScaleSpacingWithResponsive = ScaleSpacingWithResponsive.Value;
                }

                if (ScaleDimensionsWithResponsive.HasValue)
                {
                    Element.ScaleDimensionsWithResponsive = ScaleDimensionsWithResponsive.Value;
                }

                if (PreferredWidth.HasValue)
                {
                    Element.PreferredWidth = PreferredWidth.Value;
                }

                if (PreferredHeight.HasValue)
                {
                    Element.PreferredHeight = PreferredHeight.Value;
                }

                if (ToolTip != null)
                {
                    Element.ToolTip = ToolTip.ToElement<MGToolTip>(Element.SelfOrParentWindow, Element);
                }

                if (ContextMenu != null)
                {
                    Element.ContextMenu = ContextMenu.ToElement<MGContextMenu>(Element.SelfOrParentWindow, Element);
                }

                if (CanHandleInputsWhileHidden.HasValue)
                {
                    Element.CanHandleInputsWhileHidden = CanHandleInputsWhileHidden.Value;
                }

                if (IsHitTestVisible.HasValue)
                {
                    Element.IsHitTestVisible = IsHitTestVisible.Value;
                }

                if (IsSelected.HasValue)
                {
                    Element.IsSelected = IsSelected.Value;
                }

                if (IsEnabled.HasValue)
                {
                    Element.IsEnabled = IsEnabled.Value;
                }

                if (BackgroundRenderPadding.HasValue)
                {
                    Element.BackgroundRenderPadding = BackgroundRenderPadding.Value.ToThickness();
                }

                ApplyBackground(Element);
                Element.OverlayBrush = Overlay?.ToFillBrush(Desktop, Element);

                ApplyResourceReferences(Element, this, Element, MapTargetPath);

                // ADR-0005/S7 will replace LocalValue here with the XAML value's actual style provenance.
                if (TextForeground.HasValue)
                {
                    Element.SetDefaultTextForegroundSlot(UIValueSlot.Normal, TextForeground.Value.ToXNAColor(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
                }

                if (DisabledTextForeground.HasValue)
                {
                    Element.SetDefaultTextForegroundSlot(UIValueSlot.Disabled, DisabledTextForeground.Value.ToXNAColor(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
                }

                if (SelectedTextForeground.HasValue)
                {
                    Element.SetDefaultTextForegroundSlot(UIValueSlot.Selected, SelectedTextForeground.Value.ToXNAColor(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
                }

                if (Visibility.HasValue)
                {
                    Element.Visibility = Visibility.Value;
                }

                if (ClipToBounds.HasValue)
                {
                    Element.ClipToBounds = ClipToBounds.Value;
                }

                if (ResponsiveAnchor.HasValue)
                {
                    Element.ResponsiveAnchor = ResponsiveAnchor.Value;
                }

                if (Opacity.HasValue)
                {
                    Element.Opacity = Opacity.Value;
                }

                if (RenderScale.HasValue)
                {
                    Element.RenderScale = new(RenderScale.Value, RenderScale.Value);
                }

                Element.Tag = Tag;

                foreach (var (Source, Target, _) in GetBaseBindableObjects(Element))
                {
                    ApplyResourceReferences(Element, Source, Target, null);
                }

                foreach (var (Source, Target, _) in GetBindableObjects(Element))
                {
                    ApplyResourceReferences(Element, Source, Target, null);
                }

                if (IncludeBindings)
                {
                    //  Note: DataBindings are initialized later, once all XAML content is done parsing, (in Window.ToElement(Desktop, Theme))
                    //  Because ElementName references cannot be resolved until all named elements have been processed and added to the MGWindow instance.
                    //  So this logic temporarily copies binding information to the underlying target types and processes them later.

                    if (Bindings?.Any() == true)
                    {
                        Element.Bindings.AddRange(Bindings);
                    }

                    void CopyBindings(XAMLBindableBase Source, object Target, string TargetPath)
                    {
                        if (Source?.Bindings?.Any() == true && Target != null)
                        {
                            if (Target is not XAMLBindableBase BindableTarget)
                            {
                                Debug.WriteLine($"Warning - DataBinding(s) on XAML element '{Source.GetType().Name}' are ignored " +
                                    $"because the underlying target type ({Target.GetType().Name}) does not support DataBindings. " +
                                    $"Bindings in XAML should typically only be applied to properties belonging to {nameof(MGElement)} objects.");
                            }
                            else
                            {
                                BindableTarget.Bindings.AddRange(Source.Bindings);

                                //  Store the target object path in the element's metadata so the target objects can be dynamically retrieved later when the binding is being created
                                List<string> Paths;
                                if (!Element.Metadata.TryGetValue(BindingPathsMetadataKey, out object List))
                                {
                                    Paths = new List<string>();
                                    Element.Metadata.Add(BindingPathsMetadataKey, Paths);
                                }
                                else
                                {
                                    Paths = List as List<string>;
                                }
                                Paths.Add(TargetPath);
                            }

                            //TODO what about nested bindable objects, such as MGBorderedFillBrush.FillBrush?
                            //Current implementation ignores bindings defined in XAML that are nested on a XAMLBindableBase object and only processes the outer-most object (MGBorderedFillBrush).
                            //XAMLBindableBase.GetNestedBindableObjects() is intended to fix this issue but there are some problems with it such as the Target object
                            //      of the nested bindable object might be null, so there isn't anything to copy the binding data to.
                            static IEnumerable<XAMLBindableBase> RecurseNestedBindableObjects(XAMLBindableBase Current)
                            {
                                if (Current != null)
                                {
                                    foreach (var Item in Current.GetNestedBindableObjects())
                                    {
                                        if (Item.Item != null)
                                        {
                                            yield return Item.Item;
                                            foreach (XAMLBindableBase Nested in RecurseNestedBindableObjects(Item.Item))
                                            {
                                                yield return Nested;
                                            }
                                        }
                                    }
                                }
                            }
                            if (RecurseNestedBindableObjects(Source).Any(x => x.Bindings?.Any() == true))
                            {
                                foreach (XAMLBindableBase nested in RecurseNestedBindableObjects(Source))
                                {
                                    if (nested.Bindings?.Any() == true)
                                    {
                                        foreach (var binding in nested.Bindings)
                                        {
                                            Debug.WriteLine(
                                                $"[WARN] DataBinding on nested {nameof(XAMLBindableBase)} is not supported and will be ignored. " +
                                                $"Path='{TargetPath}.{binding.TargetPath}'. " +
                                                $"Consider binding directly on the enclosing {nameof(MGElement)} property instead.");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    foreach (var (Source, Target, TargetPath) in GetBaseBindableObjects(Element))
                    {
                        CopyBindings(Source, Target, TargetPath);
                    }

                    foreach (var (Source, Target, TargetPath) in GetBindableObjects(Element))
                    {
                        CopyBindings(Source, Target, TargetPath);
                    }
                }
            }
        }

        private IEnumerable<(XAMLBindableBase Source, object Target, string TargetPath)> GetBaseBindableObjects(MGElement Element)
        {
            yield return (Background, Element.BackgroundBrush?.NormalValue, $"{nameof(MGElement.BackgroundBrush)}.{nameof(VisualStateFillBrush.NormalValue)}");
            yield return (Overlay, Element.OverlayBrush, $"{nameof(MGElement.OverlayBrush)}");
            yield return (DisabledBackground, Element.BackgroundBrush?.DisabledValue, $"{nameof(MGElement.BackgroundBrush)}.{nameof(VisualStateFillBrush.DisabledValue)}");
            yield return (SelectedBackground, Element.BackgroundBrush?.SelectedValue, $"{nameof(MGElement.BackgroundBrush)}.{nameof(VisualStateFillBrush.SelectedValue)}");
        }

        /// <summary>Returns a list of non-<see cref="MGElement"/> objects that support <see cref="DataBinding"/>s in XAML.<para/>
        /// Value1: the XAML type<br/>Value2: the the underlying type<br/>Value3: the path to the underlying type, starting from the <see cref="MGElement"/> object.<para/>
        /// This is usually fillbrushes and borderbrushes with bindable properties. 
        /// For example, a <see cref="Slider"/> would return a tuple consisting of <see cref="Slider.Foreground"/>, <see cref="MGSlider.Foreground"/>, and nameof(<see cref="MGSlider.Foreground"/>).</summary>
        /// <param name="Element"></param>
        protected virtual IEnumerable<(XAMLBindableBase Source, object Target, string TargetPath)> GetBindableObjects(MGElement Element) 
            => Enumerable.Empty<(XAMLBindableBase Source, object Target, string TargetPath)>();

        private void ApplyResourceReferences(MGElement HostElement, XAMLBindableBase Source, object Target, Func<string, string> ResolveTargetPath)
        {
            if (Source?.ResourceReferences?.Any() != true || Target == null)
            {
                return;
            }

            foreach (UIResourceReferenceConfig ResourceReference in Source.ResourceReferences)
            {
                string TargetPath = ResolveTargetPath?.Invoke(ResourceReference.TargetPath) ?? ResourceReference.TargetPath;
                UIResourceReferenceConfig AppliedReference = ResourceReference with { TargetPath = TargetPath };
                _ = UIResourceReferenceApplicator.Apply(HostElement, Target, AppliedReference, HostElement.GetResources());
            }
        }

        private string MapTargetPath(string TargetPath)
            => BindingPathMappings.TryGetValue(TargetPath, out string ActualPath) ? ActualPath : TargetPath;

        //  DataBindings are defined in XAML (so they are applied to the properties of the XAML types)
        //  but are bound to the properties of the actual type (such as MGUI.Core.UI.MGButton instead of MGUI.Core.UI.XAML.Button).
        //  This dictionary is intended to handle cases where a property on the XAML type isn't the same name/path as the actual property of the binding
        private static readonly Dictionary<string, string> BindingPathMappings = new()
        {
            { nameof(Background), $"{nameof(MGElement.BackgroundBrush)}.{nameof(VisualStateFillBrush.NormalValue)}" },
            { nameof(TextBlock.Foreground), $"{nameof(MGTextBlock.Foreground)}.{nameof(VisualStateFillBrush.NormalValue)}" },
            { nameof(Overlay), $"{nameof(MGElement.OverlayBrush)}" },
            { nameof(SelectedBackground), $"{nameof(MGElement.BackgroundBrush)}.{nameof(VisualStateFillBrush.SelectedValue)}" },
            { nameof(DisabledBackground), $"{nameof(MGElement.BackgroundBrush)}.{nameof(VisualStateFillBrush.DisabledValue)}" },
            { nameof(TextForeground), $"{nameof(MGElement.DefaultTextForeground)}.{nameof(VisualStateSetting<XNAColor>.NormalValue)}" },
            { nameof(SelectedTextForeground), $"{nameof(MGElement.DefaultTextForeground)}.{nameof(VisualStateSetting<XNAColor>.SelectedValue)}" },
            { nameof(DisabledTextForeground), $"{nameof(MGElement.DefaultTextForeground)}.{nameof(VisualStateSetting<XNAColor>.DisabledValue)}" },
            { nameof(Width), $"{nameof(MGElement.PreferredWidth)}" },
            { nameof(Height), $"{nameof(MGElement.PreferredHeight)}" },
            { nameof(ListBox.Items), $"{nameof(MGListBox<object>.ItemsSource)}" }
        };

        private const string BindingPathsMetadataKey = "TmpBindingPaths";

        /// <summary>Resolves any pending <see cref="BindingConfig"/>s by converting them into <see cref="DataBinding"/>s</summary>
        /// <param name="DataContextOverride">If not null, this value will be applied to the <see cref="MGElement.DataContextOverride"/> value of every element that is processed.<para/>
        /// If <paramref name="RecurseChildren"/> is false, this is only applied to <paramref name="Element"/>. Else it's applied to <paramref name="Element"/> and all its nested children.</param>
        internal static void ProcessBindings(MGElement Element, bool RecurseChildren, object DataContextOverride)
        {
            if (RecurseChildren)
            {
                foreach (MGElement Child in Element.TraverseVisualTree(true, true, true, true, MGElement.TreeTraversalMode.Preorder))
                {
                    ProcessBindings(Child, false, DataContextOverride);
                }
            }
            else
            {
                if (DataContextOverride != null)
                {
                    Element.DataContextOverride = DataContextOverride;
                }

                if (Element.Bindings?.Any() == true)
                {
                    foreach (BindingConfig Binding in Element.Bindings)
                    {
                        object TargetObject = Element;
                        BindingConfig PostProcessedBinding = Binding;

                        //  Handle some special-cases where the name of the XAML property isn't the same as the corresponding property on the c# object
                        if (BindingPathMappings.TryGetValue(Binding.TargetPath, out string ActualPath))
                        {
                            PostProcessedBinding = Binding with { TargetPath = ActualPath };
                        }

                        if (PostProcessedBinding.Converter is StringToToolTipConverter StringToolTipConverter)
                        {
                            StringToolTipConverter.Host = Element;
                        }

                        DataBindingManager.AddBinding(PostProcessedBinding, TargetObject);
                    }
                    Element.Bindings.Clear();
                }

                if (Element.Metadata.TryGetValue(BindingPathsMetadataKey, out object Items))
                {
                    if (Items is List<string> BindingPaths)
                    {
                        MGWindow Window = Element.SelfOrParentWindow;
                        List<XAMLBindableBase> Targets = new List<XAMLBindableBase>();

                        foreach (string Path in BindingPaths)
                        {
                            object Target = DataBinding.ResolvePath(Element, Path.Split('.'));
                            if (Target != null && Target is XAMLBindableBase BindableTarget && BindableTarget.Bindings?.Any() == true)
                            {
                                foreach (BindingConfig Binding in BindableTarget.Bindings)
                                {
                                    if (Binding.Converter is StringToToolTipConverter StringToolTipConverter)
                                    {
                                        StringToolTipConverter.Host = Element;
                                    }

                                    DataBindingManager.AddBinding(Binding, BindableTarget);
                                    Targets.Add(BindableTarget);
                                }
                            }
                        }

                        //  Copy the DataContext from the parent window to each binding target object
                        if (Targets.Any())
                        {
                            void UpdateDataContext(XAMLBindableBase Target)
                            {
                                if (Target.DataContext != Window.WindowDataContext)
                                {
                                    Target.DataContext = Window.WindowDataContext;
                                    Target.InvokeDataContextChanged();
                                }
                            }

                            foreach (var Item in Targets)
                            {
                                UpdateDataContext(Item);
                            }

                            Window.DataContextChanged += (sender, e) =>
                            {
                                foreach (var Item in Targets)
                                {
                                    UpdateDataContext(Item);
                                }
                            };
                        }
                    }
                    Element.Metadata.Remove(BindingPathsMetadataKey);
                }
            }
        }

        internal static void ApplyExplicitBackground(MGElement element, IFillBrush explicitBackground)
        {
            if (element == null || explicitBackground == null)
            {
                return;
            }

            // ADR-0005/S7 will replace LocalValue here with the XAML value's actual style provenance.
            element.SetBackgroundSlot(UIValueSlot.Normal, explicitBackground, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
            element.SetBackgroundSlot(UIValueSlot.Focused, explicitBackground.Copy(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        }

        protected void ApplyBackground(MGElement Element)
        {
            MGDesktop Desktop = Element.GetDesktop();

            if (Background != null)
            {
                ApplyExplicitBackground(Element, Background.ToFillBrush(Desktop, Element));
            }

            if (DisabledBackground != null)
            {
                Element.SetBackgroundSlot(UIValueSlot.Disabled, DisabledBackground.ToFillBrush(Desktop, Element), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
            }

            if (SelectedBackground != null)
            {
                Element.SetBackgroundSlot(UIValueSlot.Selected, SelectedBackground.ToFillBrush(Desktop, Element), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
            }

            if (BackgroundFocusedColor != null)
            {
                Element.SetBackgroundFocusedColor(BackgroundFocusedColor.Value.ToXNAColor(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
            }
        }

        protected abstract MGElement CreateElementInstance(MGWindow Window, MGElement Parent);
        /// <param name="IncludeContent">Recommended value: true. If true, child XAML content, if any, will also be processed.</param>
        protected internal abstract void ApplyDerivedSettings(MGElement Parent, MGElement Element, bool IncludeContent);

        protected internal abstract IEnumerable<Element> GetChildren();

        protected internal void ProcessStyles(MGResources Resources)
        {
            Dictionary<string, Style> StylesByName = new();
            foreach (KeyValuePair<string, Style> KVP in Resources.Styles)
            {
                StylesByName[KVP.Key] = KVP.Value;
            }

            MGResources Current = Resources.Parent;
            while (Current != null)
            {
                foreach (KeyValuePair<string, Style> KVP in Current.Styles)
                {
                    if (!StylesByName.ContainsKey(KVP.Key))
                    {
                        StylesByName.Add(KVP.Key, KVP.Value);
                    }
                }

                Current = Current.Parent;
            }

            // Pre-seed StylesByType with desktop-level implicit styles so they apply to all elements of their target type
            var StylesByType = new Dictionary<MGElementType, Dictionary<string, List<object>>>();
            foreach (var KVP in Resources.GetMergedImplicitStyles())
            {
                if (KVP.Value.Setters.Any())
                {
                    var ValuesByProperty = new Dictionary<string, List<object>>();
                    foreach (Setter Setter in KVP.Value.Setters)
                    {
                        if (!ValuesByProperty.TryGetValue(Setter.Property, out List<object> Values))
                        {
                            Values = new();
                            ValuesByProperty.Add(Setter.Property, Values);
                        }
                        Values.Add(Setter.Value);
                    }
                    StylesByType.Add(KVP.Key, ValuesByProperty);
                }
            }

            ProcessStyles(StylesByName, StylesByType);
        }
        private void ProcessStyles(Dictionary<string, Style> StylesByName, Dictionary<MGElementType, Dictionary<string, List<object>>> StylesByType)
        {
            Dictionary<string, List<object>> ValuesByProperty;

            //  Append current style setters to indexed data
            foreach (Style Style in Styles.Where(x => x.Setters.Any()))
            {
                if (Style.Name != null)
                {
                    StylesByName.Add(Style.Name, Style);
                }
                else
                {
                    MGElementType Type = Style.TargetType;
                    if (!StylesByType.TryGetValue(Type, out ValuesByProperty))
                    {
                        ValuesByProperty = new();
                        StylesByType.Add(Type, ValuesByProperty);
                    }

                    foreach (Setter Setter in Style.Setters)
                    {
                        string Property = Setter.Property;
                        if (!ValuesByProperty.TryGetValue(Property, out List<object> Values))
                        {
                            Values = new();
                            ValuesByProperty.Add(Property, Values);
                        }

                        Values.Add(Setter.Value);
                    }
                }
            }

            //  Apply the appropriate style setters to this instance
            if (IsStyleable)
            {
                Type ThisType = GetType();

                HashSet<string> ModifiedPropertyNames = new();

                //  Apply implicit styles (styles that aren't referenced by a Name)
                if (StylesByType.TryGetValue(ElementType, out ValuesByProperty))
                {
                    foreach (KeyValuePair<string, List<object>> KVP in ValuesByProperty)
                    {
#if DEBUG
                        //  Sanity check
                        if (KVP.Value.Count == 0)
                        {
                            throw new InvalidOperationException($"{nameof(Element)}.{nameof(ProcessStyles)}.{nameof(ValuesByProperty)} should never be empty. The indexed data might not be properly updated.");
                        }
#endif

                        string PropertyName = KVP.Key;
                        PropertyInfo PropertyInfo = ThisType.GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance); // | BindingFlags.IgnoreCase?
                        if (PropertyInfo != null)
                        {
                            if (ModifiedPropertyNames.Contains(PropertyName) || IsXAMLPropertyUnset(PropertyInfo))
                            {
                                TypeConverter Converter = TypeDescriptor.GetConverter(PropertyInfo.PropertyType);
                                foreach (object Value in KVP.Value)
                                {
                                    if (Value is string StringValue)
                                    {
                                        PropertyInfo.SetValue(this, Converter.ConvertFrom(null, CultureInfo.InvariantCulture, StringValue));
                                    }
                                    else
                                    {
                                        PropertyInfo.SetValue(this, Value);
                                    }
                                }

                                ModifiedPropertyNames.Add(PropertyName);
                            }
                        }
                    }
                }

                //  Apply explicit styles (styles that were explicitly referenced by their Name)
                if (StyleNames != null)
                {
                    string[] Names = StyleNames.Split(',');
                    List<Style> ExplicitStyles = Names.Select(x => StylesByName[x]).Where(x => x.TargetType == ElementType).ToList();

                    //  Get all the properties that the explicit styles will modify
                    HashSet<string> PropertyNames = ExplicitStyles.SelectMany(x => x.Setters).Select(x => x.Property).ToHashSet();
                    Dictionary<string, PropertyInfo> PropertiesByName = new();
                    foreach (string PropertyName in PropertyNames)
                    {
                        PropertyInfo PropertyInfo = ThisType.GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance); // | BindingFlags.IgnoreCase?
                        if (PropertyInfo != null)
                        {
                            if (ModifiedPropertyNames.Contains(PropertyName) || IsXAMLPropertyUnset(PropertyInfo))
                            {
                                PropertiesByName.Add(PropertyName, PropertyInfo);
                            }
                        }
                    }

                    //  Apply the values of each setter
                    foreach (Style Style in ExplicitStyles)
                    {
                        foreach (Setter Setter in Style.Setters)
                        {
                            string PropertyName = Setter.Property;
                            if (PropertiesByName.TryGetValue(PropertyName, out PropertyInfo PropertyInfo))
                            {
                                TypeConverter Converter = TypeDescriptor.GetConverter(PropertyInfo.PropertyType);
                                if (Setter.Value is string StringValue)
                                {
                                    PropertyInfo.SetValue(this, Converter.ConvertFrom(null, CultureInfo.InvariantCulture, StringValue));
                                }
                                else
                                {
                                    PropertyInfo.SetValue(this, Setter.Value);
                                }

                                ModifiedPropertyNames.Add(PropertyName);
                            }
                        }
                    }
                }
            }

            //  Recursively process all children
            foreach (Element Child in GetChildren())
            {
                if (Child.InheritsParentStyles)
                {
                    Child.ProcessStyles(StylesByName, StylesByType);
                }
                else
                {
                    Child.ProcessStyles(new Dictionary<string, Style>(), new Dictionary<MGElementType, Dictionary<string, List<object>>>());
                }
            }

            //  Remove current style setters from indexed data
            foreach (Style Style in Styles.Where(x => x.Setters.Any()))
            {
                if (Style.Name != null)
                {
                    StylesByName.Remove(Style.Name);
                }
                else
                {
                    MGElementType Type = Style.TargetType;
                    if (StylesByType.TryGetValue(Type, out ValuesByProperty))
                    {
                        foreach (Setter Setter in Style.Setters)
                        {
                            string Property = Setter.Property;
                            if (ValuesByProperty.TryGetValue(Property, out List<object> Values))
                            {
                                if (Values.Remove(Setter.Value) && Values.Count == 0)
                                {
                                    ValuesByProperty.Remove(Property);
                                    if (ValuesByProperty.Count == 0)
                                    {
                                        StylesByType.Remove(Type);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Returns <see langword="true"/> when a XAML property has NOT been explicitly set by the XAML
        /// parser, meaning a style setter may override it.
        /// <list type="bullet">
        /// <item>Non-nullable value types (<c>bool</c>, <c>int</c>, <c>enum</c>, …): relies on
        ///   <see cref="ExplicitlySetProperties"/>.  If absent from that set the property is still at its
        ///   C# default, i.e. effectively unset.</item>
        /// <item>Nullable value types (<c>int?</c>, <c>bool?</c>, …) and reference types: a
        ///   <see langword="null"/> value means "not set" because the XAML parser never called the
        ///   setter.</item>
        /// </list>
        /// Called from <see cref="ProcessStyles(MGResources)"/> to guard against style overrides.
        /// </summary>
        private bool IsXAMLPropertyUnset(PropertyInfo pi)
        {
            // Explicit-tracking wins: if the setter was called during XAML parsing, the
            // property was intentionally set — the style must not override it.
            if (ExplicitlySetProperties.Contains(pi.Name))
            {
                return false;
            }

            Type type = pi.PropertyType;

            // Non-nullable value type: the boxed value is never null, so a null-check
            // would always report "not set".  Without an entry in ExplicitlySetProperties
            // the property is at its C# default and is therefore effectively unset.
            if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                return true;
            }

            // Reference type or Nullable<T>: null ↔ "not set by the XAML parser".
            return pi.GetValue(this) == null;
        }
    }

    public class ElementStringConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }

            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
            {
                TextBlock TextBlock = new() { Text = stringValue };
                return TextBlock;
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}