using MGUI.Shared.Helpers;
using MGUI.Shared.Text.Engines;
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGame.Extended;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.ObjectModel;
using MGUI.Core.UI.Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Rendering;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Data_Binding;
using MGUI.Core.UI.Styling;
using System.ComponentModel;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.DragDrop;
using MGUI.Core.UI.Shapes;
using MGUI.Core.UI.Responsive;
using MGUI.Core.Tooling;
using MGUI.Shared.Rendering.Clipping;
using System.Threading;

namespace MGUI.Core.UI
{
    public readonly record struct ElementUpdateArgs(UpdateBaseArgs BA, bool IsEnabled, bool IsSelected, bool IsHitTestVisible, Point Offset, Rectangle ActualLayoutBounds)
    {
        public ElementUpdateArgs AsZeroOffset() => this with { Offset = Point.Zero };
        public ElementUpdateArgs ChangeOffset(Point Value) => Offset == Value ? this : this with { Offset = Value };
        public ElementUpdateArgs ChangeHitTestVisible(bool Value) => IsHitTestVisible == Value ? this : this with { IsHitTestVisible = Value };
    };

    public readonly record struct ElementDrawArgs(DrawBaseArgs BA, VisualState VisualState, Point Offset)
    {
        public TimeSpan TS => BA.TS;
        public IUIRenderContext Context => BA.Context;
        public IUIDrawTransaction DT => BA.DT;
        public float Opacity => BA.Opacity;
        public bool IsEnabled => !VisualState.IsDisabled;
        public bool IsSelected => VisualState.IsSelected;

        public ElementDrawArgs SetOpacity(float Value) => this with { BA = BA.SetOpacity(Value) };
        public ElementDrawArgs SetOffset(Point Value) => this with { Offset = Value };

        public ElementDrawArgs AsZeroOffset() => this with { Offset = Point.Zero };
    }

    public readonly record struct ElementMeasurement(Size AvailableSize, Thickness RequestedSize, Thickness SharedSize, Thickness ContentSize)
	{
		public bool IsAvailableSizeGreaterThan(Size Other) => AvailableSize.Width > Other.Width && AvailableSize.Height > Other.Height;
        public bool IsAvailableSizeGreaterThanOrEqual(Size Other) => AvailableSize.Width >= Other.Width && AvailableSize.Height >= Other.Height;
        public bool IsAvailableSizeLessThan(Size Other) => AvailableSize.Width < Other.Width && AvailableSize.Height < Other.Height;
        public bool IsAvailableSizeLessThanOrEqual(Size Other) => AvailableSize.Width <= Other.Width && AvailableSize.Height <= Other.Height;

        public bool IsRequestedSizeGreaterThan(Size Other) => RequestedSize.Width > Other.Width && RequestedSize.Height > Other.Height;
        public bool IsRequestedSizeGreaterThanOrEqual(Size Other) => RequestedSize.Width >= Other.Width && RequestedSize.Height >= Other.Height;
        public bool IsRequestedSizeLessThan(Size Other) => RequestedSize.Width < Other.Width && RequestedSize.Height < Other.Height;
        public bool IsRequestedSizeLessThanOrEqual(Size Other) => RequestedSize.Width <= Other.Width && RequestedSize.Height <= Other.Height;
    }

    /// <param name="PressedScale">A scale to apply to the target element when <see cref="MGElement.IsLMBPressed"/> is true</param>
    /// <param name="HoveredScale">A scale to apply to the target element when <see cref="MGElement.IsHovered"/> is true</param>
    public readonly record struct ConditionalScaleTransform(float PressedScale, float HoveredScale)
    {
        public bool TryGetScale(VisualState VS, out float Scale)
        {
            if (VS.Secondary == SecondaryVisualState.Pressed)
            {
                Scale = PressedScale;
                return true;
            }
            else if (VS.Secondary == SecondaryVisualState.Hovered)
            {
                Scale = HoveredScale;
                return true;
            }
            else
            {
                Scale = 1.0f;
                return false;
            }
        }
    }

    /// <summary>Event args for <see cref="MGElement.ContextMenuRequested"/>.
    /// Allows subscribers to provide or replace the <see cref="MGContextMenu"/> that will be opened
    /// when the user right-clicks the element, without needing to assign <see cref="MGElement.ContextMenu"/>.</summary>
    public class ContextMenuRequestedEventArgs : EventArgs
    {
        /// <summary>The context menu that will be opened, initially equal to the element's
        /// <see cref="MGElement.ContextMenu"/> property. Set this to a different instance to
        /// replace the menu, or to <see langword="null"/> to suppress opening entirely.</summary>
        public MGContextMenu Menu { get; set; }

        /// <summary>Mouse position in screen space at the moment of the right-click.</summary>
        public Point Position { get; }

        /// <summary>Set to <see langword="true"/> to suppress opening the menu entirely.
        /// If <see langword="false"/> (default), the menu referenced by <see cref="Menu"/> will be opened.</summary>
        public bool Handled { get; set; }

        public ContextMenuRequestedEventArgs(MGContextMenu InitialMenu, Point Position)
        {
            Menu = InitialMenu;
            this.Position = Position;
            Handled = false;
        }
    }

    //TODO:
    //FIXED Bug 1 (see MeasureSelf): shared component sizes now use element-wise MAX instead of SUM.
    //      When multiple components have IsWidthSharedWithContent=true or IsHeightSharedWithContent=true,
    //      their contributions to SharedSize are now max'd (not summed), so the measured total size is
    //      Max(comp1, comp2, ..., Content) rather than comp1+comp2+...+Max(0,Content-sum).
    //REMAINING Bug 2: components aren't able to share their size with the Padding.
    //      This probably only matters in cases where a component doesn't use the owner's padding (like MGTabControl)
    //      So it's causing the total measured dimensions to be Sum(Padding, UnsharedComponentSize, Max(ContentSize, SharedComponentSize)
    //      instead of something like: Sum(Max(Padding, UnsharedComponentSize), Max(ContentSize, SharedComponentSize))
    //      Fixing this requires tracking a "MaxPaddingAndUnsharedComponent" which is more invasive.
    //Make ItemsSource bindable in combobox/listbox/listview/grid/unfiromgrid
    //      for example: ComboBox could have "public MGBinding ItemsSource"
    //      then in MGComboBox.LoadSettings, if ItemsSource binding is not null,
    //      create a new kind of DataBinding that allows you to pass in a delegate for setting the target object's property (so it would just ignore bindingconfig.targetpath i guess)
    //      the delegate would be: "x => this.SetItemsSource(x as ICollection<TDataType>);"
    //          and remove the ItemsSourceBinding from the PendingBindings list in XAML/Element.cs -> ProcessBindings()
    //      also make SelectedItem(s) and SelectedIndex bindable
    //Fix issue where the parsed lines of text get screwed up when selecting empty lines of text in a TextBox.
    //Fix DataBinding to properties that dont have a corresponding property in their XAML class or where the property setter isn't public.
    //      Also some properties aren't exposed on the XAML classes like MGListBox.SelectedItem, so should make a property like SelectedItemBinding or something
    //      (so you can't explicitly set the value, but you can still initialize an MGBinding for it)
    //maybe a way to register your own custom text markdown?
    //      RegisterFormattingCode(string Name, settings...)
    //In Element.ProcessStyles method, we need a better way to detect which properties do or don't have an explicit value in the XAML.
    //      maybe there's a way to be notified by the XAML processor when it sets a value?
    //      in XAMLParser.cs we could try using XamlServices.Load(XamlReader) instead of XamlServices.Parse, maybe that somehow lets us detect StartMember xml nodes or something
    //      so we can keep track of the member names we wrote to the objects.
    //      Alternatively, could look for any properties in XAML/Controls.cs that are initialized to a new object, such as ProgressBar.ValueTextBlock
    //          and initialize them to null instead, but if they are null in ApplyDerivedSettings, then initialize them to a new default object. (since ApplyDerivedSettings
    //          should be invoked after ProcessStyles has been invoked)
    //Is there a way to specify line break '\n' character inside text content of a XAML TextBlock?
    //      This works because we can re-encode '\n' as "&#38;#x0a;" which is processed as a '\n' character by System.Xaml.XamlServices.Parse: <TextBlock Text="Hello\nWorld" />
    //      But what would we do in this case?: <TextBlock>Hello\nWorld</TextBlock> (other than setting xml:space="preserve", which prevents you from leveraging text wrapping in the Xaml designer)
    //Remember this fix? https://github.com/Videogamers0/MGUI/commit/45f249ecf48b29e563e4ea552bb7872af673fbcd
    //      Check if the same problem is affecting scrollable ListBoxes too, especially in this line of code in MGListBox's constructor:
    //      MGListBoxItem<TItemType> PressedItem = InternalItems?.FirstOrDefault(x => x.ContentPresenter.IsHovered);
    //Maybe make a CompositedBorderBrush, like CompositedFillBrush it's just a wrapper for 0-many border brushes that are drawn in sequence overtop of each other.
    //something for mouse cursors?
    //		maybe an enum MouseCursorType
    //		and MGElement would have MouseCursorType Cursor property
    //		then MGDesktop defines some textures (and drawing offsets) associated with each MouseCursorType, and bool UseHardwareCursor
    //		If usehardwarecursor=false, then after drawing the desktop, get the hovered element, and draw the texture associated with its MGElement.Cursor
    //something really basic for Gamepads? Maybe just a simple way to 'spoof' a mousestate that's hovering a particular MGElement
    //		like MGElement.NavigateTo, Dictionary<Direction, MGElement> Neighbors. So if you press GamePad Left, it would basically just 
    //		get the hovered element, and call hovered.Neighbors[Left]?.NavigateTo() which returns a new MouseState to use for next update tick? idk
    //textblock inline formatting:
    //      inlined images should have option for render size AND layout size, so you could, for example, have a zero-width image underneath specific text in the textblock
    //Improve Grid/UniformGrid's default selection graphics
    //Bugfix MGTextBox's Caret positioning after moving to new line such as when inserting a linebreak
    //      only seems incorrect if the textbox's height changes? (I.E. it doesnt have a PreferredHeight and its not inside a ScrollViewer)
    //      it's just the screen position though, the indices in the text seem correct
    //statusbar, menubar/menuitems
    //      messagebox
    //          has icon docked left
    //          button choices like YesNoCancel, OKCancel, or even custom where you can call AddButton(mgelement content) and they are appended in order etc
    //          also 'commandlinks' like taskdialog
    //dialoguebox
    //      subclass of window, but mostly invisible
    //      left is an optional image to display character portrait
    //      rest is a border with a gradientfill background that displays a dockpanel
    //      dockpanel has user choices on the bottom (optional)
    //      rest is a textblock for the character's dialogue
    //maybe a subclass of MGImage for showing animations? Automatically cycles through a set list of textures/sourcerects without invoking LayoutChanged each time
    //		under the assumption each frame of the animation is same size. MGAnimatedImage(bool IsUniform) (if !IsUniform, has to invoke LayoutChanged)
    //maybe MGElement should have a: List<MGElement> AttachedElements { get; }
    //		This would specifically be for elements where the parent doesn't normally have a reference to the child, such as MGResizeGrip when using MGResizeGrip.Host to attach to
    //		The Visual Tree traversal logic should have an additional parameter, IncludeAttached
    /// <summary>Base class for all UI elements.</summary>
    public abstract class MGElement : XAMLBindableBase, IMouseHandlerHost, IKeyboardHandlerHost, 
        IElementNameResolver, IResourcesResolver, IDesktopResolver
    {
        public string UniqueId { get; }

		public MGDesktop GetDesktop() => SelfOrParentWindow.Desktop;
        /// <summary>Resolves the effective theme from the current resource scope.</summary>
        public MGTheme GetTheme() => GetResources().DefaultTheme;
            private MGResources _LocalResources;
            public MGResources LocalResources => _LocalResources;
            public MGResources GetResources() => _LocalResources ?? GetInheritedResources();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, MGElement> _TemplateParts = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, MGElement> TemplateParts => _TemplateParts;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, object> _AppliedTemplateDefaults = new(StringComparer.Ordinal);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly HashSet<string> _InstantiatedTemplatePartNames = new(StringComparer.Ordinal);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGControlTemplate _AppliedStructuredTemplate;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGControlTemplateStructure _AppliedTemplateStructure;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGControlTemplate _ControlTemplate;
        public string AppliedControlTemplateName => ControlTemplate?.Name ?? ResolveControlTemplateName();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string LastControlTemplateError { get; private set; }

        public MGControlTemplate ControlTemplate
        {
            get => _ControlTemplate;
            set
            {
                if (_ControlTemplate != value)
                {
                    _ControlTemplate = value;
                    ApplyControlTemplate(false);
                    NPC(nameof(ControlTemplate));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _ControlTemplateName;
        public string ControlTemplateName
        {
            get => _ControlTemplateName;
            set
            {
                if (_ControlTemplateName != value)
                {
                    _ControlTemplateName = value;
                    ApplyControlTemplate(false);
                    NPC(nameof(ControlTemplateName));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _DefaultControlTemplateName;
        public string DefaultControlTemplateName
        {
            get => _DefaultControlTemplateName;
            set
            {
                if (_DefaultControlTemplateName != value)
                {
                    _DefaultControlTemplateName = value;
                    ApplyControlTemplate(false);
                    NPC(nameof(DefaultControlTemplateName));
                }
            }
        }

        protected internal virtual string ResolveControlTemplateName()
        {
            if (!string.IsNullOrWhiteSpace(ControlTemplateName))
            {
                return ControlTemplateName;
            }

            MGTheme Theme = GetTheme();
            if (Theme != null)
            {
                if (Theme.TryGetControlTemplateMapping(GetType(), out string RuntimeTypeTemplateName)
                    && !string.IsNullOrWhiteSpace(RuntimeTypeTemplateName))
                {
                    return RuntimeTypeTemplateName;
                }

                if (Theme.TryGetControlTemplateMapping(ElementType, out string ThemeTemplateName)
                    && !string.IsNullOrWhiteSpace(ThemeTemplateName))
                {
                    return ThemeTemplateName;
                }
            }

            return DefaultControlTemplateName;
        }

        public bool TryGetElementByName(string Name, out MGElement NamedElement) => SelfOrParentWindow.TryGetElementByName(Name, out NamedElement);
        public bool TryGetTemplatePart(string Name, out MGElement Part) => _TemplateParts.TryGetValue(Name, out Part);

        protected internal virtual IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
            => Enumerable.Empty<MGControlTemplatePartRequirement>();

        protected internal virtual void ValidateControlTemplateParts()
        {
            string availableParts = _TemplateParts.Any()
                ? string.Join(", ", _TemplateParts.Select(x => $"{x.Key}:{x.Value?.GetType().Name ?? nameof(MGElement)}"))
                : "<none>";

            foreach (MGControlTemplatePartRequirement Requirement in GetRequiredControlTemplateParts())
            {
                if (!_TemplateParts.TryGetValue(Requirement.Name, out MGElement Part))
                {
                    if (Requirement.IsRequired)
                    {
                        throw new InvalidOperationException(
                            $"Control template '{ControlTemplate?.Name ?? ResolveControlTemplateName() ?? "<unnamed>"}' for '{GetType().Name}' is missing required part '{Requirement.Name}' of type '{Requirement.PartType.Name}'. Available parts: {availableParts}.");
                    }

                    continue;
                }

                if (Part != null && !Requirement.PartType.IsAssignableFrom(Part.GetType()))
                {
                    throw new InvalidOperationException(
                        $"Control template '{ControlTemplate?.Name ?? ResolveControlTemplateName() ?? "<unnamed>"}' for '{GetType().Name}' requires part '{Requirement.Name}' to be assignable to '{Requirement.PartType.Name}', but got '{Part.GetType().Name}'. Available parts: {availableParts}.");
                }
            }
        }

        internal bool TryGetAppliedTemplateDefault<T>(string Name, out T Value)
        {
            if (_AppliedTemplateDefaults.TryGetValue(Name, out object Existing))
            {
                if (Existing is UIResolvedValue<T> Resolved)
                {
                    Value = Resolved.Value;
                    return Resolved.IsSet;
                }

                if (Existing is T Typed)
                {
                    Value = Typed;
                    return true;
                }
            }

            Value = default;
            return false;
        }

        internal bool TryGetAppliedTemplateDefault<T>(string Name, out UIResolvedValue<T> Value)
        {
            if (_AppliedTemplateDefaults.TryGetValue(Name, out object Existing))
            {
                if (Existing is UIResolvedValue<T> Resolved)
                {
                    Value = Resolved;
                    return Resolved.IsSet;
                }

                if (Existing is T Typed)
                {
                    Value = new(Typed, UIValueResolutionSource.Template(UIInvalidationKind.Draw, Name));
                    return true;
                }
            }

            Value = UIResolvedValue<T>.Unset(UIInvalidationKind.Draw);
            return false;
        }

        internal void SetAppliedTemplateDefault<T>(string Name, T Value)
            => SetAppliedTemplateDefault(Name, new UIResolvedValue<T>(Value, UIValueResolutionSource.Template(UIInvalidationKind.Draw, Name)));

        internal void SetAppliedTemplateDefault<T>(string Name, UIResolvedValue<T> Value)
            => _AppliedTemplateDefaults[Name] = Value;

        protected internal void RegisterTemplatePart(string Name, MGElement Part)
        {
            if (string.IsNullOrWhiteSpace(Name) || Part == null)
            {
                return;
            }

            _TemplateParts[Name] = Part;
            if (ControlTemplate != null)
            {
                ApplyControlTemplate(false);
            }
        }

        private void RegisterInstantiatedTemplateStructure(MGControlTemplateStructure Structure)
        {
            if (Structure == null)
            {
                return;
            }

            foreach (KeyValuePair<string, MGElement> KVP in Structure.Parts)
            {
                if (!string.IsNullOrWhiteSpace(KVP.Key) && KVP.Value != null)
                {
                    _TemplateParts[KVP.Key] = KVP.Value;
                    _InstantiatedTemplatePartNames.Add(KVP.Key);
                }
            }
        }

        private void ClearInstantiatedTemplateStructure()
        {
            if (_AppliedTemplateStructure == null)
            {
                return;
            }

            foreach (string PartName in _InstantiatedTemplatePartNames)
            {
                _TemplateParts.Remove(PartName);
            }
            _InstantiatedTemplatePartNames.Clear();

            if (_AppliedTemplateStructure.Root != null && this is MGSingleContentHost SingleContentHost && ReferenceEquals(SingleContentHost.Content, _AppliedTemplateStructure.Root))
            {
                using (SingleContentHost.AllowChangingContentTemporarily())
                {
                    SingleContentHost.SetContent(null as MGElement);
                }
            }

            _AppliedTemplateStructure = null;
            _AppliedStructuredTemplate = null;
        }

        /// <summary>Attaches a structure created by a <see cref="MGControlTemplate"/>.
        /// The default runtime path supports single-content hosts; more specialized controls can override this hook.</summary>
        protected internal virtual void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
        {
            if (Structure?.Root == null)
            {
                return;
            }

            if (this is MGSingleContentHost SingleContentHost)
            {
                using (SingleContentHost.AllowChangingContentTemporarily())
                {
                    SingleContentHost.SetContent(Structure.Root);
                }
                return;
            }

            throw new InvalidOperationException($"{GetType().Name} requires a custom {nameof(AttachControlTemplateStructure)} override to consume structural control templates.");
        }

        protected internal virtual void ApplyControlTemplate(bool IsThemeRefresh)
        {
            MGControlTemplate Template = ControlTemplate;
            string ResolvedTemplateName = ResolveControlTemplateName();
            if (Template == null && !string.IsNullOrWhiteSpace(ResolvedTemplateName))
            {
                GetResources().TryGetControlTemplate(ResolvedTemplateName, out Template);
            }

            try
            {
                bool TemplateChanged = !ReferenceEquals(_AppliedStructuredTemplate, Template);
                if (TemplateChanged)
                {
                    ClearInstantiatedTemplateStructure();
                }

                if (Template?.SupportsStructure == true && (_AppliedTemplateStructure == null || TemplateChanged))
                {
                    MGControlTemplateContext Context = new(this, false);
                    MGControlTemplateStructure Structure = Template.CreateStructure(Context);
                    if (Structure != null)
                    {
                        RegisterInstantiatedTemplateStructure(Structure);
                        ValidateControlTemplateParts();

                        if (Template.SupportsAttachment)
                        {
                            Template.AttachStructure(Context, Structure);
                        }
                        else
                        {
                            AttachControlTemplateStructure(Structure);
                        }

                        _AppliedTemplateStructure = Structure;
                        _AppliedStructuredTemplate = Template;
                    }
                }

                if (Template != null && _AppliedTemplateStructure == null)
                {
                    ValidateControlTemplateParts();
                }

                Template?.Apply(this, IsThemeRefresh);
                LastControlTemplateError = null;
            }
            catch (Exception ex)
            {
                LastControlTemplateError = ex.Message;
                throw;
            }
        }

            protected MGResources GetInheritedResources()
            {
                if (Parent != null)
                {
                    return Parent.GetResources();
                }
                else if (ParentWindow != null)
                {
                    return ParentWindow.GetResources();
                }
                else
                {
                    return GetDesktop().Resources;
                }
            }

            public MGResources EnsureResourceScope(UIResourceScope Scope = UIResourceScope.Subtree)
            {
                if (_LocalResources == null)
                {
                    _LocalResources = new(GetInheritedResources(), Scope);
                    _LocalResources.OnDefaultThemeChanged += (_, e) => NotifyThemeChanged(e.PreviousTheme, e.Theme);
                }
                else
                {
                    _LocalResources.SetParent(GetInheritedResources());
                }

                return _LocalResources;
            }

            internal void NotifyThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
            {
                OnThemeChanged(PreviousTheme, CurrentTheme);
                ApplyControlTemplate(true);

                UIInvalidationKind Invalidation = GetThemeInvalidation(PreviousTheme, CurrentTheme);
                if ((Invalidation & (UIInvalidationKind.Measure | UIInvalidationKind.Arrange | UIInvalidationKind.Structure)) != 0)
                {
                    InvalidateLayout();
                }

                IReadOnlyList<MGElement> Children = GetVisualTreeChildren(true, true);
                for (int i = 0; i < Children.Count; i++)
                {
                    MGElement Child = Children[i];
                    if (Child.LocalResources == null)
                    {
                        Child.NotifyThemeChanged(PreviousTheme, CurrentTheme);
                    }
                }

                foreach (MGComponentBase Component in Components)
                {
                    if (Component.BaseElement.LocalResources == null)
                    {
                        Component.BaseElement.NotifyThemeChanged(PreviousTheme, CurrentTheme);
                    }
                }
            }

            protected internal virtual UIInvalidationKind GetThemeInvalidation(MGTheme PreviousTheme, MGTheme CurrentTheme)
                => UIInvalidationKind.Draw;

            protected internal virtual void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme) { }

            /// <summary>
            /// Invoked directly by <see cref="MGDesktop.FocusedKeyboardHandler"/>'s setter whenever this element gains or loses keyboard focus,
            /// immediately before <see cref="MGDesktop.FocusedKeyboardHandlerChanged"/> is raised.<para/>
            /// This is a self-only notification (no subscription needed, no leak risk): it fires only for the element whose focus state changed,
            /// not for arbitrary focus transitions between other elements. Overriding this instead of subscribing to
            /// <see cref="MGDesktop.FocusedKeyboardHandlerChanged"/> avoids rooting this element to the desktop for its whole lifetime.</summary>
            /// <param name="gained">True if this element just became the <see cref="MGDesktop.FocusedKeyboardHandler"/>, false if it just stopped being it.</param>
            protected internal virtual void OnKeyboardFocusChanged(bool gained) { }

        /// <summary>
        /// Optional per-element <see cref="ITextMeasurementEngine"/> override.
        /// When set, this element (and any children that call <see cref="GetTextEngine"/>) will use this engine
        /// instead of the desktop-level one.
        /// </summary>
        public ITextMeasurementEngine TextEngineOverride { get; set; }

        /// <summary>
        /// Returns the <see cref="ITextMeasurementEngine"/> to use for this element.
        /// Walks up the visual tree: first non-null <see cref="TextEngineOverride"/> wins;
        /// if none found, falls back to <see cref="MGDesktop.TextEngine"/>.
        /// </summary>
        public ITextMeasurementEngine GetTextEngine()
        {
            MGElement current = this;
            while (current != null)
            {
                if (current.TextEngineOverride != null)
                {
                    return current.TextEngineOverride;
                }

                current = current.Parent;
            }
            return GetDesktop().TextEngine;
        }

		/// <summary>The <see cref="MGWindow"/> that this <see cref="MGElement"/> belongs to. This value is only null if this <see cref="MGElement"/> is an <see cref="MGWindow"/> with no parent.</summary>
		public MGWindow ParentWindow { get; }
        /// <summary>Returns a reference to 'this' if this is an instance of <see cref="MGWindow"/>. Else returns <see cref="ParentWindow"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MGWindow SelfOrParentWindow => IsWindow ? this as MGWindow : ParentWindow;

        /// <summary>Process-wide counter incremented every time any <see cref="MGElement"/>'s visual <see cref="Parent"/> actually changes (see <see cref="SetParent(MGElement)"/>).<para/>
        /// Used to invalidate the <see cref="DisplayingWindow"/> cache of every element without having to walk the tree on re-parent: a re-parented subtree root
        /// invalidates its whole subtree implicitly, at the cost of one chain walk per element on its next hit-test after any topology change anywhere.<para/>
        /// See: <c>Docs/decisions/0004-hit-test-occlusion-from-displaying-window.md</c></summary>
        private static int _TreeTopologyGeneration;

        private MGWindow _DisplayingWindow;
        private int _DisplayingWindowGeneration = -1;

        /// <summary>The <see cref="MGWindow"/> that actually displays this <see cref="MGElement"/> right now: itself when it is a window,
        /// else the first <see cref="MGWindow"/> found by walking the visual <see cref="Parent"/> chain upward, falling back to <see cref="ParentWindow"/>
        /// if the chain ends without a window (e.g. a detached element).<para/>
        /// Unlike <see cref="SelfOrParentWindow"/> (the window this element was <i>constructed</i> with), this reflects re-parenting:
        /// an element built by one window's application code but re-parented into another window (for example, panel content re-parented
        /// into a floating dock window) resolves occlusion, <see cref="MGWindow.HasModalWindow"/>, <see cref="MGWindow.HoveredElement"/> and
        /// <see cref="MGWindow.PressedElement"/> from the window that displays it, not the window that built it.<para/>
        /// The result is cached and only recomputed when <see cref="_TreeTopologyGeneration"/> has changed since the last computation, so a
        /// lookup outside topology changes costs one integer comparison plus a field read.<para/>
        /// See: <c>Docs/decisions/0004-hit-test-occlusion-from-displaying-window.md</c></summary>
        internal MGWindow DisplayingWindow
        {
            get
            {
                int currentGeneration = Volatile.Read(ref _TreeTopologyGeneration);
                if (_DisplayingWindowGeneration != currentGeneration)
                {
                    _DisplayingWindowGeneration = currentGeneration;
                    if (IsWindow)
                    {
                        _DisplayingWindow = this as MGWindow;
                    }
                    else
                    {
                        MGElement current = Parent;
                        MGWindow found = null;
                        while (current != null)
                        {
                            if (current.IsWindow)
                            {
                                found = current as MGWindow;
                                break;
                            }
                            current = current.Parent;
                        }
                        _DisplayingWindow = found ?? ParentWindow;
                    }
                }
                return _DisplayingWindow;
            }
        }

        private object _DataContextOverride;
        /// <summary>If null, this element's <see cref="DataContext"/> is defaulted to the window's <see cref="MGWindow.WindowDataContext"/>.<para/>
        /// Note: <see cref="MGWindow"/> instances cannot have an override and will always use their <see cref="MGWindow.WindowDataContext"/> instead.<br/>
        /// For <see cref="MGWindow"/>, this property is overridden to set <see cref="MGWindow.WindowDataContext"/></summary>
        public virtual object DataContextOverride
        {
            get => _DataContextOverride;
            set
            {
                if (_DataContextOverride != value)
                {
                    _DataContextOverride = value;
                    NPC(nameof(DataContextOverride));
                    NPC(nameof(DataContext));
                    InvokeDataContextChanged();
                }
            }
        }

        /// <summary>The source object that data bindings are resolved from.<para/>
        /// This value prioritizes <see cref="DataContextOverride"/>, but falls back on <see cref="MGWindow.WindowDataContext"/> if there is no explicit override.<para/>
        /// To set this value, set <see cref="DataContextOverride"/> or set <see cref="MGWindow.WindowDataContext"/></summary>
        public override object DataContext => DataContextOverride ?? SelfOrParentWindow.WindowDataContext;

        public static readonly ReadOnlyCollection<MGElementType> WindowElementTypes = new List<MGElementType>() { MGElementType.Window, MGElementType.ToolTip, MGElementType.ContextMenu }.AsReadOnly();
        public MGElementType ElementType { get; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool IsWindow => WindowElementTypes.Contains(ElementType);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _Parent;
		public MGElement Parent { get => _Parent; }
		protected internal void SetParent(MGElement Value)
		{
            if (_Parent != Value)
            {
                MGElement Previous = Parent;
                _Parent = Value;
                Interlocked.Increment(ref _TreeTopologyGeneration);
                _LocalResources?.SetParent(GetInheritedResources());
                InvalidateLayoutTree();
                NPC(nameof(Parent));
                OnParentChanged?.Invoke(this, new(Previous, Parent));
            }
        }

		public event EventHandler<EventArgs<MGElement>> OnParentChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _ManagedParent;
        /// <summary>The parent <see cref="MGElement"/> that micromanages this <see cref="MGElement"/>, or null if this <see cref="MGElement"/> is not tightly coupled with its creator.<para/>
        /// For example, an <see cref="MGListView{TItemType}"/> always contains an <see cref="MGGrid"/> to display the column headers, and another <see cref="MGGrid"/> to display the rows<br/>
        /// Those 2 <see cref="MGGrid"/>s are specially-created elements whose <see cref="ManagedParent"/> is the <see cref="MGListView{TItemType}"/></summary>
        public MGElement ManagedParent
        {
            get => _ManagedParent;
            protected internal set
            {
                if (_ManagedParent != value)
                {
                    _ManagedParent = value;
                    NPC(nameof(ManagedParent));
                    NPC(nameof(IsManagedElement));
                }
            }
        }

        /// <summary>True if this <see cref="MGElement"/> is automatically created and managed by its parent,<br/>
        /// such as a ComboBox's <see cref="MGComboBox{TItemType}.Dropdown"/> or a ContextMenu's <see cref="MGContextMenu.ItemsPanel"/></summary>
        public bool IsManagedElement => ManagedParent != null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _ComponentParent;
        /// <summary>The parent <see cref="MGElement"/> that this <see cref="MGComponent{TElementType}"/> belongs to, or null if this <see cref="MGElement"/> is not a <see cref="MGComponent{TElementType}"/>.</summary>
        public MGElement ComponentParent
        {
            get => _ComponentParent;
            private set
            {
                if (_ComponentParent != value)
                {
                    _ComponentParent = value;
                    NPC(nameof(ComponentParent));
                    NPC(nameof(IsComponent));
                }
            }
        }
        /// <summary>True if this <see cref="MGElement"/> is a <see cref="MGComponent{TElementType}"/> of its parent,
        /// such as the <see cref="MGCheckBox.ButtonComponent"/> of an <see cref="MGCheckBox"/>, or the <see cref="MGButton.BorderComponent"/> of an <see cref="MGButton"/></summary>
        public bool IsComponent => ComponentParent != null;

        protected List<MGComponentBase> Components { get; } = new();

        // Pre-computed component lists by category — updated in AddComponent, eliminates LINQ allocations in hot paths.
        private readonly List<MGElement> _componentsDrawBeforeBackground = new();
        private readonly List<MGElement> _componentsDrawBeforeSelf       = new();
        private readonly List<MGElement> _componentsDrawBeforeContents   = new();
        private readonly List<MGElement> _componentsDrawAfterContents    = new();
        private readonly List<MGElement> _componentsUpdateBeforeContents = new();
        private readonly List<MGElement> _componentsUpdateAfterContents  = new();

        private void RebuildComponentCaches()
        {
            _componentsDrawBeforeBackground.Clear();
            _componentsDrawBeforeSelf.Clear();
            _componentsDrawBeforeContents.Clear();
            _componentsDrawAfterContents.Clear();
            _componentsUpdateBeforeContents.Clear();
            _componentsUpdateAfterContents.Clear();
            foreach (MGComponentBase c in Components)
            {
                if (c.DrawBeforeBackground)
                {
                    _componentsDrawBeforeBackground.Add(c.BaseElement);
                }

                if (c.DrawBeforeSelf)
                {
                    _componentsDrawBeforeSelf.Add(c.BaseElement);
                }

                if (c.DrawBeforeContents)
                {
                    _componentsDrawBeforeContents.Add(c.BaseElement);
                }

                if (c.DrawAfterContents)
                {
                    _componentsDrawAfterContents.Add(c.BaseElement);
                }

                if (c.UpdateBeforeContents)
                {
                    _componentsUpdateBeforeContents.Add(c.BaseElement);
                }

                if (c.UpdateAfterContents)
                {
                    _componentsUpdateAfterContents.Add(c.BaseElement);
                }
            }
        }

		protected virtual void AddComponent(MGComponentBase Component)
		{
			Component.BaseElement.SetParent(this);
            Component.BaseElement.ComponentParent = this;
			Components.Add(Component);
            // Update cached category lists
            if (Component.DrawBeforeBackground)
            {
                _componentsDrawBeforeBackground.Add(Component.BaseElement);
            }

            if (Component.DrawBeforeSelf)
            {
                _componentsDrawBeforeSelf.Add(Component.BaseElement);
            }

            if (Component.DrawBeforeContents)
            {
                _componentsDrawBeforeContents.Add(Component.BaseElement);
            }

            if (Component.DrawAfterContents)
            {
                _componentsDrawAfterContents.Add(Component.BaseElement);
            }

            if (Component.UpdateBeforeContents)
            {
                _componentsUpdateBeforeContents.Add(Component.BaseElement);
            }

            if (Component.UpdateAfterContents)
            {
                _componentsUpdateAfterContents.Add(Component.BaseElement);
            }
        }

        protected virtual bool RemoveComponent(MGComponentBase Component)
        {
            if (Component == null || !Components.Remove(Component))
            {
                return false;
            }

            Component.BaseElement.ComponentParent = null;
            Component.BaseElement.SetParent(null);
            _componentsDrawBeforeBackground.Remove(Component.BaseElement);
            _componentsDrawBeforeSelf.Remove(Component.BaseElement);
            _componentsDrawBeforeContents.Remove(Component.BaseElement);
            _componentsDrawAfterContents.Remove(Component.BaseElement);
            _componentsUpdateBeforeContents.Remove(Component.BaseElement);
            _componentsUpdateAfterContents.Remove(Component.BaseElement);
            LayoutChanged(this, true);
            return true;
        }

        protected void EnsureComponentBinding<TElementType>(Func<MGComponent<TElementType>> GetComponent,
            Action<MGComponent<TElementType>> SetComponent, TElementType Element,
            Func<TElementType, MGComponent<TElementType>> CreateComponent)
            where TElementType : MGElement
        {
            MGComponent<TElementType> Component = GetComponent();

            if (Element == null)
            {
                if (Component != null)
                {
                    RemoveComponent(Component);
                    SetComponent(null);
                }
                return;
            }

            if (Component != null && ReferenceEquals(Component.Element, Element))
            {
                return;
            }

            if (Component != null)
            {
                RemoveComponent(Component);
            }

            Component = CreateComponent(Element);
            SetComponent(Component);
            AddComponent(Component);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _Name;
		/// <summary>Optional - can be null. If not null, the <see cref="SelfOrParentWindow"/> will index all child elements by their <see cref="Name"/>, so <see cref="Name"/>s must be unique.</summary>
		public string Name
		{
			get => _Name;
			set
			{
				if (_Name != value)
				{
					string Previous = Name;
					_Name = value;
                    NPC(nameof(Name));
					OnNameChanged?.Invoke(this, new(Previous, Name));
				}
			}
		}

		public event EventHandler<EventArgs<string>> OnNameChanged;

        #region Responsive
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool? _UseResponsiveLayout;
        public bool? UseResponsiveLayout
        {
            get => _UseResponsiveLayout;
            set
            {
                if (_UseResponsiveLayout != value)
                {
                    _UseResponsiveLayout = value;
                    LayoutChanged(this, true);
                    NPC(nameof(UseResponsiveLayout));
                    NPC(nameof(IsResponsiveLayoutEnabled));
                }
            }
        }

        public bool IsResponsiveLayoutEnabled => UseResponsiveLayout ?? Parent?.IsResponsiveLayoutEnabled ?? false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ScaleSpacingWithResponsive = true;
        public bool ScaleSpacingWithResponsive
        {
            get => _ScaleSpacingWithResponsive;
            set
            {
                if (_ScaleSpacingWithResponsive != value)
                {
                    _ScaleSpacingWithResponsive = value;
                    LayoutChanged(this, true);
                    NPC(nameof(ScaleSpacingWithResponsive));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ScaleDimensionsWithResponsive = true;
        public bool ScaleDimensionsWithResponsive
        {
            get => _ScaleDimensionsWithResponsive;
            set
            {
                if (_ScaleDimensionsWithResponsive != value)
                {
                    _ScaleDimensionsWithResponsive = value;
                    LayoutChanged(this, true);
                    NPC(nameof(ScaleDimensionsWithResponsive));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ResponsiveAnchor _ResponsiveAnchor;
        public ResponsiveAnchor ResponsiveAnchor
        {
            get => _ResponsiveAnchor;
            set
            {
                if (_ResponsiveAnchor != value)
                {
                    _ResponsiveAnchor = value;
                    LayoutChanged(this, true);
                    NPC(nameof(ResponsiveAnchor));
                }
            }
        }

        protected float ResponsiveLayoutScaleFactor => IsResponsiveLayoutEnabled && ScaleDimensionsWithResponsive ? GetDesktop().ResponsiveMetrics.UIScaleFactor : 1.0f;
        protected float ResponsiveSpacingScaleFactor => IsResponsiveLayoutEnabled && ScaleSpacingWithResponsive ? GetDesktop().ResponsiveMetrics.UIScaleFactor : 1.0f;

        internal Thickness ResolvedMargin => ScaleSpacingWithResponsive ? UIResponsiveMath.ScaleThickness(_Margin, ResponsiveSpacingScaleFactor) : _Margin;
        internal Thickness ResolvedPadding => ScaleSpacingWithResponsive ? UIResponsiveMath.ScaleThickness(_Padding, ResponsiveSpacingScaleFactor) : _Padding;

        internal int? ResolvedMinWidth => ScaleDimensionsWithResponsive ? UIResponsiveMath.ScaleNullableInt(_MinWidth, ResponsiveLayoutScaleFactor) : _MinWidth;
        internal int? ResolvedMinHeight => ScaleDimensionsWithResponsive ? UIResponsiveMath.ScaleNullableInt(_MinHeight, ResponsiveLayoutScaleFactor) : _MinHeight;
        internal int? ResolvedMaxWidth => ScaleDimensionsWithResponsive ? UIResponsiveMath.ScaleNullableInt(_MaxWidth, ResponsiveLayoutScaleFactor) : _MaxWidth;
        internal int? ResolvedMaxHeight => ScaleDimensionsWithResponsive ? UIResponsiveMath.ScaleNullableInt(_MaxHeight, ResponsiveLayoutScaleFactor) : _MaxHeight;
        internal int? ResolvedPreferredWidth => ScaleDimensionsWithResponsive ? UIResponsiveMath.ScaleNullableInt(_PreferredWidth, ResponsiveLayoutScaleFactor) : _PreferredWidth;
        internal int? ResolvedPreferredHeight => ScaleDimensionsWithResponsive ? UIResponsiveMath.ScaleNullableInt(_PreferredHeight, ResponsiveLayoutScaleFactor) : _PreferredHeight;

        internal Thickness ResolveExternalSpacing(Thickness value)
            => IsResponsiveLayoutEnabled && ScaleSpacingWithResponsive ? UIResponsiveMath.ScaleThickness(value, ResponsiveSpacingScaleFactor) : value;

        /// <summary>Resolves a spacing value owned by this element (such as <c>MGStackPanel.Spacing</c>) through <see cref="ResponsiveSpacingScaleFactor"/>,
        /// using <see cref="UIResponsiveMath.ScaleSpacing(int, float)"/>.<br/>
        /// At scale factor 1.0 (outside a responsive subtree, or when <see cref="ScaleSpacingWithResponsive"/> is false), this is the identity
        /// function since <see cref="UIResponsiveMath.ScaleInt(int, float)"/> is a no-op at scale 1.0.</summary>
        internal int ResolveOwnedSpacing(int designSpacing)
            => UIResponsiveMath.ScaleSpacing(designSpacing, ResponsiveSpacingScaleFactor);

        /// <summary>Resolves a gridline margin owned by this element (such as <c>MGGrid.GridLineMargin</c>) through <see cref="ResponsiveSpacingScaleFactor"/>,
        /// using <see cref="UIResponsiveMath.ScaleGridLineMargin(int, int, int, float)"/>. <paramref name="resolvedSpacing"/> must already be the
        /// resolved (post-scaling) spacing for the same axis, since the returned margin is capped relative to it.<br/>
        /// At scale factor 1.0, this is the identity function: <c>ScaleInt(margin, 1) == margin</c>, and whenever the design-space gutter is
        /// positive, <c>min(margin, (spacing - 1) / 2) == margin</c> because <paramref name="resolvedSpacing"/> equals <paramref name="designSpacing"/>
        /// and the invariant <c>designSpacing - 2 * margin > 0</c> already guarantees <c>margin &lt;= (designSpacing - 1) / 2</c>.</summary>
        internal int ResolveOwnedGridLineMargin(int margin, int designSpacing, int resolvedSpacing)
            => UIResponsiveMath.ScaleGridLineMargin(margin, designSpacing, resolvedSpacing, ResponsiveSpacingScaleFactor);
        #endregion Responsive

        #region Margin / Padding
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thickness _Margin;
		public Thickness Margin
		{
			get => _Margin;
			set
			{
				if (!_Margin.Equals(value))
				{
                    Thickness Previous = Margin;
					_Margin = value;
                    LayoutChanged(this, true);
                    NPC(nameof(Margin));
                    NPC(nameof(HorizontalMargin));
                    NPC(nameof(VerticalMargin));
                    NPC(nameof(MarginSize));
                    NPC(nameof(HorizontalMarginAndPadding));
                    NPC(nameof(VerticalMarginAndPadding));
                    NPC(nameof(MarginAndPaddingSize));
                    NPC(nameof(MinSizeIncludingMargin));
                    NPC(nameof(MaxSizeIncludingMargin));
                    NPC(nameof(ActualPreferredWidth));
                    NPC(nameof(ActualPreferredHeight));
                    OnMarginChanged?.Invoke(this, new(Previous, Margin));
                }
			}
		}

        public event EventHandler<EventArgs<Thickness>> OnMarginChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thickness _Padding;
		public Thickness Padding
		{
			get => _Padding;
			set
			{
				if (!_Padding.Equals(value))
				{
					_Padding = value;
                    LayoutChanged(this, true);
                    NPC(nameof(Padding));
                    NPC(nameof(HorizontalPadding));
                    NPC(nameof(VerticalPadding));
                    NPC(nameof(PaddingSize));
                    NPC(nameof(HorizontalMarginAndPadding));
                    NPC(nameof(VerticalMarginAndPadding));
                    NPC(nameof(MarginAndPaddingSize));
                }
			}
		}

        /// <summary>Total width of <see cref="Margin"/> (<see cref="Thickness.Left"/> + <see cref="Thickness.Right"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int HorizontalMargin => ResolvedMargin.Width;
        /// <summary>Total height of <see cref="Margin"/> (<see cref="Thickness.Top"/> + <see cref="Thickness.Bottom"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int VerticalMargin => ResolvedMargin.Height;
        /// <summary>Total size of <see cref="Margin"/><para/>
        /// Width = <see cref="Thickness.Left"/> + <see cref="Thickness.Right"/>;<br/>
        /// Height = <see cref="Thickness.Top"/> + <see cref="Thickness.Bottom"/>;</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Size MarginSize => ResolvedMargin.Size;

        /// <summary>Total width of <see cref="Padding"/> (<see cref="Thickness.Left"/> + <see cref="Thickness.Right"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int HorizontalPadding => ResolvedPadding.Width;
        /// <summary>Total height of <see cref="Padding"/> (<see cref="Thickness.Top"/> + <see cref="Thickness.Bottom"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int VerticalPadding => ResolvedPadding.Height;
        /// <summary>Total size of <see cref="Padding"/><para/>
        /// Width = <see cref="Thickness.Left"/> + <see cref="Thickness.Right"/>;<br/>
        /// Height = <see cref="Thickness.Top"/> + <see cref="Thickness.Bottom"/>;</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Size PaddingSize => ResolvedPadding.Size;

        /// <summary>Total width of <see cref="Margin"/> + <see cref="Padding"/> (<see cref="Thickness.Left"/> + <see cref="Thickness.Right"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int HorizontalMarginAndPadding => HorizontalMargin + HorizontalPadding;
        /// <summary>Total height of <see cref="Margin"/> + <see cref="Padding"/> (<see cref="Thickness.Top"/> + <see cref="Thickness.Bottom"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int VerticalMarginAndPadding => VerticalMargin + VerticalPadding;
        /// <summary>Total size of <see cref="Margin"/> + <see cref="Padding"/><para/>
        /// Width = <see cref="Thickness.Left"/> + <see cref="Thickness.Right"/>;<br/>
        /// Height = <see cref="Thickness.Top"/> + <see cref="Thickness.Bottom"/>;</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Size MarginAndPaddingSize => new(HorizontalMarginAndPadding, VerticalMarginAndPadding);
        #endregion Margin / Padding

        #region Alignment
        public static Rectangle ApplyAlignment(Rectangle Bounds, HorizontalAlignment HA, VerticalAlignment VA, Size Size)
        {
            int StartX = HA switch
            {
                HorizontalAlignment.Left => Bounds.Left,
                HorizontalAlignment.Center => Bounds.Left + (Bounds.Width - Size.Width) / 2,
                HorizontalAlignment.Right => Bounds.Right - Size.Width,
                HorizontalAlignment.Stretch => Bounds.Left,
                _ => throw new NotImplementedException($"Unrecognized {nameof(HorizontalAlignment)}: {HA}"),
            };

            int StartY = VA switch
            {
                VerticalAlignment.Top => Bounds.Top,
                VerticalAlignment.Center => Bounds.Top + (Bounds.Height - Size.Height) / 2,
                VerticalAlignment.Bottom => Bounds.Bottom - Size.Height,
                VerticalAlignment.Stretch => Bounds.Top,
                _ => throw new NotImplementedException($"Unrecognized {nameof(VerticalAlignment)}: {VA}"),
            };

            return new(StartX, StartY, HA == HorizontalAlignment.Stretch ? Bounds.Width : Size.Width, VA == VerticalAlignment.Stretch ? Bounds.Height : Size.Height);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private HorizontalAlignment _HorizontalAlignment;
		public HorizontalAlignment HorizontalAlignment
		{
			get => _HorizontalAlignment;
			set
			{
				if (_HorizontalAlignment != value)
				{
					HorizontalAlignment Previous = HorizontalAlignment;
					_HorizontalAlignment = value;
                    NPC(nameof(HorizontalAlignment));
					OnHorizontalAlignmentChanged?.Invoke(this, new(Previous, HorizontalAlignment));
                    LayoutChanged(this, true);
                }
			}
		}

		protected event EventHandler<EventArgs<HorizontalAlignment>> OnHorizontalAlignmentChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private VerticalAlignment _VerticalAlignment;
		public VerticalAlignment VerticalAlignment
		{
			get => _VerticalAlignment;
			set
			{
				if (_VerticalAlignment != value)
				{
					VerticalAlignment Previous = VerticalAlignment;
                    _VerticalAlignment = value;
                    NPC(nameof(VerticalAlignment));
                    OnVerticalAlignmentChanged?.Invoke(this, new(Previous, VerticalAlignment));
                    LayoutChanged(this, true);
                }
			}
		}

        protected event EventHandler<EventArgs<VerticalAlignment>> OnVerticalAlignmentChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private HorizontalAlignment _HorizontalContentAlignment;
		public HorizontalAlignment HorizontalContentAlignment
		{
			get => _HorizontalContentAlignment;
			set
			{
				if (_HorizontalContentAlignment != value)
				{
					_HorizontalContentAlignment = value;
                    LayoutChanged(this, true);
                    NPC(nameof(HorizontalContentAlignment));
                }
			}
		}

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private VerticalAlignment _VerticalContentAlignment;
		public VerticalAlignment VerticalContentAlignment
		{
			get => _VerticalContentAlignment;
			set
			{
				if (_VerticalContentAlignment != value)
				{
					_VerticalContentAlignment = value;
                    LayoutChanged(this, true);
                    NPC(nameof(VerticalContentAlignment));
                }
			}
		}
        #endregion Alignment

        #region Size
        #region Min / Max Size
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _MinWidth;
        /// <summary>The minimum width of this <see cref="MGElement"/>, not including <see cref="HorizontalMargin"/>, or 0 if null.</summary>
		public int? MinWidth
		{
			get => _MinWidth;
			set
			{
				if (_MinWidth != value)
				{
					_MinWidth = value;
                    LayoutChanged(this, true);
                    NPC(nameof(MinWidth));
                    NPC(nameof(MinSize));
                    NPC(nameof(MinSizeIncludingMargin));
                }
			}
		}

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _MinHeight;
        /// <summary>The minimum height of this <see cref="MGElement"/>, not including <see cref="VerticalMargin"/>, or 0 if null.</summary>
        public int? MinHeight
        {
            get => _MinHeight;
            set
            {
                if (_MinHeight != value)
                {
                    _MinHeight = value;
                    LayoutChanged(this, true);
                    NPC(nameof(MinHeight));
                    NPC(nameof(MinSize));
                    NPC(nameof(MinSizeIncludingMargin));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _MaxWidth;
        /// <summary>The maximum width of this <see cref="MGElement"/>, not including <see cref="HorizontalMargin"/>, or <see cref="int.MaxValue"/> if null.</summary>
        public int? MaxWidth
        {
            get => _MaxWidth;
            set
            {
                if (_MaxWidth != value)
                {
                    _MaxWidth = value;
                    LayoutChanged(this, true);
                    NPC(nameof(MaxWidth));
                    NPC(nameof(MaxSizeIncludingMargin));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _MaxHeight;
        /// <summary>The maximum height of this <see cref="MGElement"/>, not including <see cref="VerticalMargin"/>, or <see cref="int.MaxValue"/> if null.</summary>
		public int? MaxHeight
		{
			get => _MaxHeight;
			set
			{
				if (_MaxHeight != value)
				{
					_MaxHeight = value;
                    LayoutChanged(this, true);
                    NPC(nameof(MaxHeight));
                    NPC(nameof(MaxSizeIncludingMargin));
                }
			}
		}

        /// <summary>Combination of <see cref="MinWidth"/> and <see cref="MinHeight"/>. Uses 0 if value is not specified.<para/>
        /// This value does not include <see cref="Margin"/>.<para/>
        /// See also: <see cref="MinSizeIncludingMargin"/>, <see cref="MaxSize"/>, <see cref="MaxSizeIncludingMargin"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Size MinSize => new(ResolvedMinWidth ?? 0, ResolvedMinHeight ?? 0);
        /// <summary>Combination of <see cref="MinWidth"/> and <see cref="MinHeight"/>. Uses 0 if value is not specified.<para/>
        /// This value includes <see cref="Margin"/>.<para/>
        /// See also: <see cref="MinSize"/>, <see cref="MaxSize"/>, <see cref="MaxSizeIncludingMargin"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Size MinSizeIncludingMargin => new(ResolvedMinWidth.HasValue ? ResolvedMinWidth.Value + HorizontalMargin : 0, ResolvedMinHeight.HasValue ? ResolvedMinHeight.Value + VerticalMargin : 0);

        /// <summary>Combination of <see cref="MinWidth"/> and <see cref="MinHeight"/>. Uses <see cref="int.MaxValue"/> if value is not specified.<para/>
        /// This value does not include <see cref="Margin"/>.<para/>
        /// See also: <see cref="MaxSizeIncludingMargin"/>, <see cref="MinSize"/>, <see cref="MinSizeIncludingMargin"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Size MaxSize => new(ResolvedMaxWidth ?? int.MaxValue, ResolvedMaxHeight ?? int.MaxValue);

        /// <summary>Combination of <see cref="MinWidth"/> and <see cref="MinHeight"/>. Uses <see cref="int.MaxValue"/> if value is not specified.<para/>
        /// This value includes <see cref="Margin"/>.<para/>
        /// See also: <see cref="MaxSize"/>, <see cref="MinSize"/>, <see cref="MinSizeIncludingMargin"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Size MaxSizeIncludingMargin => new(ResolvedMaxWidth.HasValue ? ResolvedMaxWidth.Value + HorizontalMargin : int.MaxValue, ResolvedMaxHeight.HasValue ? ResolvedMaxHeight.Value + VerticalMargin : int.MaxValue);
        #endregion Min / Max Size

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _PreferredWidth;
		/// <summary>The requested layout width of this <see cref="MGElement"/>, not including <see cref="HorizontalMargin"/>.<br/>
		/// For the value that includes <see cref="HorizontalMargin"/>, use <see cref="ActualPreferredWidth"/>.<para/>
		/// Not the same as the rendered width if the parent <see cref="MGElement"/> did not have enough available space to allocate to this <see cref="MGElement"/> or if there is a non-zero <see cref="HorizontalMargin"/>.<para/>
		/// See also: <see cref="ActualWidth"/>, <see cref="ActualPreferredWidth"/></summary>
		public int? PreferredWidth
		{
			get => _PreferredWidth;
			set
			{
				if (_PreferredWidth != value)
				{
					if (value.HasValue && value.Value < 0)
                    {
                        throw new ArgumentOutOfRangeException($"{nameof(MGElement)}.{nameof(PreferredWidth)} cannot be negative.");
                    }

                    _PreferredWidth = value;
                    LayoutChanged(this, true);
                    NPC(nameof(PreferredWidth));
                    NPC(nameof(ActualPreferredWidth));
                }
			}
		}

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _PreferredHeight;
        /// <summary>The requested layout height of this <see cref="MGElement"/>, not including <see cref="VerticalMargin"/>.<br/>
        /// For the value that includes <see cref="VerticalMargin"/>, use <see cref="ActualPreferredHeight"/>.<para/>
        /// Not the same as the rendered height if the parent <see cref="MGElement"/> did not have enough available space to allocate to this <see cref="MGElement"/> or if there is a non-zero <see cref="VerticalMargin"/>.<para/>
        /// See also: <see cref="ActualHeight"/>, <see cref="ActualPreferredHeight"/></summary>
        public int? PreferredHeight
		{
			get => _PreferredHeight;
			set
			{
				if (_PreferredHeight != value)
				{
                    if (value.HasValue && value.Value < 0)
                    {
                        throw new ArgumentOutOfRangeException($"{nameof(MGElement)}.{nameof(PreferredHeight)} cannot be negative.");
                    }

                    _PreferredHeight = value;
                    LayoutChanged(this, true);
                    NPC(nameof(PreferredHeight));
                    NPC(nameof(ActualPreferredHeight));
                }
			}
		}

        /// <summary>Same as <see cref="PreferredWidth"/>, except this includes the <see cref="HorizontalMargin"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int? ActualPreferredWidth => ResolvedPreferredWidth.HasValue ? Math.Max(0, ResolvedPreferredWidth.Value + HorizontalMargin) : ResolvedPreferredWidth;
        /// <summary>Same as <see cref="PreferredHeight"/>, except this includes the <see cref="VerticalMargin"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int? ActualPreferredHeight => ResolvedPreferredHeight.HasValue ? Math.Max(0, ResolvedPreferredHeight.Value + VerticalMargin) : ResolvedPreferredHeight;
        #endregion Size

        #region ToolTip
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGToolTip _ToolTip;
		public MGToolTip ToolTip
		{
			get => _ToolTip;
			set
			{
				if (_ToolTip != value)
				{
					MGToolTip Previous = ToolTip;
					_ToolTip = value;
                    NPC(nameof(ToolTip));
					ToolTipChanged?.Invoke(this, new(Previous, ToolTip));
				}
			}
		}

		public event EventHandler<EventArgs<MGToolTip>> ToolTipChanged;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsToolTipShowing => GetDesktop().ActiveToolTip == ToolTip;
        #endregion ToolTip

        #region ContextMenu
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGContextMenu _ContextMenu;
		public MGContextMenu ContextMenu
		{
			get => _ContextMenu;
			set
			{
				if (_ContextMenu != value)
				{
					MGContextMenu Previous = ContextMenu;
					_ContextMenu = value;
                    SyncContextMenuRmbHandler();
                    NPC(nameof(ContextMenu));
					ContextMenuChanged?.Invoke(this, new(Previous, ContextMenu));
				}
			}
		}

        /// <summary>Tracks whether <see cref="TryOpenContextMenuOnRightClick"/> is currently
        /// subscribed to <see cref="MouseHandler"/>.<see cref="MGUI.Shared.Input.Mouse.MouseHandler.RMBReleasedInside"/>.</summary>
        private bool _rmbHandlerSubscribed;

        /// <summary>Subscribes or unsubscribes <see cref="TryOpenContextMenuOnRightClick"/> from
        /// <see cref="MouseHandler"/>.<see cref="MGUI.Shared.Input.Mouse.MouseHandler.RMBReleasedInside"/>
        /// based on whether a <see cref="ContextMenu"/> is assigned or <see cref="ContextMenuRequested"/> has subscribers.</summary>
        private void SyncContextMenuRmbHandler()
        {
            bool needsHandler = _ContextMenu != null || _contextMenuRequested != null;
            if (needsHandler && !_rmbHandlerSubscribed)
            {
                MouseHandler.RMBReleasedInside += TryOpenContextMenuOnRightClick;
                _rmbHandlerSubscribed = true;
            }
            else if (!needsHandler && _rmbHandlerSubscribed)
            {
                MouseHandler.RMBReleasedInside -= TryOpenContextMenuOnRightClick;
                _rmbHandlerSubscribed = false;
            }
        }

        private void TryOpenContextMenuOnRightClick(object sender, BaseMouseReleasedEventArgs e)
        {
            // Build args with the element's static ContextMenu as default.
            var args = new ContextMenuRequestedEventArgs(_ContextMenu, e.Position);

            // Let subscribers override or cancel.
            _contextMenuRequested?.Invoke(this, args);

            if (args.Handled)
            {
                return;
            }

            MGContextMenu menu = args.Menu;
            if (menu != null && menu.TryOpenContextMenu(e.Position))
            {
                e.SetHandledBy(menu, false);
            }
        }

		/// <summary>Invoked when <see cref="ContextMenu"/> is set to a new value. (Not invoked when the content within <see cref="ContextMenu"/> is modified)<para/>
		/// See also: <see cref="MGDesktop.ActiveContextMenu"/>, <see cref="MGDesktop.ContextMenuClosing"/>, <see cref="MGDesktop.ContextMenuClosed"/>, <see cref="MGDesktop.ContextMenuOpening"/>, <see cref="MGDesktop.ContextMenuOpened"/></summary>
        public event EventHandler<EventArgs<MGContextMenu>> ContextMenuChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private EventHandler<ContextMenuRequestedEventArgs> _contextMenuRequested;
        /// <summary>Fired when the user right-clicks this element, just before the context menu is opened.
        /// Allows building or replacing the menu dynamically without assigning <see cref="ContextMenu"/>.
        /// <para/><b>Usage:</b> set <see cref="ContextMenuRequestedEventArgs.Menu"/> to a fresh
        /// <see cref="MGContextMenu"/> instance, or set <see cref="ContextMenuRequestedEventArgs.Handled"/>
        /// to <see langword="true"/> to suppress the menu entirely.
        /// <para/>Subscribing to this event automatically wires up the right-click handler, so you do
        /// <em>not</em> need to also set <see cref="ContextMenu"/> if you only use this event.
        /// <para/>Example:
        /// <code>
        /// element.ContextMenuRequested += (s, e) =>
        /// {
        ///     var m = new MGContextMenu(window, "");
        ///     m.AddButton("Option A", _ => DoA());
        ///     e.Menu = m;
        /// };
        /// </code></summary>
        public event EventHandler<ContextMenuRequestedEventArgs> ContextMenuRequested
        {
            add
            {
                _contextMenuRequested += value;
                SyncContextMenuRmbHandler();
            }
            remove
            {
                _contextMenuRequested -= value;
                SyncContextMenuRmbHandler();
            }
        }
        #endregion ContextMenu

        #region Input
        protected InputTracker InputTracker => GetDesktop().InputTracker;

        protected MouseHandler _MouseHandler;
        public MouseHandler MouseHandler => _MouseHandler ??= InputTracker.Mouse.CreateHandler(this, null);

        protected KeyboardHandler _KeyboardHandler;
        /// <summary>Warning - the events within this handler might never be invoked if <see cref="IKeyboardHandlerHost.CanReceiveKeyboardInput"/> or <see cref="CanHandleKeyboardInput"/> are false.<para/>
        /// See also: <see cref="MGDesktop.FocusedKeyboardHandler"/></summary>
        public KeyboardHandler KeyboardHandler => _KeyboardHandler ??= InputTracker.Keyboard.CreateHandler(this, null);

        /// <summary>True if <see cref="MouseButton.Left"/> was pressed overtop of this <see cref="MGElement"/> and has not been released yet.<para/>
        /// This property can be true even if the mouse isn't overtop of this <see cref="MGElement"/> (if pressed overtop, but then moved outside and not yet released)<para/>
        /// You may want to consider checking for <see cref="VisualState"/>'s <see cref="SecondaryVisualState.Pressed"/> instead.<para/>
        /// See also: <see cref="MGWindow.PressedElement"/> (You might also want to call <see cref="IsSelfOrAncestorOf(MGElement)"/> on the <see cref="MGWindow.PressedElement"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsLMBPressed => InputTracker.Mouse.IsPressedInside(MouseButton.Left, this);

        /// <summary>True if <see cref="MouseButton.Right"/> was pressed overtop of this <see cref="MGElement"/> and has not been released yet.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsRMBPressed => InputTracker.Mouse.IsPressedInside(MouseButton.Right, this);

        /// <summary>True if <see cref="MouseButton.Middle"/> was pressed overtop of this <see cref="MGElement"/> and has not been released yet.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsMMBPressed => InputTracker.Mouse.IsPressedInside(MouseButton.Middle, this);

        /// <summary>Z-order-aware with respect to windows: false when the mouse position is covered by a window drawn above this element's window
        /// (nested/modal windows, higher desktop windows, the active context menu). Warning - it stays geometric within the window: it can be true
        /// even if this <see cref="MGElement"/> is covered by a sibling <see cref="MGElement"/> drawn overtop of it in the same window.<para/>
        /// You may want to consider checking for <see cref="VisualState"/>'s <see cref="SecondaryVisualState.Hovered"/> instead.<para/>
        /// See also: <see cref="MGWindow.HoveredElement"/> (You might also want to call <see cref="IsSelfOrAncestorOf(MGElement)"/> on the <see cref="MGWindow.HoveredElement"/>)</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsHovered => AsIMouseHandlerHost().IsInside(InputTracker.Mouse.CurrentPosition.ToVector2());

        protected internal virtual bool ContainsUnscaledInputPoint(Vector2 unscaledScreenPosition)
            => ActualLayoutBounds.ContainsInclusive(unscaledScreenPosition);

        protected IMouseViewport AsIViewport() => this;
        protected IMouseHandlerHost AsIMouseHandlerHost() => this;
        bool IMouseViewport.IsInside(Vector2 Position)
        {
            Vector2 UnscaledPosition = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.UnscaledScreen, Position);
            //  Z-order-aware hit-test: a position covered by a window drawn over the window that displays this element (its own
            //  nested/modal windows, sibling nested windows above it, higher desktop windows, the active context menu) is not "inside"
            //  this element, so the Inside/Outside classification of every mouse event - movement, Entered/Exited, hover, press -
            //  follows the visible surface rather than raw geometry. The test is position-dependent so Entered/Exited fire at the
            //  occluder's edge. Drag continuation is unaffected: MouseHandler delivers Dragged/DragEnd to the drag owner regardless of IsInside.
            //  Occlusion is resolved from DisplayingWindow (the window that actually shows this element), not SelfOrParentWindow (the window
            //  it was constructed with), so an element re-parented into another window is not occluded by its own construction window
            //  anymore - see Docs/decisions/0004-hit-test-occlusion-from-displaying-window.md.
            return ContainsUnscaledInputPoint(UnscaledPosition)
                && GetDesktop().ValidScreenBounds.ContainsInclusive(Position)
                && !(DisplayingWindow?.IsUnscaledPositionOccluded(UnscaledPosition) ?? false);
        }

        Vector2 IMouseViewport.GetOffset() => Vector2.Zero;

		protected bool _CanReceiveMouseInput { get; private set; }
		bool IMouseHandlerHost.CanReceiveMouseInput() => _CanReceiveMouseInput;
        IMouseHandlerHost IMouseHandlerHost.GetMouseInputParent() => Parent;

        /// <summary>If true, this element can receive keyboard focus when clicked,
        /// without necessarily being a text input control.<br/>
        /// Controls like <see cref="MGTreeView"/>, <see cref="MGListBox{TItemType}"/>,
        /// and <see cref="MGListView{TItemType}"/> set this to <see langword="true"/> to enable keyboard navigation.<para/>
        /// Setting this to <see langword="true"/> also makes <see cref="CanHandleKeyboardInput"/> return <see langword="true"/>.<para/>
        /// When first set to <see langword="true"/>, a one-time subscription to <see cref="MouseHandler"/>
        /// <c>.LMBPressedInside</c> is made so that clicking this element will automatically call <see cref="Focus"/>.</summary>
        public virtual bool IsFocusable
        {
            get => _isFocusable;
            set
            {
                if (_isFocusable != value)
                {
                    _isFocusable = value;
                    if (value && !_autoFocusSubscribed)
                    {
                        _autoFocusSubscribed = true;
                        MouseHandler.LMBPressedInside += (sender, e) =>
                        {
                            if (IsFocusable)
                            {
                                Focus(KeyboardFocusSource.Pointer);
                            }
                        };
                    }
                }
            }
        }
        private bool _isFocusable;
        private bool _autoFocusSubscribed;

        /// <summary>Returns <see langword="true"/> if this element can process keyboard input.<br/>
        /// By default returns <see cref="IsFocusable"/>.<br/>
        /// Controls that are always keyboard-active (like text boxes) override this to return <see langword="true"/>.</summary>
        public virtual bool CanHandleKeyboardInput { get => IsFocusable; }
        protected bool _CanReceiveKeyboardInput { get; private set; }
		bool IKeyboardHandlerHost.CanReceiveKeyboardInput() => CanHandleKeyboardInput && _CanReceiveKeyboardInput;

        /// <summary>Requests keyboard focus for this element.
        /// Focus will be applied at the end of the current update tick.</summary>
        public void Focus(KeyboardFocusSource source = KeyboardFocusSource.Programmatic)
        {
            if (CanHandleKeyboardInput)
            {
                GetDesktop().QueueFocusedKeyboardHandler(this, source);
            }
        }

        #region Drag and Drop
        /// <summary>When true, this element accepts dropped items during a drag-and-drop operation.
        /// Setting this will subscribe to <see cref="MouseHandler.MovedInside"/> and <see cref="MouseHandler.Exited"/>
        /// in order to drive <see cref="DragEnter"/>, <see cref="DragOver"/>, and <see cref="DragLeave"/> events.</summary>
        public bool AllowDrop
        {
            get => _AllowDrop;
            set
            {
                if (_AllowDrop != value)
                {
                    _AllowDrop = value;
                    RefreshDragDropSubscriptions();
                }
            }
        }
        private bool _AllowDrop;
        private bool _dragDropSubscribed;

        private void RefreshDragDropSubscriptions()
        {
            if (_AllowDrop && !_dragDropSubscribed)
            {
                MouseHandler.MovedInside += OnDragMovedInside;
                MouseHandler.Exited      += OnDragExited;
                _dragDropSubscribed = true;
            }
            else if (!_AllowDrop && _dragDropSubscribed)
            {
                MouseHandler.MovedInside -= OnDragMovedInside;
                MouseHandler.Exited      -= OnDragExited;
                _dragDropSubscribed = false;
            }
        }

        private void OnDragMovedInside(object sender, MGUI.Shared.Input.Mouse.BaseMouseMovedEventArgs e)
        {
            MGDesktop desktop = GetDesktop();
            if (desktop?.DragDropManager?.IsDragging == true)
            {
                desktop.DragDropManager.NotifyDragOver(this, e.CurrentPosition);
            }
        }

        private void OnDragExited(object sender, MGUI.Shared.Input.Mouse.BaseMouseMovedEventArgs e)
        {
            MGDesktop desktop = GetDesktop();
            if (desktop?.DragDropManager?.IsDragging == true)
            {
                desktop.DragDropManager.NotifyDragLeave(this, e.CurrentPosition);
            }
        }

        public event EventHandler<DragEnterEventArgs> DragEnter;
        public event EventHandler<DragOverEventArgs>  DragOver;
        public event EventHandler<DragLeaveEventArgs> DragLeave;
        public event EventHandler<DropEventArgs>      Drop;

        internal void RaiseDragEnter(DragEnterEventArgs e)  => DragEnter?.Invoke(this, e);
        internal void RaiseDragOver(DragOverEventArgs e)    => DragOver?.Invoke(this, e);
        internal void RaiseDragLeave(DragLeaveEventArgs e)  => DragLeave?.Invoke(this, e);
        internal void RaiseDrop(DropEventArgs e)            => Drop?.Invoke(this, e);

        /// <summary>Initiates a drag-and-drop operation from this element.
        /// Call this from a <see cref="MouseHandler.LMBPressedInside"/> or similar handler.</summary>
        public void DoDragDrop(DragDropData data) => GetDesktop()?.DragDropManager?.DoDragDrop(this, data);
        #endregion Drag and Drop

        // ---- Dirty flags for targeted input-state recomputation (Task 14) ---------
        // Set to true when IsEnabled, IsHitTestVisible, Visibility, or RecentDrawWasClipped changes
        // so the next Update() call knows it must recompute cached input eligibility.
        private bool _inputStateDirty = true;
        // Cached values from the last recomputation so we can detect unchanged frames.
        private bool _cachedComputedEnabled;
        private bool _cachedComputedHTVisible;
        private bool _cachedParentCanReceiveMouse = true;
        private bool _cachedParentCanReceiveKeyboard = true;
        private bool _cachedHasModalWindow;
        // ---- End dirty-flag fields ------------------------------------------------

        private bool CanInheritMouseInputFromParent()
            => this is MGWindow { IsModalWindow: true } || (Parent?._CanReceiveMouseInput ?? true);

		bool IKeyboardHandlerHost.HasKeyboardFocus() => GetDesktop().FocusedKeyboardHandler == this;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _CanHandleInputsWhileHidden;
        public bool CanHandleInputsWhileHidden
        {
            get => _CanHandleInputsWhileHidden;
            internal protected set
            {
                if (_CanHandleInputsWhileHidden != value)
                {
                    _CanHandleInputsWhileHidden = value;
                    NPC(nameof(CanHandleInputsWhileHidden));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsHitTestVisible;
        /// <summary>True if this <see cref="MGElement"/> should be allowed to detect and handle mouse and keyboard inputs.<para/>
        /// This property does not account for the parent's <see cref="IsHitTestVisible"/>. Consider using <see cref="DerivedIsHitTestVisible"/> instead.</summary>
        public bool IsHitTestVisible
        {
            get => _IsHitTestVisible && (Visibility == Visibility.Visible || (CanHandleInputsWhileHidden && Visibility == Visibility.Hidden));
            set
            {
                if (IsHitTestVisible != value)
                {
                    _IsHitTestVisible = value;
                    _inputStateDirty = true;
                    NPC(nameof(IsHitTestVisible));
                }
            }
        }
        /// <summary>True if this <see cref="MGElement"/> should be allowed to detect and handle mouse and keyboard inputs.<para/>
        /// This property is only true if both this <see cref="MGElement"/> and every parent along the visual tree have <see cref="IsHitTestVisible"/>==true.</summary>
        public bool DerivedIsHitTestVisible => IsHitTestVisible && (Parent?.IsHitTestVisible ?? IsHitTestVisible);

        /// <summary>If true, <see cref="IsLMBPressed"/> will be evaluated as true while drawing this element's background, regardless of the real MouseState.</summary>
        internal protected bool SpoofIsPressedWhileDrawingBackground { get; set; } = false;
        /// <summary>If true, <see cref="IsHovered"/> will be evaluated as true while drawing this element's background, regardless of the real MouseState.</summary>
        internal protected bool SpoofIsHoveredWhileDrawingBackground { get; set; } = false;
        #endregion Input

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private VisualState _VisualState = new(PrimaryVisualState.Normal, SecondaryVisualState.None);
        public VisualState VisualState
        {
            get => _VisualState;
            private set
            {
                if (_VisualState != value)
                {
                    VisualState Previous = VisualState;
                    _VisualState = value;
                    NPC(nameof(VisualState));
                    VisualStateChanged?.Invoke(this, new(Previous, VisualState));
                }
            }
        }

        /// <summary>Invoked when <see cref="VisualState"/> changes.</summary>
        public event EventHandler<EventArgs<VisualState>> VisualStateChanged;

        public virtual bool TryHandleNavigationAction(UINavigationAction action) => false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _TabIndex;
        public int TabIndex
        {
            get => _TabIndex;
            set
            {
                if (_TabIndex != value)
                {
                    _TabIndex = value;
                    NPC(nameof(TabIndex));
                }
            }
        }

        public Dictionary<NavigationDirection, MGElement> NavigationNeighbors { get; } = new();

        internal static PrimaryVisualState ResolvePrimaryVisualState(bool isEnabled, bool isSelected, bool hasKeyboardFocus, bool shouldDisplayFocusedState)
        {
            if (!isEnabled)
            {
                return PrimaryVisualState.Disabled;
            }

            if (isSelected)
            {
                return PrimaryVisualState.Selected;
            }

            if (hasKeyboardFocus && shouldDisplayFocusedState)
            {
                return PrimaryVisualState.Focused;
            }

            return PrimaryVisualState.Normal;
        }

        internal static SecondaryVisualState ResolveSecondaryVisualState(bool isHitTestVisible, bool hasModalWindow,
            bool isLmbPressed, bool isPressedElementOrAncestor, bool isHovered, bool isHoveredElementOrAncestor,
            bool hasKeyboardFocus, bool shouldDisplayFocusedState)
        {
            if (!isHitTestVisible || hasModalWindow)
            {
                return SecondaryVisualState.None;
            }

            if (isLmbPressed && isPressedElementOrAncestor)
            {
                return SecondaryVisualState.Pressed;
            }

            bool shouldSuppressHover = hasKeyboardFocus && shouldDisplayFocusedState;
            if (!shouldSuppressHover && isHovered && isHoveredElementOrAncestor)
            {
                return SecondaryVisualState.Hovered;
            }

            return SecondaryVisualState.None;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsSelected;
        /// <summary>This property does not account for the parent's <see cref="IsSelected"/>. Consider using <see cref="DerivedIsSelected"/> instead.</summary>
        public bool IsSelected
        {
            get => _IsSelected;
            set
            {
                if (_IsSelected != value)
                {
                    _IsSelected = value;
                    InvalidateLayoutTree();
                    LayoutChanged(this, true);
                    NPC(nameof(IsSelected));
                }
            }
        }
        /// <summary>This property is only false if both this <see cref="MGElement"/> and every parent along the visual tree have <see cref="IsSelected"/>==false.</summary>
        public bool DerivedIsSelected => IsSelected || (Parent?.DerivedIsSelected ?? IsSelected);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsEnabled;
        /// <summary>This property does not account for the parent's <see cref="IsEnabled"/>. Consider using <see cref="DerivedIsEnabled"/> instead.</summary>
        public bool IsEnabled
        {
            get => _IsEnabled;
            set
            {
                if (_IsEnabled != value)
                {
                    _IsEnabled = value;
                    _inputStateDirty = true;
                    NPC(nameof(IsEnabled));
                }
            }
        }
        /// <summary>This property is only true if both this <see cref="MGElement"/> and every parent along the visual tree have <see cref="IsEnabled"/>==true.</summary>
        public bool DerivedIsEnabled => IsEnabled && (Parent?.DerivedIsEnabled ?? IsEnabled);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Thickness _BackgroundRenderPadding;
        /// <summary>A padding to use when drawing the <see cref="BackgroundBrush"/>.<br/>Default = (0,0,0,0).<para/>
        /// A negative value allows the background to span a larger rectangular region, such as if the element's border contained some transparent pixels that you would want to fill with the <see cref="BackgroundBrush"/>.<para/>
        /// Warning - negative padding does NOT increase this element's layout bounds.<br/>
        /// If using a large enough negative padding, some of the background may be rendered outside of this element's bounds, and thus might be clipped if <see cref="ClipToBounds"/> is true.</summary>
        public Thickness BackgroundRenderPadding
        {
            get => _BackgroundRenderPadding;
            set
            {
                if (!BackgroundRenderPadding.Equals(value))
                {
                    _BackgroundRenderPadding = value;
                    NPC(nameof(BackgroundRenderPadding));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private VisualStateFillBrush _BackgroundBrush;
        /// <summary>The <see cref="IFillBrush"/>es to use for this <see cref="MGElement"/>'s background, depending on the current <see cref="VisualState"/>.<para/>
        /// See also: <see cref="OverlayBrush"/></summary>
        public VisualStateFillBrush BackgroundBrush
        {
            get => _BackgroundBrush;
            set
            {
                if (_BackgroundBrush != value)
                {
                    _BackgroundBrush = value;
                    NPC(nameof(BackgroundBrush));
                    NPC(nameof(BackgroundUnderlay));
                    NPC(nameof(BackgroundOverlay));
                }
            }
        }
        /// <summary>The first <see cref="IFillBrush"/> used to draw this <see cref="MGElement"/>'s background. Drawn before <see cref="BackgroundOverlay"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IFillBrush BackgroundUnderlay => BackgroundBrush.GetUnderlay(VisualState.Primary);
        /// <summary>The second <see cref="IFillBrush"/> used to draw this <see cref="MGElement"/>'s background. Drawn after <see cref="BackgroundUnderlay"/></summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IFillBrush BackgroundOverlay => BackgroundBrush.GetFillOverlay(VisualState.Secondary);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private VisualStateSetting<Color?> _DefaultTextForeground;
        /// <summary>The default foreground color to use when rendering text on this <see cref="MGElement"/> or any of its child elements.<br/>
        /// For a value that accounts for the parent's <see cref="DefaultTextForeground"/>, use <see cref="DerivedDefaultTextForeground"/> instead.<para/>
        /// See also: <see cref="DerivedDefaultTextForeground"/>, <see cref="CurrentDefaultTextForeground"/></summary>
        public VisualStateSetting<Color?> DefaultTextForeground
        {
            get => _DefaultTextForeground;
            set
            {
                if (_DefaultTextForeground != value)
                {
                    _DefaultTextForeground = value;
                    NPC(nameof(DefaultTextForeground));
                    NPC(nameof(CurrentDefaultTextForeground));
                }
            }
        }
        /// <summary>The currently-active value from <see cref="DefaultTextForeground"/>, based on <see cref="VisualState"/></summary>
        public Color? CurrentDefaultTextForeground => DefaultTextForeground.GetValue(VisualState.Primary) ?? DefaultTextForeground.NormalValue;
        /// <summary>This property prioritizes <see cref="CurrentDefaultTextForeground"/> if it has a value.<br/>
        /// Else traverses up the visual tree until finding the first non-null <see cref="CurrentDefaultTextForeground"/>.</summary>
        public Color? DerivedDefaultTextForeground => CurrentDefaultTextForeground ?? Parent?.DerivedDefaultTextForeground;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IFillBrush _OverlayBrush;
        /// <summary>A brush that is drawn overtop of this element.<br/>
        /// This brush is drawn to the same bounds as the <see cref="BackgroundBrush"/>.
        /// I.E. It does not span the border thickness if this element has a border, and it accounts for <see cref="Margin"/> and <see cref="BackgroundRenderPadding"/>)<para/>
        /// See also: <see cref="BackgroundBrush"/></summary>
        public IFillBrush OverlayBrush
        {
            get => _OverlayBrush;
            set
            {
                if (_OverlayBrush != value)
                {
                    _OverlayBrush = value;
                    NPC(nameof(OverlayBrush));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Visibility _Visibility;
        public Visibility Visibility
        {
            get => _Visibility;
            set
            {
                if (_Visibility != value)
                {
                    Visibility Previous = Visibility;
                    _Visibility = value;
                    _inputStateDirty = true;
                    if (Previous == Visibility.Collapsed || Visibility == Visibility.Collapsed)
                    {
                        LayoutChanged(this, true);
                    }

                    NPC(nameof(Visibility));
                    NPC(nameof(IsVisibilityCollapsed));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsVisibilityCollapsed => Visibility == Visibility.Collapsed;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private float _Opacity;
        /// <summary>This property does not account for the parent's <see cref="Opacity"/>.</summary>
        public float Opacity
        {
            get => _Opacity;
            set
            {
                if (_Opacity != value)
                {
                    _Opacity = value;
                    NPC(nameof(Opacity));
                }
            }
        }

        /// <summary>General-purpose dictionary to attach your own data to this <see cref="MGElement"/></summary>
        public Dictionary<string, object> Metadata { get; } = new();
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private object _Tag;
        /// <summary>General-purpose object to attach your own data to this <see cref="MGElement"/></summary>
        public object Tag
        {
            get => _Tag;
            set
            {
                if (_Tag != value)
                {
                    _Tag = value;
                    NPC(nameof(Tag));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DeferEventsManager InitializationManager { get; }
		/// <summary>To improve performance, consider invoking this method during constructor initialization.<para/>
		/// This will defer layout updates until the <see cref="DeferEventsTransaction"/> is disposed.</summary>
		protected internal DeferEventsTransaction BeginInitializing() => InitializationManager.DeferEvents();

        #region Delayed Actions
        public enum InvokeLaterPriority
        {
            /// <summary>See also: <see cref="MGElement.OnBeginUpdate"/></summary>
            OnBeginUpdate,
            /// <summary>See also: <see cref="MGElement.OnBeginUpdateContents"/></summary>
            OnBeginUpdateContents,
            /// <summary>See also: <see cref="MGElement.OnEndUpdateContents"/></summary>
            OnEndUpdateContents,
            /// <summary>See also: <see cref="MGElement.OnEndUpdate"/></summary>
            OnEndUpdate
        }

        /// <summary>Invokes the given <paramref name="Action"/> after a certain number of update ticks given by <paramref name="FrameDelay"/>.</summary>
        /// <param name="Action"></param>
        /// <param name="FrameDelay">How many update ticks to wait before executing the <paramref name="Action"/>.<br/>
        /// Must be > 0. If 1, the <paramref name="Action"/> will be executed during the next update.</param>
        /// <param name="Priority">Determines what part of the update tick the action should be invoked during</param>
        public void InvokeLater(Action Action, int FrameDelay, InvokeLaterPriority Priority)
        {
            if (Action == null)
            {
                throw new ArgumentNullException(nameof(Action));
            }

            if (FrameDelay <= 0)
            {
                throw new ArgumentException($"{nameof(FrameDelay)} must be > 0");
            }

            switch (Priority)
            {
                case InvokeLaterPriority.OnBeginUpdate:
                    {
                        int RemainingFrames = FrameDelay;
                        void Handler(object sender, ElementUpdateEventArgs args)
                        {
                            RemainingFrames--;
                            if (RemainingFrames <= 0)
                            {
                                try { Action(); }
                                finally { OnBeginUpdate -= Handler; }
                            }
                        }
                        OnBeginUpdate += Handler;
                    }
                    break;
                case InvokeLaterPriority.OnBeginUpdateContents:
                    {
                        int RemainingFrames = FrameDelay;
                        void Handler(object sender, ElementUpdateEventArgs args)
                        {
                            RemainingFrames--;
                            if (RemainingFrames <= 0)
                            {
                                try { Action(); }
                                finally { OnBeginUpdateContents -= Handler; }
                            }
                        }
                        OnBeginUpdateContents += Handler;
                    }
                    break;
                case InvokeLaterPriority.OnEndUpdateContents:
                    {
                        int RemainingFrames = FrameDelay;
                        void Handler(object sender, ElementUpdateEventArgs args)
                        {
                            RemainingFrames--;
                            if (RemainingFrames <= 0)
                            {
                                try { Action(); }
                                finally { OnEndUpdateContents -= Handler; }
                            }
                        }
                        OnEndUpdateContents += Handler;
                    }
                    break;
                case InvokeLaterPriority.OnEndUpdate:
                    {
                        int RemainingFrames = FrameDelay;
                        void Handler(object sender, ElementUpdateEventArgs args)
                        {
                            RemainingFrames--;
                            if (RemainingFrames <= 0)
                            {
                                try { Action(); }
                                finally { OnEndUpdate -= Handler; }
                            }
                        }
                        OnEndUpdate += Handler;
                    }
                    break;
            }
        }
        #endregion Delayed Actions

        protected MGElement(MGWindow ParentWindow, MGElementType ElementType)
			: this(ParentWindow.Desktop, ParentWindow, ElementType)
		{

		}

        protected MGElement(MGDesktop Desktop, MGWindow ParentWindow, MGElementType ElementType, MGTheme Theme = null)
		{
			InitializationManager = new(() => { LayoutChanged(this, true); });
			using (BeginInitializing())
			{
				UniqueId = Guid.NewGuid().ToString();
                this.ParentWindow = ParentWindow;
                this.ElementType = ElementType;

                SelfOrParentWindow.WindowDataContextChanged += (sender, e) =>
                {
                    if (DataContextOverride == null)
                    {
                        NPC(nameof(DataContext));
                        InvokeDataContextChanged();
                    }
                };

                MGTheme ActualTheme = Theme ?? ParentWindow?.Theme ?? Desktop.Theme;

                Margin = new(0);
				Padding = new(0);

				HorizontalAlignment = HorizontalAlignment.Stretch;
				VerticalAlignment = VerticalAlignment.Stretch;
				HorizontalContentAlignment = HorizontalAlignment.Stretch;
				VerticalContentAlignment = VerticalAlignment.Stretch;

                BackgroundRenderPadding = new(0);
                BackgroundBrush = ActualTheme.GetBackgroundBrush(ElementType);
                DefaultTextForeground = new VisualStateSetting<Color?>(null, null, null);

                Visibility = Visibility.Visible;
                IsEnabled = true;
                IsSelected = false;
				IsHitTestVisible = true;
                ClipToBounds = true;
                Opacity = 1.0f;

                SelfOrParentWindow.OnWindowPositionChanged += (sender, e) =>
				{
					Point Offset = new(e.NewValue.Left - e.PreviousValue.Left, e.NewValue.Top - e.PreviousValue.Top);
                    TranslateAllBounds(Offset);
				};
            }
		}

		public override string ToString() => string.IsNullOrEmpty(Name) ?
            $"{ElementType}: {GetType().Name} ({RenderBounds})" :
            $"'{Name}' - {ElementType}: {GetType().Name} ({RenderBounds})";

		public virtual IEnumerable<MGElement> GetChildren() => Enumerable.Empty<MGElement>();

        public virtual MGBorder GetBorder() => null;
        public bool HasBorder => GetBorder() != null;

        /// <summary>Retrieves all <see cref="IBorderBrush"/>es that this <see cref="MGElement"/> uses, 
        /// excluding any brushes associated with its built-in <see cref="MGBorder"/> (See: <see cref="HasBorder"/>, <see cref="GetBorder"/>)<br/>
        /// This method does not recursively return brushes nested within brushes, such as the <see cref="MGBandedBorderBrush.Bands"/> of an <see cref="MGBandedBorderBrush"/><para/>
        /// May contain <see langword="null"/> entries.</summary>
        protected virtual IEnumerable<IBorderBrush> GetBorderBrushes()
        {
            if (HasBorder)
            {
                yield return GetBorder().BorderBrush;
            }
        }

        /// <summary>Retrieves the raw <see cref="IFillBrush"/> slots that this <see cref="MGElement"/> draws itself, excluding <see cref="BackgroundBrush"/>
        /// (see <see cref="GetVisualStateFillBrushes"/>). By default only <see cref="OverlayBrush"/>.<para/>
        /// Each brush returned here receives <see cref="IFillBrush.Update(UpdateBaseArgs)"/> once per frame from <see cref="Update(ElementUpdateArgs)"/>,
        /// so that stateful paints keep animating. Controls that own additional <see cref="IFillBrush"/> slots override this method (and call the base implementation).<para/>
        /// Do not return a brush that belongs to a child element's <see cref="BackgroundBrush"/>/<see cref="OverlayBrush"/>, nor a "template" brush that is assigned
        /// into other elements: those are already ticked by their consumer, and a brush ticked twice per frame animates twice as fast.<br/>
        /// This method does not recursively return brushes nested within brushes.<para/>
        /// May contain <see langword="null"/> entries.</summary>
        protected virtual IEnumerable<IFillBrush> GetFillBrushes()
        {
            yield return OverlayBrush;
        }

        /// <summary>Retrieves the <see cref="VisualStateFillBrush"/> slots that this <see cref="MGElement"/> draws itself. By default only <see cref="BackgroundBrush"/>.<para/>
        /// Each wrapper returned here receives <see cref="VisualStateFillBrush.Update(UpdateBaseArgs)"/> once per frame from <see cref="Update(ElementUpdateArgs)"/>.
        /// Controls that own additional <see cref="VisualStateFillBrush"/> slots override this method (and call the base implementation).
        /// The same rules as <see cref="GetFillBrushes"/> apply: never return a slot that proxies a child element's background, nor a template brush consumed by other elements.<para/>
        /// May contain <see langword="null"/> entries.</summary>
        protected virtual IEnumerable<VisualStateFillBrush> GetVisualStateFillBrushes()
        {
            yield return BackgroundBrush;
        }

        /// <summary>Removes all <see cref="DataBinding"/>s that are associated with this <see cref="MGElement"/>.<para/>
        /// Recommended to invoke this method if you are about to remove this <see cref="MGElement"/> from the visual tree,<br/>
        /// so that all data bindings can unsubscribe from events such as <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.</summary>
        /// <param name="IncludeChildren">If true, will also recursively remove bindings for all child items.</param>
        /// <returns>The number of <see cref="DataBinding"/>s that were deleted.</returns>
        public int RemoveDataBindings(bool IncludeChildren)
        {
            if (!IncludeChildren)
            {
                return DataBindingManager.RemoveBindings(this);
            }
            else
            {
                return TraverseVisualTree().Sum(x => DataBindingManager.RemoveBindings(x));
            }
        }

        #region Bounds
        protected internal Matrix GetTransform(CoordinateSpace From, CoordinateSpace To)
        {
            if (From == To)
            {
                return Matrix.Identity;
            }
            else
            {
                return From switch
                {
                    CoordinateSpace.Layout => To switch
                    {
                        CoordinateSpace.Layout => Matrix.Identity,
                        CoordinateSpace.UnscaledScreen => Matrix.CreateTranslation(-Origin.X, -Origin.Y, 0),
                        CoordinateSpace.Screen => Matrix.CreateTranslation(-Origin.X, -Origin.Y, 0) * SelfOrParentWindow.UnscaledScreenSpaceToScaledScreenSpace,
                        _ => throw new NotImplementedException($"Unrecognized {nameof(CoordinateSpace)}: {To}")
                    },
                    CoordinateSpace.UnscaledScreen => To switch
                    {
                        CoordinateSpace.Layout => Matrix.CreateTranslation(Origin.X, Origin.Y, 0),
                        CoordinateSpace.UnscaledScreen => Matrix.Identity,
                        CoordinateSpace.Screen => SelfOrParentWindow.UnscaledScreenSpaceToScaledScreenSpace,
                        _ => throw new NotImplementedException($"Unrecognized {nameof(CoordinateSpace)}: {To}")
                    },
                    CoordinateSpace.Screen => To switch
                    {
                        CoordinateSpace.Layout => SelfOrParentWindow.ScaledScreenSpaceToUnscaledScreenSpace * Matrix.CreateTranslation(Origin.X, Origin.Y, 0),
                        CoordinateSpace.UnscaledScreen => SelfOrParentWindow.ScaledScreenSpaceToUnscaledScreenSpace,
                        CoordinateSpace.Screen => Matrix.Identity,
                        _ => throw new NotImplementedException($"Unrecognized {nameof(CoordinateSpace)}: {To}")
                    },
                    _ => throw new NotImplementedException($"Unrecognized {nameof(CoordinateSpace)}: {From}")
                };
            }
        }

        public Rectangle ConvertCoordinateSpace(CoordinateSpace From, CoordinateSpace To, Rectangle Value)
        {
            if (From == To)
            {
                return Value;
            }
            else
            {
                Matrix Transform = GetTransform(From, To);
                return Value.CreateTransformedF(Transform).RoundUp();
            }
        }

        public Vector2 ConvertCoordinateSpace(CoordinateSpace From, CoordinateSpace To, Vector2 Value)
        {
            if (From == To)
            {
                return Value;
            }
            else
            {
                Matrix Transform = GetTransform(From, To);
                return Value.TransformBy(Transform);
            }
        }

        public Point ConvertCoordinateSpace(CoordinateSpace From, CoordinateSpace To, Point Value) => ConvertCoordinateSpace(From, To, Value.ToVector2()).ToPoint();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ClipToBounds;
        public virtual bool ClipToBounds
        {
            get => _ClipToBounds;
            set
            {
                if (_ClipToBounds != value)
                {
                    _ClipToBounds = value;
                    NPC(nameof(ClipToBounds));
                }
            }
        }

        public bool DrawBackgroundEnabled { get; set; } = true;
        public bool DrawOverlayBrushEnabled { get; set; } = true;
        public bool DrawBackgroundBorderOverlayEnabled { get; set; } = true;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Point _Origin;
        /// <summary>This value is typically <see cref="Point.Zero"/> except when this <see cref="MGElement"/> is a child of an <see cref="MGScrollViewer"/>,<br/>
        /// in which case the origin would be based on the <see cref="MGScrollViewer.HorizontalOffset"/> / <see cref="MGScrollViewer.VerticalOffset"/></summary>
        public Point Origin
        {
            get => _Origin;
            private set
            {
                if (_Origin != value)
                {
                    _Origin = value;
                    NPC(nameof(Origin));
                }
            }
        }

        protected void TranslateAllBounds(Point Offset)
        {
            if (Offset != Point.Zero)
            {
                Rectangle PreviousLayoutBounds = LayoutBounds;
                AllocatedBounds = AllocatedBounds.GetTranslated(Offset);
                RenderBounds = RenderBounds.GetTranslated(Offset);
                LayoutBounds = LayoutBounds.GetTranslated(Offset);
                StretchedContentBounds = StretchedContentBounds.GetTranslated(Offset);
                AlignedContentBounds = AlignedContentBounds.GetTranslated(Offset);
                OnLayoutBoundsChanged?.Invoke(this, new(PreviousLayoutBounds, LayoutBounds));
            }
        }


        private Rectangle _ActualLayoutBounds;
        /// <summary>The screen space that this element is rendered to.<para/>
        /// Unlike <see cref="LayoutBounds"/>, this value always uses an origin of <see cref="Point.Zero"/>, rather than being relative to <see cref="Origin"/>,<br/>
        /// and also accounts for <see cref="ClipToBounds"/> by intersecting the bounds with the parent's <see cref="ActualLayoutBounds"/>.<para/>
        /// This value will show Width=0/Height=0 for elements that are outside the visible viewport, even if they've technically been allocated non-zero dimensions.<para/>
        /// See also: <see cref="LayoutBounds"/>, <see cref="Origin"/></summary>
        public Rectangle ActualLayoutBounds
        {
            get => _ActualLayoutBounds;
            private set
            {
                if (_ActualLayoutBounds != value)
                {
                    Rectangle Previous = ActualLayoutBounds;
                    _ActualLayoutBounds = value;
                    NPC(nameof(ActualLayoutBounds));
                    OnActualLayoutBoundsChanged?.Invoke(this, new(Previous, ActualLayoutBounds));
                }
            }
        }

        /// <summary>Invoked when <see cref="ActualLayoutBounds"/> changes.</summary>
        public event EventHandler<EventArgs<Rectangle>> OnActualLayoutBoundsChanged;

        /// <summary>The screen space that was allocated to this <see cref="MGElement"/>.<br/>
        /// This <see cref="MGElement"/> may choose not to use all of the allocated space based on <see cref="HorizontalAlignment"/> and <see cref="VerticalAlignment"/>.<para/>
        /// This value does not account for <see cref="Origin"/> and thus might not match the exact screen bounds where this <see cref="MGElement"/> was drawn.<para/>
        /// See also: <br/><see cref="AllocatedBounds"/><br/><see cref="RenderBounds"/><br/><see cref="LayoutBounds"/><br/><see cref="StretchedContentBounds"/><br/>
        /// <see cref="AlignedContentBounds"/><br/><see cref="ActualLayoutBounds"/></summary>
        public Rectangle AllocatedBounds { get; private set; }

        /// <summary>The screen space that this <see cref="MGElement"/> will render itself to.<br/>
        /// This value accounts for <see cref="HorizontalAlignment"/> and <see cref="VerticalAlignment"/>.<para/>
		/// This value does not account for <see cref="Origin"/> and thus might not match the exact screen bounds where this <see cref="MGElement"/> was drawn.<para/>
        /// See also: <br/><see cref="AllocatedBounds"/><br/><see cref="RenderBounds"/><br/><see cref="LayoutBounds"/><br/><see cref="StretchedContentBounds"/><br/>
        /// <see cref="AlignedContentBounds"/><br/><see cref="ActualLayoutBounds"/></summary>
        public Rectangle RenderBounds { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Rectangle _LayoutBounds;
        /// <summary>The screen space that this <see cref="MGElement"/> will render itself to, after accounting for <see cref="Margin"/>.<br/>
        /// The <see cref="BackgroundBrush"/> of this <see cref="MGElement"/> spans these bounds.<para/>
		/// This value does not account for <see cref="Origin"/> and thus might not match the exact screen bounds where this <see cref="MGElement"/> was drawn.<para/>
        /// See also: <br/><see cref="AllocatedBounds"/><br/><see cref="RenderBounds"/><br/><see cref="LayoutBounds"/><br/><see cref="StretchedContentBounds"/><br/>
        /// <see cref="AlignedContentBounds"/><br/><see cref="ActualLayoutBounds"/></summary>
        public Rectangle LayoutBounds
        {
            get => _LayoutBounds;
            private set
            {
                if (_LayoutBounds != value)
                {
                    _LayoutBounds = value;
                    NPC(nameof(LayoutBounds));
                    NPC(nameof(ActualWidth));
                    NPC(nameof(ActualHeight));
                }
            }
        }

        /// <summary>The screen space that this <see cref="MGElement"/>'s contents will render to, before accounting for <see cref="HorizontalContentAlignment"/> and <see cref="VerticalContentAlignment"/>.<br/>
        /// This value accounts for <see cref="Margin"/>, <see cref="Padding"/>, and any other properties that affect this <see cref="MGElement"/>'s size (such as BorderThickness of a <see cref="MGBorder"/>).<para/>
		/// This value does not account for <see cref="Origin"/> and thus might not match the exact screen bounds where this <see cref="MGElement"/> was drawn.<para/>
        /// See also: <br/><see cref="AllocatedBounds"/><br/><see cref="RenderBounds"/><br/><see cref="LayoutBounds"/><br/><see cref="StretchedContentBounds"/><br/>
        /// <see cref="AlignedContentBounds"/><br/><see cref="ActualLayoutBounds"/></summary>
        public Rectangle StretchedContentBounds { get; private set; }

        /// <summary>The screen space that this <see cref="MGElement"/>'s contents will render to, after accounting for <see cref="HorizontalContentAlignment"/> and <see cref="VerticalContentAlignment"/>.<br/>
        /// This value accounts for <see cref="Margin"/>, <see cref="Padding"/>, and any other properties that affect this <see cref="MGElement"/>'s size (such as BorderThickness of a <see cref="MGBorder"/>).<para/>
		/// This value does not account for <see cref="Origin"/> and thus might not match the exact screen bounds where this <see cref="MGElement"/> was drawn.<para/>
        /// See also: <br/><see cref="AllocatedBounds"/><br/><see cref="RenderBounds"/><br/><see cref="LayoutBounds"/><br/><see cref="StretchedContentBounds"/><br/>
        /// <see cref="AlignedContentBounds"/><br/><see cref="ActualLayoutBounds"/></summary>
        public Rectangle AlignedContentBounds { get; private set; }

        /// <summary>Note: This value does not include <see cref="Margin"/>. For a value with <see cref="Margin"/>, consider using <see cref="RenderBounds"/>.Width</summary>
        public int ActualWidth => LayoutBounds.Width;
        /// <summary>Note: This value does not include <see cref="Margin"/>. For a value with <see cref="Margin"/>, consider using <see cref="RenderBounds"/>.Height</summary>
        public int ActualHeight => LayoutBounds.Height;
        #endregion Bounds

        #region Update
        [DebuggerStepThrough]
        public class ElementUpdateEventArgs : EventArgs
        {
			public readonly MGElement Element;
            public readonly ElementUpdateArgs UA;

            public ElementUpdateEventArgs(MGElement Element, ElementUpdateArgs UA)
            {
				this.Element = Element;
                this.UA = UA;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime? HoverStartTime = null;

        protected internal MGElement GetTopmostHoveredElement(ElementUpdateArgs UA)
        {
            MGElement Result = null;
            // Convert mouse position to unscaled screen space once, then pass down the tree
            Vector2 unscaledMousePos = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.UnscaledScreen,
                InputTracker.Mouse.CurrentPosition.ToVector2());
            ComputeTopmostHoveredElement(UA.IsEnabled, UA.IsHitTestVisible, true, unscaledMousePos, ref Result);
            return Result;
        }

        private void ComputeTopmostHoveredElement(bool IsParentEnabled, bool IsParentHitTestVisible, bool CanParentReceiveMouseInput, Vector2 unscaledMousePos, ref MGElement Result)
        {
            bool canReceiveMouseInputWhileHidden = Visibility == Visibility.Hidden && CanHandleInputsWhileHidden;

            if (Visibility != Visibility.Visible && !canReceiveMouseInputWhileHidden)
            {
                return;
            }

            // Skip elements that were clipped / not rendered
            if (RecentDrawWasClipped && !canReceiveMouseInputWhileHidden)
            {
                return;
            }

            bool ComputedIsEnabled = IsParentEnabled && IsEnabled;
            bool ComputedIsHitTestVisible = IsParentHitTestVisible && IsHitTestVisible;

            bool BaseCanReceiveInput = (Visibility == Visibility.Visible || (Visibility == Visibility.Hidden && CanHandleInputsWhileHidden)) && ComputedIsEnabled && ComputedIsHitTestVisible
                && (!RecentDrawWasClipped || (Visibility == Visibility.Hidden && CanHandleInputsWhileHidden));
            bool CanReceiveMouseInput = BaseCanReceiveInput && CanParentReceiveMouseInput;

            // Components can overflow parent bounds — always recurse into them
            foreach (MGElement Component in _componentsDrawBeforeBackground)
            {
                Component.ComputeTopmostHoveredElement(ComputedIsEnabled, ComputedIsHitTestVisible, CanReceiveMouseInput, unscaledMousePos, ref Result);
            }

            foreach (MGElement Component in _componentsDrawBeforeSelf)
            {
                Component.ComputeTopmostHoveredElement(ComputedIsEnabled, ComputedIsHitTestVisible, CanReceiveMouseInput, unscaledMousePos, ref Result);
            }

            // Early-out: if mouse is outside this element's bounds, skip self-hover and visual children
            // (visual tree children are always clipped to the parent's content area)
            bool mouseInBounds = ContainsUnscaledInputPoint(unscaledMousePos);

            if (mouseInBounds && CanReceiveMouseInput && ComputedIsHitTestVisible && !DisplayingWindow.HasModalWindow && IsHovered)
            {
                Result = this;
            }

            foreach (MGElement Component in _componentsDrawBeforeContents)
            {
                Component.ComputeTopmostHoveredElement(ComputedIsEnabled, ComputedIsHitTestVisible, CanReceiveMouseInput, unscaledMousePos, ref Result);
            }

            if (mouseInBounds)
            {
                // Use indexed for loop to avoid IReadOnlyList enumerator allocation (Task 16)
                IReadOnlyList<MGElement> vtc = GetVisualTreeChildren(false, true);
                for (int vtcIdx = 0; vtcIdx < vtc.Count; vtcIdx++)
                {
                    vtc[vtcIdx].ComputeTopmostHoveredElement(ComputedIsEnabled, ComputedIsHitTestVisible, CanReceiveMouseInput, unscaledMousePos, ref Result);
                }
            }

            foreach (MGElement Component in _componentsDrawAfterContents)
            {
                Component.ComputeTopmostHoveredElement(ComputedIsEnabled, ComputedIsHitTestVisible, CanReceiveMouseInput, unscaledMousePos, ref Result);
            }
        }

        public void Update(ElementUpdateArgs UA)
		{
            using var performanceScope = UIPerformanceProbe.BeginElementUpdate(this);
            bool ComputedIsEnabled = UA.IsEnabled && IsEnabled;
            bool ComputedIsSelected = UA.IsSelected || IsSelected;
            bool ComputedIsHitTestVisible = UA.IsHitTestVisible && IsHitTestVisible;

            Origin = UA.Offset;

            Rectangle UnscaledScreenBounds = ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.UnscaledScreen, LayoutBounds);
            //FIX (Task 5): ActualLayoutBounds is computed by intersecting the received UA.ActualLayoutBounds
            //(which IS the parent's content-area bounds — see below) with this element's own unscaled screen bounds.
            //This correctly clips each element to the visible content area of its parent.
            //Note: UA.ActualLayoutBounds passed into this call is always the parent's content-area bounds (parent's
            //ActualLayoutBounds shrunk by the parent's Padding), computed just before calling UpdateContents below.
            //Components (border panels, title bars, etc.) receive the parent's full ActualLayoutBounds (not shrunk)
            //because components typically live outside or spanning the padding area.
            //An MGTabControl's HeadersPanel is a component, so it correctly receives the full bounds.
            if (IsWindow)
            {
                ActualLayoutBounds = UnscaledScreenBounds;
            }
            else
            {
                ActualLayoutBounds = Rectangle.Intersect(UA.ActualLayoutBounds, UnscaledScreenBounds);
            }

            // Fast path: element is fully clipped (off-viewport). Skip all expensive processing.
            // Exception: elements that can receive input while hidden still need a minimal update.
            if (!IsWindow && ActualLayoutBounds.Width == 0 && ActualLayoutBounds.Height == 0
                && !(Visibility == Visibility.Hidden && CanHandleInputsWhileHidden))
            {
                _CanReceiveMouseInput = false;
                _CanReceiveKeyboardInput = false;
                _inputStateDirty = true;  // ensure recompute when the element re-enters viewport
                HoverStartTime = null;
                VisualState = new(UI.PrimaryVisualState.Normal, UI.SecondaryVisualState.None);
                return;
            }

            // Fast path: element is fully clipped (off-viewport). Skip all expensive processing.
            // Exception: elements that can receive input while hidden still need a minimal update.
            if (!IsWindow && ActualLayoutBounds.Width == 0 && ActualLayoutBounds.Height == 0
                && !(Visibility == Visibility.Hidden && CanHandleInputsWhileHidden))
            {
                _CanReceiveMouseInput = false;
                _CanReceiveKeyboardInput = false;
                _inputStateDirty = true;  // ensure recompute when the element re-enters viewport
                HoverStartTime = null;
                VisualState = new(PrimaryVisualState.Normal, SecondaryVisualState.None);
                return;
            }

            UA = UA with {
                IsEnabled = ComputedIsEnabled, 
                IsSelected = ComputedIsSelected, 
                IsHitTestVisible = ComputedIsHitTestVisible, 
                ActualLayoutBounds = ActualLayoutBounds
            };
            //UA = new(UA.BA, ComputedIsEnabled, ComputedIsSelected, ComputedIsHitTestVisible, UA.Offset, this.ActualLayoutBounds);

            MGDesktop desktop = GetDesktop();
            bool hasKeyboardFocus = desktop?.FocusedKeyboardHandler == this;
            bool shouldDisplayFocusedState = desktop?.ShouldDisplayFocusedState == true;
            PrimaryVisualState newPVS = ResolvePrimaryVisualState(ComputedIsEnabled, ComputedIsSelected, hasKeyboardFocus, shouldDisplayFocusedState);
            //  Resolved from DisplayingWindow (the window that actually displays this element and therefore assigns its HoveredElement/PressedElement),
            //  not SelfOrParentWindow (the window it was constructed with) - see Docs/decisions/0004-hit-test-occlusion-from-displaying-window.md.
            MGWindow displayingWindow = DisplayingWindow;
            SecondaryVisualState newSVS = ResolveSecondaryVisualState(
                ComputedIsHitTestVisible,
                displayingWindow.HasModalWindow,
                IsLMBPressed,
                IsSelfOrAncestorOf(displayingWindow.PressedElement),
                IsHovered,
                IsSelfOrAncestorOf(displayingWindow.HoveredElement),
                hasKeyboardFocus,
                shouldDisplayFocusedState);
            VisualState = new(newPVS, newSVS);

            ElementUpdateEventArgs UpdateEventArgs = new(this, UA);

            OnBeginUpdate?.Invoke(this, UpdateEventArgs);

            // Fix (Task 14): iterate directly instead of .ToList().ForEach() which allocates a temporary List<>
            foreach (IBorderBrush brush in GetBorderBrushes())
            {
                PaintLifecycle.Update(brush, UA.BA);
            }

            //  Fill brush lifecycle: tick the background wrappers and the raw fill slots this element draws itself (see GetFillBrushes/GetVisualStateFillBrushes).
            //  A stateful paint is ticked once per frame regardless of how many slots or elements reference it: PaintLifecycle
            //  dedups by reference against UA.BA.PaintRegistry (see Docs/drawing-architecture.md, Limites connues).
            foreach (VisualStateFillBrush brush in GetVisualStateFillBrushes())
            {
                brush?.Update(UA.BA);
            }

            foreach (IFillBrush brush in GetFillBrushes())
            {
                PaintLifecycle.Update(brush, UA.BA);
            }

            if (ComputedIsHitTestVisible && Visibility == Visibility.Visible &&
                (newSVS == SecondaryVisualState.Hovered || (IsHovered && newSVS == SecondaryVisualState.Pressed)))
            {
                HoverStartTime ??= DateTime.Now;
            }
            else
            {
                HoverStartTime = null;
            }

            if (!RecentDrawWasClipped && HoverStartTime.HasValue && !SelfOrParentWindow.HasModalWindow)
			{
                if (!TryGetToolTip(out MGToolTip ToolTip))
                {
                    ToolTip = this.ToolTip;
                }

                if (ToolTip != null && (ComputedIsEnabled || ToolTip.ShowOnDisabled))
                {
                    TimeSpan HoveredTime = DateTime.Now.Subtract(HoverStartTime.Value);
                    if (HoveredTime >= ToolTip.ActualShowDelay)
                    {
                        GetDesktop().QueuedToolTip = ToolTip;
                    }
                }
			}

            // Optimisation (Task 14): skip input eligibility recomputation when nothing that affects it changed.
            // Mouse hit testing is clipped by the last draw result, but keyboard focus must remain stable for a
            // focused text editor even when part of its visual subtree was clipped.
            bool parentCanMouse = CanInheritMouseInputFromParent();
            bool parentCanKeyboard = Parent?._CanReceiveKeyboardInput ?? true;
            bool hasModalWindow = SelfOrParentWindow?.HasModalWindow == true;
            if (_inputStateDirty ||
                ComputedIsEnabled != _cachedComputedEnabled ||
                ComputedIsHitTestVisible != _cachedComputedHTVisible ||
                parentCanMouse != _cachedParentCanReceiveMouse ||
                parentCanKeyboard != _cachedParentCanReceiveKeyboard ||
                hasModalWindow != _cachedHasModalWindow)
            {
                _inputStateDirty = false;
                bool baseCanReceiveKeyboardInput = (Visibility == Visibility.Visible || (Visibility == Visibility.Hidden && CanHandleInputsWhileHidden)) && ComputedIsEnabled && ComputedIsHitTestVisible;
                bool BaseCanReceiveMouseInput = baseCanReceiveKeyboardInput
                    && (!RecentDrawWasClipped || (Visibility == Visibility.Hidden && CanHandleInputsWhileHidden));
                _CanReceiveMouseInput    = BaseCanReceiveMouseInput && parentCanMouse && !hasModalWindow;
                _CanReceiveKeyboardInput = baseCanReceiveKeyboardInput && parentCanKeyboard;
                _cachedComputedEnabled       = ComputedIsEnabled;
                _cachedComputedHTVisible     = ComputedIsHitTestVisible;
                _cachedParentCanReceiveMouse = parentCanMouse;
                _cachedParentCanReceiveKeyboard = parentCanKeyboard;
                _cachedHasModalWindow        = hasModalWindow;
            }

            OnBeginUpdateContents?.Invoke(this, UpdateEventArgs);

            // Compute the content-area bounds: ActualLayoutBounds shrunk by this element's Padding.
            // Content children (visual-tree children) are clipped to the content area so their
            // ActualLayoutBounds correctly excludes the padding region of their parent.
            // Components live outside or spanning the padding area, so they use the full bounds.
            // There is no special-case needed for MGTabControl: its HeadersPanel IS a component,
            // so it always receives the full (unpadded) bounds.
            // Clamp origin so ContentAreaBounds never exceeds ActualLayoutBounds
            // even when Padding is larger than the available Width/Height.
            int _cabX = Math.Min(ActualLayoutBounds.X + Padding.Left,  ActualLayoutBounds.Right);
            int _cabY = Math.Min(ActualLayoutBounds.Y + Padding.Top,   ActualLayoutBounds.Bottom);
            Rectangle ContentAreaBounds = new Rectangle(
                _cabX, _cabY,
                Math.Max(0, ActualLayoutBounds.Width  - Padding.Left - Padding.Right),
                Math.Max(0, ActualLayoutBounds.Height - Padding.Top  - Padding.Bottom));
            ElementUpdateArgs UAForContents = UA with { ActualLayoutBounds = ContentAreaBounds };

			foreach (MGElement Component in _componentsUpdateBeforeContents)
            {
                Component.Update(UA);               // components get the full (unpadded) bounds
            }

            UpdateContents(UAForContents);          // content children get the content-area bounds
            foreach (MGElement Component in _componentsUpdateAfterContents)
            {
                Component.Update(UA);              // components get the full (unpadded) bounds
            }

            OnEndUpdateContents?.Invoke(this, UpdateEventArgs);

            if (ComputedIsHitTestVisible)
            {
                //TODO: Need to improve performance of this method
                //  Maybe MouseHandler.HasSubscribedEvents should be cached and updated whenever an event if subscribed/unsubscribed
                //  This would allow MouseHandler.InvokeQueuedEvents() to return immediately in most cases after a trivial boolean check
                //  Some more testing shows that having 3k elements to draw and update seems fine on my computer if there's only about 500 input handlers to update
                //Maybe can also do something about ListBox/ContextMenu/ComboBox
                //      to consolidate their input handling into a single MouseHandler instead of separate ones for each list item
                bool shouldUpdateMouseHandler = true;
                if (_MouseHandler != null && SelfOrParentWindow?.GetActiveMouseDragCaptureOwner() is MGElement activeMouseDragCaptureOwner)
                {
                    // While a control owns the active mouse drag capture, unrelated siblings should not
                    // keep processing hover/move events on every frame.
                    shouldUpdateMouseHandler = activeMouseDragCaptureOwner.IsSelfOrAncestorOf(this)
                        || IsSelfOrAncestorOf(activeMouseDragCaptureOwner);
                }

                if (shouldUpdateMouseHandler && _MouseHandler != null)
                {
                    _MouseHandler.ManualUpdate();
                }

                _KeyboardHandler?.ManualUpdate();
			}
            UpdateSelf(UA);

            OnEndUpdate?.Invoke(this, UpdateEventArgs);
        }

        protected virtual bool TryGetToolTip(out MGToolTip ToolTip)
        {
            ToolTip = null;
            return false;
        }
		protected virtual void UpdateContents(ElementUpdateArgs UA)
        {
            IReadOnlyList<MGElement> inactiveChildren = GetVisualTreeChildren(true, false);
            for (int i = inactiveChildren.Count - 1; i >= 0; i--)
            {
                inactiveChildren[i].Update(UA.ChangeHitTestVisible(false));
            }

            IReadOnlyList<MGElement> activeChildren = GetVisualTreeChildren(false, true);
            for (int i = activeChildren.Count - 1; i >= 0; i--)
            {
                activeChildren[i].Update(UA);
            }
        }

        public event EventHandler<ElementUpdateEventArgs> OnBeginUpdate;
        public event EventHandler<ElementUpdateEventArgs> OnEndUpdate;
        public event EventHandler<ElementUpdateEventArgs> OnBeginUpdateContents;
        public event EventHandler<ElementUpdateEventArgs> OnEndUpdateContents;

		public virtual void UpdateSelf(ElementUpdateArgs UA) { }
#endregion Update

        #region Draw
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ConditionalScaleTransform? _RenderScale;
        /// <summary>A scale transform to apply to this <see cref="MGElement"/> when the appropriate condition is met. (such as <see cref="IsLMBPressed"/> is true)<para/>
        /// This scale transform only affects how the element is rendered, but not its layout, so it may result in overlapping elements. Uses the center of <see cref="LayoutBounds"/> as the scaling origin.<para/>
        /// Default value: null</summary>
        public ConditionalScaleTransform? RenderScale
        {
            get => _RenderScale;
            set
            {
                if (_RenderScale != value)
                {
                    _RenderScale = value;
                    NPC(nameof(RenderScale));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _RecentDrawWasClipped;
        /// <summary>True if the most recent Draw call resulted in this <see cref="MGElement"/> not being drawn, due to one of the following reasons:<para/>
        /// It was out-of-bounds (See: <see cref="ClipToBounds"/>, <see cref="GraphicsDevice.ScissorRectangle"/>, <see cref="RasterizerState.ScissorTestEnable"/>)<br/>
        /// It was not visible (See: <see cref="Visibility"/>)<br/>
        /// It had no geometry (See: <see cref="LayoutBounds"/>, Width and/or Height = 0)</summary>
        public bool RecentDrawWasClipped
        {
            get => _RecentDrawWasClipped;
            private set
            {
                if (_RecentDrawWasClipped != value)
                {
                    _RecentDrawWasClipped = value;
                    _inputStateDirty = true;
                    NPC(nameof(RecentDrawWasClipped));
                }
            }
        }

        protected Rectangle TransformClipBounds(ElementDrawArgs DA, Rectangle bounds)
            => bounds.GetTranslated(DA.Offset).CreateTransformedF(DA.DT.CurrentSettings.Transform).RoundUp();

        protected ClipDefinition CreateRectangleClipDefinition(Rectangle targetBounds, string debugName)
            => ClipDefinition.Rectangle(targetBounds, true, debugName: debugName);

        protected ClipDefinition CreateRoundedClipDefinition(Rectangle targetBounds, MGCornerRadius cornerRadius, MGBoxGeometry geometry,
            string debugName, bool allowRectangleFallback = false)
            => ClipDefinition.RoundedRectangle(targetBounds, cornerRadius.ToClipCornerRadius(), geometry: geometry.ToClipGeometry(),
                intersectWithCurrentClip: true, allowRectangleFallback: allowRectangleFallback, debugName: debugName);

        protected ClipDefinition CreateRoundedClipDefinition(Rectangle targetBounds, MGCornerRadius cornerRadius, ClipGeometry geometry,
            string debugName, bool allowRectangleFallback = false)
            => ClipDefinition.RoundedRectangle(targetBounds, cornerRadius.ToClipCornerRadius(), geometry: geometry,
                intersectWithCurrentClip: true, allowRectangleFallback: allowRectangleFallback, debugName: debugName);

        protected ClipDefinition CreateGeometryClipDefinition(Rectangle targetBounds, MGUI.Shared.Rendering.Clipping.ClipGeometry geometry,
            string debugName, bool allowRectangleFallback = false)
            => ClipDefinition.ArbitraryGeometry(targetBounds, geometry, intersectWithCurrentClip: true,
                allowRectangleFallback: allowRectangleFallback, debugName: debugName);

        protected ClipDefinition CreateBorderBackedContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, string debugName)
        {
            if (!HasBorder)
            {
                return null;
            }

            if (!TryGetRoundedBackgroundShapeAndGeometry(layoutBounds, out MGBoxShape backgroundShape, out MGBoxGeometry backgroundGeometry, false))
            {
                Rectangle backgroundBounds = GetBackgroundBounds(layoutBounds);
                Rectangle rectangleClipBounds = TransformClipBounds(DA, backgroundBounds);
                return CreateRectangleClipDefinition(rectangleClipBounds, debugName);
            }

            Rectangle clipBounds = TransformClipBounds(DA, backgroundShape.OuterBounds);
            if (backgroundShape.InnerCornerRadius.IsZero)
            {
                return CreateRectangleClipDefinition(clipBounds, debugName);
            }

            return CreateRoundedClipDefinition(clipBounds, backgroundShape.InnerCornerRadius, backgroundGeometry.ToClipGeometry(DA.Offset),
                debugName, allowRectangleFallback: true);
        }

        // These hooks describe the logical clip that should constrain drawing. They do not participate in
        // background, border, or overlay painting; those continue to use the existing shape paint pipeline.
        internal virtual ClipDefinition GetSelfClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
            => ClipToBounds ? CreateRectangleClipDefinition(targetBounds, $"{ElementType}.Self") : null;

        internal virtual ClipDefinition GetContentsClipDefinition(ElementDrawArgs DA, Rectangle layoutBounds, Rectangle targetBounds)
            => null;

        public virtual void Draw(ElementDrawArgs DA)
		{
            using var performanceScope = UIPerformanceProbe.BeginElementDraw(this);
			RecentDrawWasClipped = false;

            DA = DA.SetOpacity(DA.Opacity * Opacity) with { VisualState = VisualState };
            MGElementDrawEventArgs DrawEventArgs = new(DA);

			OnBeginDraw?.Invoke(this, DrawEventArgs);

            if (Visibility != Visibility.Visible || LayoutBounds.Width <= 0 || LayoutBounds.Height <= 0)
			{
				RecentDrawWasClipped = true;
				return;
			}

            Rectangle TargetBounds = LayoutBounds.GetTranslated(DA.Offset).CreateTransformedF(DA.DT.CurrentSettings.Transform).RoundUp();

            //  Apply render scale, if any
            IDisposable TempTransform = null;
            if (RenderScale?.TryGetScale(VisualState, out float Scale) == true)
            {
                Matrix Transform =
                    Matrix.CreateTranslation(new Vector3(-TargetBounds.Center.ToVector2(), 0)) *
                    Matrix.CreateScale(Scale) *
                    Matrix.CreateTranslation(new Vector3(TargetBounds.Center.ToVector2(), 0));
                TempTransform = DA.DT.SetTransformTemporary(DA.DT.CurrentSettings.Transform * Transform);

                // Rectangle clip bounds are re-evaluated in render-target space after the transform is applied.
                // Geometry clips keep their local vertices and rely on the active draw transform for stencil/mask backends.
                TargetBounds = TargetBounds.CreateTransformedF(Transform).RoundUp();
            }

			ClipDefinition SelfClipDefinition = GetSelfClipDefinition(DA, LayoutBounds, TargetBounds);
			ClipDefinition ContentsClipDefinition = GetContentsClipDefinition(DA, LayoutBounds, TargetBounds);

            if (!DA.DT.CurrentSettings.UsesScissorTest || !DA.DT.CurrentClipBounds.HasValue || TargetBounds.Intersects(DA.DT.CurrentClipBounds.Value))
			{
				using (SelfClipDefinition == null ? null : DA.Context.PushClipTemporary(SelfClipDefinition))
				{
                    // Decorative layers follow the element's self clip. Content-only clipping starts later,
                    // so rounded shape paint remains independent from whichever clip backend gets selected.
                    foreach (MGElement Component in _componentsDrawBeforeBackground)
                    {
                        Component.Draw(DA);
                    }

                    if (DrawBackgroundEnabled)
                    {
                        DrawBackground(DA, LayoutBounds);
                    }

                    foreach (MGElement Component in _componentsDrawBeforeSelf)
                    {
                        Component.Draw(DA);
                    }

                    DrawSelf(DA, LayoutBounds);

                    using (ContentsClipDefinition == null ? null : DA.Context.PushClipTemporary(ContentsClipDefinition))
                    {
						foreach (MGElement Component in _componentsDrawBeforeContents)
                        {
                            Component.Draw(DA);
                        }

                        DrawContents(DA);

                        foreach (MGElement Component in _componentsDrawAfterContents)
                        {
                            Component.Draw(DA);
                        }
                    }

                    if (DrawOverlayBrushEnabled)
                    {
                        DrawOverlayBrush(DA, LayoutBounds);
                    }

                    OnEndingDraw?.Invoke(this, DrawEventArgs);
                }
            }
            else
			{
				RecentDrawWasClipped = true;
			}

            TempTransform?.Dispose();

            OnEndDraw?.Invoke(this, DrawEventArgs);
		}

		protected virtual void DrawContents(ElementDrawArgs DA) { }

        [DebuggerStepThrough]
        public class MGElementDrawEventArgs : EventArgs
		{
			public readonly ElementDrawArgs DA;

            public MGElementDrawEventArgs(ElementDrawArgs DA)
			{
                this.DA = DA;
			}
		}

		public event EventHandler<MGElementDrawEventArgs> OnBeginDraw;
        /// <summary>Invoked after drawing the <see cref="BackgroundBrush"/>, self, all components, and the <see cref="OverlayBrush"/>,
        /// but while the <see cref="GraphicsDevice.ScissorRectangle"/> is still set to the desired screen-space bounds of this element.<para/>
        /// See also: <see cref="OnEndDraw"/></summary>
        public event EventHandler<MGElementDrawEventArgs> OnEndingDraw;
        /// <summary>Invoked at the very end of <see cref="MGElement.Draw(ElementDrawArgs)"/>.<br/>
        /// Unlike <see cref="OnEndingDraw"/>, this event is invoked AFTER the <see cref="GraphicsDevice.ScissorRectangle"/> has been reverted to its prior value.<para/>
        /// See also: <see cref="OnEndingDraw"/></summary>
		public event EventHandler<MGElementDrawEventArgs> OnEndDraw;

        protected void DrawBackground(ElementDrawArgs DA) => DrawBackground(DA, LayoutBounds);

        private Rectangle GetBackgroundBounds(Rectangle LayoutBounds)
        {
            Rectangle BorderlessBounds = !HasBorder ? LayoutBounds : LayoutBounds.GetCompressed(GetBorder().BorderThickness);
            Rectangle BackgroundBounds = BorderlessBounds.GetCompressed(BackgroundRenderPadding);
            return BackgroundBounds;
        }

        protected bool TryGetRoundedBackgroundShapeAndGeometry(Rectangle layoutBounds, out MGBoxShape backgroundShape, out MGBoxGeometry backgroundGeometry,
            bool overlapUnderBorder)
        {
            if (HasBorder && !GetBorder().CornerRadius.IsZero)
            {
                MGBoxShape boxShape = new MGBoxShape(layoutBounds, GetBorder().BorderThickness, GetBorder().CornerRadius).Normalize();
                if (BackgroundRenderPadding.IsEmpty())
                {
                    MGBoxGeometry boxGeometry = MGBoxGeometryBuilder.Build(boxShape);
                    if (boxGeometry.HasInnerContour)
                    {
                        backgroundGeometry = MGBoxGeometryBuilder.BuildInteriorFillGeometry(boxGeometry, overlapUnderBorder ? 1.0f : 0.0f);
                        backgroundShape = backgroundGeometry.Shape;
                        return true;
                    }
                }

                Rectangle backgroundBounds = boxShape.InnerBounds.GetCompressed(BackgroundRenderPadding);
                backgroundShape = new MGBoxShape(backgroundBounds, new Thickness(0), boxShape.InnerCornerRadius).Normalize();
                backgroundGeometry = MGBoxGeometryBuilder.Build(backgroundShape);
                return true;
            }

            backgroundShape = default;
            backgroundGeometry = default;
            return false;
        }

        public virtual void DrawBackground(ElementDrawArgs DA, Rectangle LayoutBounds)
		{
            if (TryGetRoundedBackgroundShapeAndGeometry(LayoutBounds, out MGBoxShape backgroundShape, out MGBoxGeometry backgroundGeometry, true))
            {
                BackgroundBrush.GetUnderlay(DA.VisualState.Primary)?.Draw(DA, this, backgroundShape, backgroundGeometry);

                SecondaryVisualState secondaryState = DA.VisualState.GetSecondaryState(SpoofIsPressedWhileDrawingBackground, SpoofIsHoveredWhileDrawingBackground);
                BackgroundBrush.GetFillOverlay(secondaryState)?.Draw(DA, this, backgroundShape, backgroundGeometry);
                return;
            }

            Rectangle BackgroundBounds = GetBackgroundBounds(LayoutBounds);
            BackgroundBrush.GetUnderlay(DA.VisualState.Primary)?.Draw(DA, this, BackgroundBounds);
            SecondaryVisualState SecondaryState = DA.VisualState.GetSecondaryState(SpoofIsPressedWhileDrawingBackground, SpoofIsHoveredWhileDrawingBackground);
            BackgroundBrush.GetFillOverlay(SecondaryState)?.Draw(DA, this, BackgroundBounds);
        }

        public virtual void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds) 
            => DrawSelfBaseImplementation(DA, LayoutBounds);

        protected virtual void DrawOverlayBrush(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            if (TryGetRoundedBackgroundShapeAndGeometry(LayoutBounds, out MGBoxShape backgroundShape, out MGBoxGeometry backgroundGeometry, true))
            {
                OverlayBrush?.Draw(DA, this, backgroundShape, backgroundGeometry);
                return;
            }

            OverlayBrush?.Draw(DA, this, GetBackgroundBounds(LayoutBounds));
        }

        protected void DrawSelfBaseImplementation(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            if (HasBorder)
            {
                if (!GetBorder().CornerRadius.IsZero)
                {
                    MGBoxShape boxShape = new MGBoxShape(LayoutBounds, GetBorder().BorderThickness, GetBorder().CornerRadius).Normalize();
                    MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(boxShape);
                    if (DrawBackgroundBorderOverlayEnabled)
                    {
                        BackgroundBrush.GetBorderOverlay(DA.VisualState.Secondary)?.Draw(DA, this, boxShape, geometry);
                    }
                }
                else
                {
                    if (DrawBackgroundBorderOverlayEnabled)
                    {
                        BackgroundBrush.GetBorderOverlay(DA.VisualState.Secondary)?.Draw(DA, this, LayoutBounds, GetBorder().BorderThickness);
                    }
                }
            }
        }
        #endregion Draw

        #region Layout
		internal protected void InvalidateLayout()
		{
            IsLayoutValid = false;
            RecentMeasurementsSelfOnly.Clear();
            RecentMeasurementsFull.Clear();
        }

		internal protected void InvalidateArrange()
		{
            IsLayoutValid = false;
        }

        /// <summary>Recursively invalidates the layout of this element and all its visual-tree descendants,
        /// including <see cref="Components"/>.<para/>
        /// Use this instead of <see cref="InvalidateLayout"/> when the entire subtree's cached measurements
        /// may be stale — for example when a popup window closes and will be re-opened later.<para/>
        /// Note: <see cref="InvalidateLayout"/> and <see cref="LayoutChanged"/> only propagate <b>upward</b>
        /// toward parents. This method fills the gap for downward propagation.</summary>
        internal protected void InvalidateLayoutTree()
        {
            InvalidateLayout();
            // Use indexed for loop to avoid IReadOnlyList enumerator allocation (Task 16)
            IReadOnlyList<MGElement> vtcAll = GetVisualTreeChildren(true, true);
            for (int i = 0; i < vtcAll.Count; i++)
            {
                vtcAll[i].InvalidateLayoutTree();
            }

            foreach (MGComponentBase Component in Components)
            {
                Component.BaseElement.InvalidateLayoutTree();
            }
        }

        internal void InvalidateTemplateValue(UIInvalidationKind invalidation)
        {
            if ((invalidation & (UIInvalidationKind.Measure | UIInvalidationKind.Arrange | UIInvalidationKind.Structure)) != 0)
            {
                LayoutChanged(this, true);
            }
        }

        private void InvalidateLayoutCore(MGElement source, bool notifyParent, bool invalidateMeasurements)
        {
			if (InitializationManager.IsDeferringEvents)
            {
                return;
            }

            UIPerformanceProbe.RecordLayoutInvalidation(source, this, notifyParent);

            if (IsUpdatingLayout)
            {
                SelfOrParentWindow.QueueLayoutRefresh = true;
            }

            if (invalidateMeasurements)
            {
                InvalidateLayout();
            }
            else
            {
                InvalidateArrange();
            }

            if (notifyParent)
            {
                if (invalidateMeasurements)
                {
                    Parent?.LayoutChanged(source, notifyParent);
                }
                else
                {
                    Parent?.ArrangeChanged(source, notifyParent);
                }
            }
        }

        /// <summary>Invoked when a property that affects this <see cref="MGElement"/>'s measured size or arrangement has changed.</summary>
        protected virtual void LayoutChanged(MGElement Source, bool NotifyParent)
            => InvalidateLayoutCore(Source, NotifyParent, invalidateMeasurements: true);

        /// <summary>Invoked when a property changes the arrangement of this element subtree without changing measured sizes.</summary>
        protected virtual void ArrangeChanged(MGElement Source, bool NotifyParent)
            => InvalidateLayoutCore(Source, NotifyParent, invalidateMeasurements: false);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsLayoutValid;
        /// <summary>If true, indicates that the layout will be re-calculated during the next update tick. This also invalidates any cached measurements.</summary>
        public bool IsLayoutValid
        {
            get => _IsLayoutValid;
            private set
            {
                if (_IsLayoutValid != value)
                {
                    _IsLayoutValid = value;
                    NPC(nameof(IsLayoutValid));
                }
            }
        }

        #region Arrange
        protected internal bool IsUpdatingLayout { get; private set; }

        internal protected void UpdateLayout(Rectangle Bounds)
        {
            using var performanceScope = UIPerformanceProbe.BeginElementLayout(this);
            try
            {
                IsUpdatingLayout = true;

                Rectangle PreviousLayoutBounds = LayoutBounds;

                if (Bounds.Width <= 0 || Bounds.Height <= 0)
                {
                    AllocatedBounds = Rectangle.Empty;
                    RenderBounds = Rectangle.Empty;
                    LayoutBounds = Rectangle.Empty;
                    StretchedContentBounds = Rectangle.Empty;
                    AlignedContentBounds = Rectangle.Empty;
                }
                else
                {
                    AllocatedBounds = Bounds;

                    Size BoundsSize = new(Bounds.Width, Bounds.Height);
                    Thickness RequestedSelfSize;
                    Thickness RequestedFullSize;
                    Thickness SharedSize;
                    Thickness RequestedContentSize;
                    if (TryGetCachedMeasurement(BoundsSize, out ElementMeasurement SelfMeasurement, out ElementMeasurement FullMeasurement))
                    {
                        RequestedSelfSize = SelfMeasurement.RequestedSize;
                        RequestedFullSize = FullMeasurement.RequestedSize;
                        SharedSize = SelfMeasurement.SharedSize;
                        RequestedContentSize = FullMeasurement.ContentSize;
                    }
                    else
                    {
                        UpdateMeasurement(BoundsSize, out RequestedSelfSize, out RequestedFullSize, out SharedSize, out RequestedContentSize);
                    }

                    int ConsumedWidth = Math.Min(MaxSizeIncludingMargin.Width, HorizontalAlignment == HorizontalAlignment.Stretch ? BoundsSize.Width : Math.Min(BoundsSize.Width, RequestedFullSize.Width));
                    int ConsumedHeight = Math.Min(MaxSizeIncludingMargin.Height, VerticalAlignment == VerticalAlignment.Stretch ? BoundsSize.Height : Math.Min(BoundsSize.Height, RequestedFullSize.Height));
                    if (ConsumedWidth <= 0 || ConsumedHeight <= 0)
                    {
                        RenderBounds = Rectangle.Empty;
                        LayoutBounds = Rectangle.Empty;
                        StretchedContentBounds = Rectangle.Empty;
                        AlignedContentBounds = Rectangle.Empty;
                    }
                    else
                    {
                        //  Account for cases where stretching horizontally or vertically would cause the bounds to exceed MaxWidth and/or MaxHeight
                        HorizontalAlignment ActualHorizontalAlignment = ResolvedMaxWidth.HasValue && HorizontalAlignment == HorizontalAlignment.Stretch && AllocatedBounds.Width > ResolvedMaxWidth.Value + HorizontalMargin ?
                            HorizontalAlignment.Center :
                            HorizontalAlignment;
                        VerticalAlignment ActualVerticalAlignment = ResolvedMaxHeight.HasValue && VerticalAlignment == VerticalAlignment.Stretch && AllocatedBounds.Height > ResolvedMaxHeight.Value + VerticalMargin ?
                            VerticalAlignment.Center :
                            VerticalAlignment;

                        Size RenderSize = new(ConsumedWidth, ConsumedHeight);
                        RenderBounds = ApplyAlignment(AllocatedBounds, ActualHorizontalAlignment, ActualVerticalAlignment, RenderSize);

                        LayoutBounds = new(RenderBounds.Left + ResolvedMargin.Left, RenderBounds.Top + ResolvedMargin.Top,
                            RenderBounds.Width - HorizontalMargin, RenderBounds.Height - VerticalMargin);
                        if (LayoutBounds.Width <= 0 || LayoutBounds.Height <= 0)
                        {
                            LayoutBounds = Rectangle.Empty;
                            StretchedContentBounds = Rectangle.Empty;
                            AlignedContentBounds = Rectangle.Empty;
                        }
                        else
                        {
                            Rectangle RemainingComponentBounds = LayoutBounds;
                            foreach (MGComponentBase Component in Components)
                            {
                                Component.BaseElement.UpdateMeasurement(RemainingComponentBounds.Size, out _, out Thickness ComponentSize, out _, out _);
                                Rectangle ComponentBounds = Component.Arrange(RemainingComponentBounds, ComponentSize);
                                Component.BaseElement.UpdateLayout(ComponentBounds);

                                int Left = RemainingComponentBounds.Left;
                                int Right = RemainingComponentBounds.Right;
                                int Top = RemainingComponentBounds.Top;
                                int Bottom = RemainingComponentBounds.Bottom;

                                Thickness ConsumedSpace = Component.ConsumesAnySpace ? Component.Arrange(ComponentSize) : new(0);
                                if (!Component.IsWidthSharedWithContent)
                                {
                                    Left += ConsumedSpace.Left;
                                    Right -= ConsumedSpace.Right;
                                }

                                if (!Component.IsHeightSharedWithContent)
                                {
                                    Top += ConsumedSpace.Top;
                                    Bottom -= ConsumedSpace.Bottom;
                                }

                                RemainingComponentBounds = new(Left, Top, Right - Left, Bottom - Top);
                            }

                            int ContentBoundsLeft = Math.Min(RenderBounds.Right, RenderBounds.Left + RequestedSelfSize.Left - SharedSize.Left);
                            int ContentBoundsTop = Math.Min(RenderBounds.Bottom, RenderBounds.Top + RequestedSelfSize.Top - SharedSize.Top);
                            int ContentBoundsRight = Math.Max(RenderBounds.Left, RenderBounds.Right - RequestedSelfSize.Right - SharedSize.Right);
                            int ContentBoundsBottom = Math.Max(RenderBounds.Top, RenderBounds.Bottom - RequestedSelfSize.Bottom - SharedSize.Bottom);

                            StretchedContentBounds = new(ContentBoundsLeft, ContentBoundsTop,
                                Math.Max(0, ContentBoundsRight - ContentBoundsLeft), Math.Max(0, ContentBoundsBottom - ContentBoundsTop));

                            //  Determine how much space the content consumes based on VerticalContentAlignment / HorizontalContentAlignment
                            int ContentConsumedWidth;
                            int ContentConsumedHeight;
                            if (HorizontalContentAlignment == HorizontalAlignment.Stretch && VerticalContentAlignment == VerticalAlignment.Stretch)
                            {
                                ContentConsumedWidth = StretchedContentBounds.Width;
                                ContentConsumedHeight = StretchedContentBounds.Height;
                            }
                            else
                            {
                                ContentConsumedWidth = HorizontalContentAlignment == HorizontalAlignment.Stretch ? StretchedContentBounds.Width : Math.Min(StretchedContentBounds.Width, RequestedContentSize.Width);
                                ContentConsumedHeight = VerticalContentAlignment == VerticalAlignment.Stretch ? StretchedContentBounds.Height : Math.Min(StretchedContentBounds.Height, RequestedContentSize.Height);
                            }

                            //  Set bounds for content
                            if (ContentConsumedWidth <= 0 || ContentConsumedHeight <= 0)
                            {
                                AlignedContentBounds = Rectangle.Empty;
                            }
                            else
                            {
                                Size ContentSize = new(ContentConsumedWidth, ContentConsumedHeight);
                                AlignedContentBounds = ApplyAlignment(StretchedContentBounds, HorizontalContentAlignment, VerticalContentAlignment, ContentSize);
                            }

                            UpdateContentLayout(AlignedContentBounds);
                        }
                    }
                }

                IsLayoutValid = true;
                OnLayoutUpdated?.Invoke(this, EventArgs.Empty);
                OnLayoutBoundsChanged?.Invoke(this, new(PreviousLayoutBounds, LayoutBounds));
            }
            finally { IsUpdatingLayout = false; }
        }

		public event EventHandler<EventArgs> OnLayoutUpdated;
		public event EventHandler<EventArgs<Rectangle>> OnLayoutBoundsChanged;

        protected virtual void UpdateContentLayout(Rectangle Bounds) { }
        #endregion Arrange

        #region Measure
        protected const int MeasurementCacheSize = 15;

		/// <summary>Full measurements - I.E. includes the requested size of this <see cref="MGElement"/> and its content, if any.<para/>
		/// See also: <see cref="RecentMeasurementsSelfOnly"/></summary>
		private List<ElementMeasurement> RecentMeasurementsFull { get; } = new();
        private void CacheFullMeasurement(ElementMeasurement Value)
        {
            while (RecentMeasurementsFull.Count > MeasurementCacheSize)
                RecentMeasurementsFull.RemoveAt(RecentMeasurementsFull.Count - 1);
            if (!RecentMeasurementsFull.Contains(Value))
            {
                RecentMeasurementsFull.Insert(0, Value);
            }
        }

		/// <summary>Recent measurements that only account for the requested size of this <see cref="MGElement"/>; does not include the requested size of its content, if any.<br/>
		/// For example, a Border would only account for the total width of it's Margin, Padding, and BorderThickness.<para/>
		/// See also: <see cref="RecentMeasurementsFull"/></summary>
		private List<ElementMeasurement> RecentMeasurementsSelfOnly { get; } = new();
        private void CacheSelfMeasurement(ElementMeasurement Value)
        {
            while (RecentMeasurementsSelfOnly.Count > MeasurementCacheSize)
                RecentMeasurementsSelfOnly.RemoveAt(RecentMeasurementsSelfOnly.Count - 1);
            if (!RecentMeasurementsSelfOnly.Contains(Value))
            {
                RecentMeasurementsSelfOnly.Insert(0, Value);
            }
        }

        /// <param name="SelfMeasurement">A measurement that only accounts for this <see cref="MGElement"/> and not its content.<br/>
        /// For example, for a Border, this would include Margin, Padding, and BorderThickness.</param>
        /// <param name="FullMeasurement">A measurement that accounts for both this <see cref="MGElement"/> self-measurement and the measurement of its content, if any.</param>
        protected bool TryGetCachedMeasurement(Size AvailableSize, out ElementMeasurement SelfMeasurement, out ElementMeasurement FullMeasurement)
		{
            // Arrange-only invalidation preserves measurement caches even though layout bounds are stale.
            // Reuse the cached sizes so parent containers can rearrange without remeasuring unchanged subtrees.
            if (RecentMeasurementsSelfOnly.Count > 0 && RecentMeasurementsFull.Count > 0)
			{
				foreach (ElementMeasurement Self in RecentMeasurementsSelfOnly)
				{
					if (Self.AvailableSize == AvailableSize || 
						(Self.IsAvailableSizeGreaterThanOrEqual(AvailableSize) && Self.IsRequestedSizeLessThanOrEqual(AvailableSize))) 
							// If we decreased the available size, but it's still at least as much as what requested, we can re-use the cached measurement.
							// We can't re-use the measurement if we increased the available size, because wrappable content may end up consuming more width and less height
					{
                        SelfMeasurement = Self;

						foreach (ElementMeasurement Full in RecentMeasurementsFull)
						{
                            if (Full.AvailableSize == AvailableSize || (Full.IsAvailableSizeGreaterThanOrEqual(AvailableSize) && Full.IsRequestedSizeLessThanOrEqual(AvailableSize)))
							{
								FullMeasurement = Full;
								return true;
							}
						}

						break;
					}
				}
			}

            SelfMeasurement = default;
			FullMeasurement = default;
			return false;
		}

        protected virtual bool CanCacheSelfMeasurement { get => true; }

		private bool TryGetRecentSelfMeasurement(Size AvailableSize, out Thickness SelfSize, out Thickness SharedSize, out Thickness ContentSize)
		{
            if (CanCacheSelfMeasurement)
            {
                foreach (ElementMeasurement Measurement in RecentMeasurementsSelfOnly)
                {
                    if (Measurement.AvailableSize == AvailableSize || (Measurement.IsAvailableSizeGreaterThanOrEqual(AvailableSize) && Measurement.IsRequestedSizeLessThanOrEqual(AvailableSize)))
                    {
                        SelfSize = Measurement.RequestedSize;
                        SharedSize = Measurement.SharedSize;
                        ContentSize = Measurement.ContentSize;
                        return true;
                    }
                }
            }

			SelfSize = default;
			SharedSize = default;
            ContentSize = default;
			return false;
		}

        /// <summary>If true, this element is capable of consuming non-zero width when the height is zero, or a non-zero height when the width is zero.<br/>
        /// Default value is false, which means that if the element is assigned 0 width or height, then BOTH the width and height are automatically truncated to zero.<para/>
        /// For example, if an <see cref="MGButton"/> requested 16x16, but there was no vertical space available, then rather than allocating it 16x0, it is allocated 0x0</summary>
        protected virtual bool CanConsumeSpaceInSingleDimension => false;

        internal protected void UpdateMeasurement(Size AvailableSize, out Thickness SelfSize, out Thickness FullSize, out Thickness SharedSize, out Thickness ContentSize)
        {
            using var performanceScope = UIPerformanceProbe.BeginElementMeasure(this);
			if (IsVisibilityCollapsed)
			{
				SelfSize = new(0);
                FullSize = new(0);
                SharedSize = new(0);
                ContentSize = new(0);
				return;
			}

			AvailableSize = AvailableSize.AsZeroOrGreater();

            if (TryGetCachedMeasurement(AvailableSize, out ElementMeasurement CachedSelfMeasurement, out ElementMeasurement CachedFullMeasurement))
            {
                SelfSize = CachedSelfMeasurement.RequestedSize;
                FullSize = CachedFullMeasurement.RequestedSize;
                SharedSize = CachedSelfMeasurement.SharedSize;
                ContentSize = CachedFullMeasurement.ContentSize;
                return;
            }

            //  Truncate the available size based on this element's MaxSize and preferred width/height
            int? actualPreferredWidth = IgnorePreferredWidthDuringMeasure ? null : ActualPreferredWidth;
            int? actualPreferredHeight = IgnorePreferredHeightDuringMeasure ? null : ActualPreferredHeight;

            Size RemainingSize = new(
                Math.Clamp(AvailableSize.Width, 0, Math.Min(actualPreferredWidth ?? int.MaxValue, MaxSizeIncludingMargin.Width)), 
                Math.Clamp(AvailableSize.Height, 0, Math.Min(actualPreferredHeight ?? int.MaxValue, MaxSizeIncludingMargin.Height))
            );

            if (!TryGetRecentSelfMeasurement(RemainingSize, out SelfSize, out SharedSize, out ContentSize))
			{
                SelfSize = MeasureSelf(RemainingSize, out SharedSize);
                ElementMeasurement SelfMeasurement = new(AvailableSize, SelfSize, SharedSize, ContentSize);
                CacheSelfMeasurement(SelfMeasurement);
            }

			Thickness UnsharedSelfSize = SelfSize.Subtract(SharedSize);
			RemainingSize = RemainingSize.Subtract(UnsharedSelfSize.Size, 0, 0);

			ContentSize = UpdateContentMeasurement(RemainingSize);
			FullSize = new Thickness(UnsharedSelfSize.Left + Math.Max(SharedSize.Left, ContentSize.Left),
				UnsharedSelfSize.Top + Math.Max(SharedSize.Top, ContentSize.Top),
				UnsharedSelfSize.Right + Math.Max(SharedSize.Right, ContentSize.Right),
				UnsharedSelfSize.Bottom + Math.Max(SharedSize.Bottom, ContentSize.Bottom));

			//  Adjust width/height based on preferred values
            if (actualPreferredWidth.HasValue || actualPreferredHeight.HasValue)
			{
             Size PreferredSize = new(actualPreferredWidth ?? FullSize.Width, actualPreferredHeight ?? FullSize.Height);
				FullSize = FullSize.Clamp(PreferredSize, PreferredSize);
			}

			//  Adjust width/height based on min/max sizes
			FullSize = FullSize.Clamp(MinSizeIncludingMargin, MaxSizeIncludingMargin).Clamp(Size.Empty, AvailableSize);

            if ((FullSize.Width <= 0 || FullSize.Height <= 0) && !CanConsumeSpaceInSingleDimension)
            {
                FullSize = new(0);
            }
            ElementMeasurement FullMeasurement = new(AvailableSize, FullSize, SharedSize, ContentSize);
            CacheFullMeasurement(FullMeasurement);
        }

        protected internal virtual bool IgnorePreferredWidthDuringMeasure => false;

        protected internal virtual bool IgnorePreferredHeightDuringMeasure => false;

        /// <summary>Returns the screen space required to display this <see cref="MGElement"/> if it has no child elements.<para/>
        /// For simple elements, this accounts for <see cref="Margin"/> and <see cref="Padding"/>.<br/>
        /// For a Border, this would also include the BorderThickness.<br/>
        /// For a TextBlock, this would include the size of the text content etc.</summary>
        /// <param name="SharedSize">Typically 0. This represents how much of the self measurement can be shared with the measurement of the content.<para/>
        /// For example, a checkbox contains a checkable rectangular portion that is in-line with the content.<br/>
        /// So if that checkable rectangle has height=16, and content has height=20, the total height is '16 + Math.Max(0, 20-16)' rather than 16+20=36.</param>
        protected Thickness MeasureSelf(Size AvailableSize, out Thickness SharedSize)
		{
			if (Visibility == Visibility.Collapsed)
			{
				SharedSize = new(0);
				return new(0);
			}

			Thickness Total = new(0);
            Size RemainingSize = AvailableSize;

            Thickness MarginAndPadding = ResolvedMargin.Add(ResolvedPadding);
			Total = Total.Add(MarginAndPadding);
            RemainingSize = RemainingSize.Subtract(MarginSize, 0, 0);

            Thickness Overridden = MeasureSelfOverride(RemainingSize, out SharedSize);
			Total = Total.Add(Overridden);
			RemainingSize = RemainingSize.Subtract(Overridden.Size, 0, 0);

            // Fix for component measurement Bug 1:
            //   Components that share their size with content also share with each other.
            //   Use element-wise MAX (not SUM) for shared sizes, and SUM for unshared sizes.
            //   See the TODO comment near the top of this file for the full description.
            Thickness MaxSharedComponentSize = new(0);  // element-wise MAX of all shared-with-content component dimensions
            Thickness UnsharedComponentSum = new(0);    // SUM of component dimensions that are NOT shared with content
			foreach (MGComponentBase Component in Components)
			{
				Size RemainingSizeForComponent = Component.UsesOwnersPadding ? RemainingSize.Subtract(PaddingSize, 0, 0) : RemainingSize;

				MGElement Element = Component.BaseElement;
                Element.UpdateMeasurement(RemainingSizeForComponent, out _, out Thickness ComponentSize, out _, out _);

Thickness ActualComponentSize = Component.ConsumesAnySpace ? Component.Arrange(ComponentSize) : new(0);
				Thickness ComponentSharedSize = new(
					Component.IsWidthSharedWithContent ? ActualComponentSize.Left : 0,
					Component.IsHeightSharedWithContent ? ActualComponentSize.Top : 0,
					Component.IsWidthSharedWithContent ? ActualComponentSize.Right : 0,
					Component.IsHeightSharedWithContent ? ActualComponentSize.Bottom : 0);

                // Track element-wise MAX of shared sizes (components sharing with content also share with each other)
                MaxSharedComponentSize = new(
                    Math.Max(MaxSharedComponentSize.Left, ComponentSharedSize.Left),
                    Math.Max(MaxSharedComponentSize.Top, ComponentSharedSize.Top),
                    Math.Max(MaxSharedComponentSize.Right, ComponentSharedSize.Right),
                    Math.Max(MaxSharedComponentSize.Bottom, ComponentSharedSize.Bottom));

                // Sum the unshared portion of this component
                Thickness ComponentUnsharedSize = new(
                    Component.IsWidthSharedWithContent ? 0 : ActualComponentSize.Left,
                    Component.IsHeightSharedWithContent ? 0 : ActualComponentSize.Top,
                    Component.IsWidthSharedWithContent ? 0 : ActualComponentSize.Right,
                    Component.IsHeightSharedWithContent ? 0 : ActualComponentSize.Bottom);
                UnsharedComponentSum = UnsharedComponentSum.Add(ComponentUnsharedSize);

                RemainingSize = RemainingSize.Subtract(ActualComponentSize.Size, 0, 0);
			}

            // SharedSize = element-wise max of all shared component sizes
            // (used by outer measurement to compute Max(SharedSize, ContentSize))
            SharedSize = SharedSize.Add(MaxSharedComponentSize);
            // Total component contribution = unshared sum + max-shared
            Thickness TotalComponentSize = UnsharedComponentSum.Add(MaxSharedComponentSize);
            Total = Total.Add(TotalComponentSize);

            if (Total.Width <= 0 && Total.Height <= 0)
            {
                Total = new(0);
            }

            return Total;
		}

		/// <summary>This method should not include <see cref="Margin"/> nor <see cref="Padding"/>.</summary>
		/// <param name="SharedSize">Typically 0. This represents how much of the self measurement can be shared with the measurement of the content.<para/>
		/// For example, a checkbox contains a checkable rectangular portion that is in-line with the content.<br/>
		/// So if that checkable rectangle has height=16, and content has height=20, the total height is '16 + Math.Max(0, 20-16)' rather than 16+20=36.</param>
		public virtual Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
		{
			SharedSize = new(0);
			return new(0);
		}

        //  Note: This is split into 2 methods because MGContentHost declares an abstract override on UpdateContentMeasurement,
        //  but some derived classes may still want to call the base implementation
        //  More info: https://stackoverflow.com/questions/42271087/how-to-call-a-base-method-that-is-declared-as-abstract-override
        protected virtual Thickness UpdateContentMeasurement(Size AvailableSize) => UpdateContentMeasurementBaseImplementation(AvailableSize);
		protected Thickness UpdateContentMeasurementBaseImplementation(Size AvailableSize) => new(0);
        #endregion Measure
        #endregion Layout

        #region Visual Tree
        private static readonly IReadOnlyList<MGElement> _emptyElementList = new List<MGElement>(0).AsReadOnly();

        /// <param name="IncludeInactive">If true, inactive content (child content which is unable to receive inputs, regardless of its <see cref="IsHitTestVisible"/> value,<br/>
        /// such as an unselected tab within a <see cref="MGTabControl"/>) will also be enumerated.</param>
        /// <param name="IncludeActive">If true, active content will also be enumerated. Recommended value: true</param>
        public virtual IReadOnlyList<MGElement> GetVisualTreeChildren(bool IncludeInactive, bool IncludeActive)
        {
            if (IncludeActive)
            {
                // Cache (Task 16): store the resolved active-children list between calls and only recompute
                // when _vtcCacheDirty is true (set by InvalidateVtcCache() from children-collection changes).
                if (!IncludeInactive)
                {
                    if (!_vtcCacheDirty && _vtcCacheActiveOnly != null)
                    {
                        return _vtcCacheActiveOnly;
                    }

                    IEnumerable<MGElement> children = GetChildren();
                    List<MGElement> result = new();
                    foreach (MGElement Child in children)
                    {
                        result.Add(Child);
                    }
                    _vtcCacheActiveOnly = result;
                    _vtcCacheDirty = false;
                    return result;
                }

                // (true, true) — include inactive + active; not cached per-frame because inactive set
                // may change without a _Children mutation (e.g. tab-selection changes).
                IEnumerable<MGElement> allChildren = GetChildren();
                if (allChildren is IReadOnlyList<MGElement> arl)
                {
                    return arl;
                }

                if (allChildren is List<MGElement> al)
                {
                    return al;
                }

                List<MGElement> allResult = new();
                foreach (MGElement Child in allChildren)
                {
                    allResult.Add(Child);
                }

                return allResult;
            }
            return _emptyElementList;
        }

        // ---- Visual-tree-children cache (Task 16) -----------------------------------
        // Caches the result of GetVisualTreeChildren(false, true) between frames so that
        // repeated calls within the same frame avoid re-resolving the underlying collection.
        // Invalidated by InvalidateVtcCache() whenever children are added or removed.
        private IReadOnlyList<MGElement> _vtcCacheActiveOnly;
        private bool _vtcCacheDirty = true;

        /// <summary>Invalidates the cached result of <see cref="GetVisualTreeChildren(bool, bool)"/>.<para/>
        /// Call this whenever the set of active visual-tree children changes (e.g. when <see cref="_Children"/> is mutated).</summary>
        protected internal void InvalidateVtcCache()
        {
            _vtcCacheDirty = true;
            _vtcCacheActiveOnly = null;
        }
        // ---- End cache fields -------------------------------------------------------

        public enum TreeTraversalMode
        {
            /// <summary>Visits the root node, then visits its children</summary>
            Preorder,
            /// <summary>Visits the root node's children, then visits itself</summary>
            Postorder
        }

        public bool TryFindParentOfType<T>(out T Result, bool IncludeSelf = false)
        {
            if (IncludeSelf && this is T TypedItem)
            {
                Result = TypedItem;
                return true;
            }
            else if (Parent != null)
            {
                return Parent.TryFindParentOfType(out Result, true);
            }
            else
            {
                Result = default;
                return false;
            }
        }

        /// <summary>Returns true if this <see cref="MGElement"/> is the same reference as the given <paramref name="Element"/>,<br/>
        /// or if this <see cref="MGElement"/> is a parent of the given <paramref name="Element"/> anywhere along the visual tree (does not need to be the immediate parent)</summary>
        public bool IsSelfOrAncestorOf(MGElement Element)
        {
            MGElement Current = Element;

            while (Current != null)
            {
                if (Current == this)
                {
                    return true;
                }

                Current = Current.Parent;
            }

            return false;
        }

        /// <param name="IncludeSelf">If true, may return a reference to this element, or to one of this element's components (if <paramref name="IncludeComponents"/> is also true).</param>
        /// <param name="IncludeComponents">If true, may returns a reference to a component of an element, such as the button part of an <see cref="MGCheckBox"/>.</param>
        /// <param name="TraversalMode">The order to visit the nodes in.</param>
        public IEnumerable<MGElement> TraverseVisualTree(bool IncludeSelf = true, bool IncludeComponents = true, bool IncludeToolTips = true, bool IncludeContextMenus = true, TreeTraversalMode TraversalMode = TreeTraversalMode.Preorder)
            => TraverseVisualTree<MGElement>(IncludeSelf, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode);

        /// <param name="IncludeSelf">If true, may return a reference to this element, or to one of this element's components (if <paramref name="IncludeComponents"/> is also true).</param>
        /// <param name="IncludeComponents">If true, may returns a reference to a component of an element, such as the button part of an <see cref="MGCheckBox"/>.</param>
        /// <param name="TraversalMode">The order to visit the nodes in.</param>
        public IEnumerable<T> TraverseVisualTree<T>(bool IncludeSelf = true, bool IncludeComponents = true,
            bool IncludeToolTips = true, bool IncludeContextMenus = true, TreeTraversalMode TraversalMode = TreeTraversalMode.Preorder)
            where T : MGElement
        {
            if (TraversalMode == TreeTraversalMode.Preorder && IncludeSelf)
            {
                if (this is T TypedItem)
                {
                    yield return TypedItem;
                }

                if (IncludeComponents)
				{
					foreach (MGComponentBase Component in Components)
					{
						foreach (T Item in Component.BaseElement.TraverseVisualTree<T>(true, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode))
                        {
                            yield return Item;
                        }
                    }
				}

                if (IncludeToolTips && ToolTip != null)
                {
                    foreach (T Item in ToolTip.TraverseVisualTree<T>(true, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode))
                    {
                        yield return Item;
                    }
                }

                if (IncludeContextMenus && ContextMenu != null)
                {
                    foreach (T Item in ContextMenu.TraverseVisualTree<T>(true, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode))
                    {
                        yield return Item;
                    }
                }
            }

            foreach (MGElement Child in GetVisualTreeChildren(true, true))
			{
				foreach (T Item in Child.TraverseVisualTree<T>(true, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode))
                {
                    yield return Item;
                }
            }

			if (TraversalMode == TreeTraversalMode.Postorder && IncludeSelf)
			{
                if (IncludeComponents)
                {
                    foreach (MGComponentBase Component in Components)
                    {
                        foreach (T Item in Component.BaseElement.TraverseVisualTree<T>(true, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode))
                        {
                            yield return Item;
                        }
                    }
                }

                if (IncludeToolTips && ToolTip != null)
                {
                    foreach (T Item in ToolTip.TraverseVisualTree<T>(true, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode))
                    {
                        yield return Item;
                    }
                }

                if (IncludeContextMenus && ContextMenu != null)
                {
                    foreach (T Item in ContextMenu.TraverseVisualTree<T>(true, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode))
                    {
                        yield return Item;
                    }
                }

                if (this is T TypedItem)
                {
                    yield return TypedItem;
                }
            }
        }

        /// <summary>Returns the first element in the visual tree that satisfies the given <paramref name="Predicate"/></summary>
        /// <param name="IncludeSelf">If true, may return a reference to this element, or to one of this element's components (if <paramref name="IncludeComponents"/> is also true).</param>
        /// <param name="IncludeComponents">If true, may returns a reference to a component of an element, such as the button part of an <see cref="MGCheckBox"/>.</param>
        /// <param name="TraversalMode">The order to visit the nodes and evaluate the <paramref name="Predicate"/> in.</param>
        /// <returns>The first match, or null if no valid match was found.</returns>
        public MGElement FindElement(Predicate<MGElement> Predicate, bool IncludeSelf = true, bool IncludeComponents = true,
            bool IncludeToolTips = true, bool IncludeContextMenus = true, TreeTraversalMode TraversalMode = TreeTraversalMode.Preorder)
            => TraverseVisualTree<MGElement>(IncludeSelf, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode).FirstOrDefault(x => Predicate(x));

        /// <summary>Returns the first element in the visual tree of type=<typeparamref name="T"/> that satisfies the given <paramref name="Predicate"/></summary>
        /// <param name="IncludeSelf">If true, may return a reference to this element, or to one of this element's components (if <paramref name="IncludeComponents"/> is also true).</param>
        /// <param name="IncludeComponents">If true, may returns a reference to a component of an element, such as the button part of an <see cref="MGCheckBox"/>.</param>
        /// <param name="TraversalMode">The order to visit the nodes and evaluate the <paramref name="Predicate"/> in.</param>
        /// <returns>The first match, or null if no valid match was found.</returns>
        public T FindElement<T>(Predicate<T> Predicate, bool IncludeSelf = true, bool IncludeComponents = true,
            bool IncludeToolTips = true, bool IncludeContextMenus = true, TreeTraversalMode TraversalMode = TreeTraversalMode.Preorder)
			where T : MGElement
			=> TraverseVisualTree<T>(IncludeSelf, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode).FirstOrDefault(x => Predicate(x));

        /// <summary>Returns all elements in the visual tree that satisfy the given <paramref name="Predicate"/></summary>
        /// <param name="IncludeSelf">If true, may return a reference to this element, or to one of this element's components (if <paramref name="IncludeComponents"/> is also true).</param>
        /// <param name="IncludeComponents">If true, may returns a reference to a component of an element, such as the button part of an <see cref="MGCheckBox"/>.</param>
        /// <param name="TraversalMode">The order to visit the nodes and evaluate the <paramref name="Predicate"/> in.</param>
        public IEnumerable<MGElement> GetElements(Predicate<MGElement> Predicate, bool IncludeSelf = true, bool IncludeComponents = true,
            bool IncludeToolTips = true, bool IncludeContextMenus = true, TreeTraversalMode TraversalMode = TreeTraversalMode.Preorder)
            => TraverseVisualTree<MGElement>(IncludeSelf, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode).Where(x => Predicate(x));

        /// <summary>Returns all elements in the visual tree of type=<typeparamref name="T"/> that satisfy the given <paramref name="Predicate"/></summary>
        /// <param name="IncludeSelf">If true, may return a reference to this element, or to one of this element's components (if <paramref name="IncludeComponents"/> is also true).</param>
        /// <param name="IncludeComponents">If true, may returns a reference to a component of an element, such as the button part of an <see cref="MGCheckBox"/>.</param>
        /// <param name="TraversalMode">The order to visit the nodes and evaluate the <paramref name="Predicate"/> in.</param>
        public IEnumerable<T> GetElements<T>(Predicate<T> Predicate, bool IncludeSelf = true, bool IncludeComponents = true,
            bool IncludeToolTips = true, bool IncludeContextMenus = true, TreeTraversalMode TraversalMode = TreeTraversalMode.Preorder)
            where T : MGElement
            => TraverseVisualTree<T>(IncludeSelf, IncludeComponents, IncludeToolTips, IncludeContextMenus, TraversalMode).Where(x => Predicate(x));

        public IEnumerable<MGElement> EnumerateVisualTree(bool IncludeSelf = true)
            => TraverseVisualTree(IncludeSelf, true, true, true, TreeTraversalMode.Preorder);
        #endregion Visual Tree
	}
}
