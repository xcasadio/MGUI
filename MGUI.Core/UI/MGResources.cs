using MGUI.Core.UI.XAML;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Size = MonoGame.Extended.Size;

namespace MGUI.Core.UI
{
    /// <summary>Stores various resources in Dictionary Key-Value pairs so that they can be accessed by their string keys.<para/>
    /// For example: the name of the command that an <see cref="MGButton"/> executes when clicked, the name of a <see cref="Texture2D"/> that an <see cref="MGImage"/> draws,
    /// or the name of an object in <see cref="StaticResources"/> for databinding purposes.<para/>
    /// This instance is usually accessed via <see cref="MGDesktop.Resources"/> (See also: <see cref="MGElement.GetResources"/>)</summary>
    public class MGResourceDefinitions
    {
        private MGResources Owner { get; }

        internal MGResourceDefinitions(MGResources Owner)
        {
            this.Owner = Owner ?? throw new ArgumentNullException(nameof(Owner));
        }

        public IReadOnlyDictionary<string, Action<MGElement>> Commands => Owner.Commands;
        public IReadOnlyDictionary<string, MGToolTip> NamedToolTips => Owner.NamedToolTips;
        public IReadOnlyDictionary<string, MGTheme> Themes => Owner.Themes;
        public MGTheme DefaultTheme => Owner.DefaultTheme;
        public IReadOnlyDictionary<MGElementType, Style> ImplicitStyles => Owner.ImplicitStyles;
        public IReadOnlyDictionary<string, Style> Styles => Owner.Styles;
        public IReadOnlyDictionary<string, object> StaticResources => Owner.StaticResources;
        public IReadOnlyDictionary<string, MGElementTemplate> ElementTemplates => Owner.ElementTemplates;
        public IReadOnlyDictionary<string, MGControlTemplate> ControlTemplates => Owner.ControlTemplates;
    }

    public class MGResourceRuntimeCache
    {
        private MGResources Owner { get; }

        internal MGResourceRuntimeCache(MGResources Owner)
        {
            this.Owner = Owner ?? throw new ArgumentNullException(nameof(Owner));
        }

        public IUIAssetProvider AssetProvider => Owner.AssetProvider;
        public IReadOnlyDictionary<string, MGTextureData> Textures => Owner.Textures;
    }

    public class MGResources
    {
        public UIResourceScope Scope { get; }
        public MGResources Parent { get; private set; }
        public IUIAssetProvider AssetProvider { get; }
        public MGResourceDefinitions Definitions { get; }
        public MGResourceRuntimeCache RuntimeCache { get; }

        public MGResources(MGTheme DefaultTheme)
            : this(DefaultTheme, null, null, UIResourceScope.Desktop) { }

        public MGResources(MGTheme DefaultTheme, IUIAssetProvider AssetProvider)
            : this(DefaultTheme, AssetProvider, null, UIResourceScope.Desktop) { }

        public MGResources(MGResources Parent, UIResourceScope Scope)
            : this(null, Parent?.AssetProvider, Parent, Scope) { }

        public MGResources(MGTheme DefaultTheme, IUIAssetProvider AssetProvider, MGResources Parent, UIResourceScope Scope)
        {
            this.Scope = Scope;
            this.AssetProvider = AssetProvider ?? Parent?.AssetProvider;
            _DefaultTheme = DefaultTheme;
            Definitions = new(this);
            RuntimeCache = new(this);
            SetParent(Parent);
        }

        public void SetParent(MGResources Parent)
        {
            if (!ReferenceEquals(this, Parent))
            {
                MGTheme PreviousTheme = _DefaultTheme == null ? this.Parent?.DefaultTheme : null;

                if (this.Parent != null)
                {
                    this.Parent.OnDefaultThemeChanged -= Parent_OnDefaultThemeChanged;
                }

                this.Parent = Parent;

                if (this.Parent != null)
                {
                    this.Parent.OnDefaultThemeChanged += Parent_OnDefaultThemeChanged;
                }

                if (_DefaultTheme == null)
                {
                    MGTheme CurrentTheme = this.Parent?.DefaultTheme;
                    if (!ReferenceEquals(PreviousTheme, CurrentTheme) && PreviousTheme != null && CurrentTheme != null)
                    {
                        OnDefaultThemeChanged?.Invoke(this, (PreviousTheme, CurrentTheme));
                    }
                }
            }
        }

        private void Parent_OnDefaultThemeChanged(object sender, (MGTheme PreviousTheme, MGTheme Theme) e)
        {
            if (_DefaultTheme == null)
            {
                OnDefaultThemeChanged?.Invoke(this, e);
            }
        }

        public IEnumerable<MGResources> EnumerateSelfAndAncestors()
        {
            for (MGResources Current = this; Current != null; Current = Current.Parent)
            {
                yield return Current;
            }
        }

        #region Textures
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, MGTextureData> _Textures = new();
        public IReadOnlyDictionary<string, MGTextureData> Textures => _Textures;

        public void AddTexture(string Name, MGTextureData Data)
        {
            _Textures.Add(Name, Data);
            OnTextureAdded?.Invoke(this, (Name, Data));
        }

        public bool RemoveTexture(string Name)
        {
            if (_Textures.TryGetValue(Name, out MGTextureData Data))
            {
                _Textures.Remove(Name);
                OnTextureRemoved?.Invoke(this, (Name, Data));
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryGetTexture(string Name, out MGTextureData Data)
        {
            if (Name != null && _Textures.TryGetValue(Name, out Data))
            {
                return true;
            }
            else if (Parent?.TryGetTexture(Name, out Data) == true)
            {
                return true;
            }
            else
            {
                Data = default;
                return false;
            }
        }

        public bool TryLoadImage(string Name, string AssetName)
        {
            if (AssetProvider == null || !AssetProvider.TryLoadImage(AssetName, out IUIImageResource Image))
            {
                return false;
            }

            AddTexture(Name, new(Image));
            return true;
        }

        public bool TryLoadTexture(string Name, string AssetName)
            => TryLoadImage(Name, AssetName);

        internal (int? Width, int? Height) GetTextureDimensions(string Name)
        {
            if (TryGetTexture(Name, out MGTextureData Texture))
            {
                Size Size = Texture.RenderSize;
                return (Size.Width, Size.Height);
            }
            else
            {
                return (null, null);
            }
        }

        public bool TryDrawTexture(IUIDrawContext DT, string Name, Rectangle TargetBounds, float Opacity = 1.0f, Color? Color = null)
            => TryDrawTexture(DT, Name, TargetBounds.TopLeft(), TargetBounds.Width, TargetBounds.Height, Opacity, Color);
        public bool TryDrawTexture(IUIDrawContext DT, MGTextureData? TextureData, Rectangle TargetBounds, float Opacity = 1.0f, Color? Color = null)
            => TryDrawTexture(DT, TextureData, TargetBounds.TopLeft(), TargetBounds.Width, TargetBounds.Height, Opacity, Color);
        public bool TryDrawTexture(IUIDrawContext DT, string Name, Point Position, int? Width, int? Height, float Opacity = 1.0f, Color? Color = null)
        {
            if (TryGetTexture(Name, out MGTextureData TextureData))
            {
                return TryDrawTexture(DT, TextureData, Position, Width, Height, Opacity, Color);
            }
            else
            {
                return false;
            }
        }
        public bool TryDrawTexture(IUIDrawContext DT, MGTextureData? TextureData, Point Position, int? Width, int? Height, float Opacity = 1.0f, Color? Color = null)
        {
            if (TextureData != null)
            {
                int ActualWidth = Width ?? TextureData.Value.RenderSize.Width;
                int ActualHeight = Height ?? TextureData.Value.RenderSize.Height;
                Rectangle Destination = new(Position.X, Position.Y, ActualWidth, ActualHeight);

                TextureData.Value.Draw(DT, Destination, Color, Opacity);

                return true;
            }
            else
            {
                return false;
            }
        }

        public event EventHandler<(string Name, MGTextureData Data)> OnTextureAdded;
        public event EventHandler<(string Name, MGTextureData Data)> OnTextureRemoved;
        #endregion Textures

        #region Commands
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, Action<MGElement>> _Commands = new();
        /// <summary>This dictionary is commonly used by <see cref="MGButton.CommandName"/> or by <see cref="MGTextBlock"/> to reference delegates by a string key value.<para/>
        /// See also:<br/><see cref="AddCommand(string, Action{MGElement})"/><br/><see cref="RemoveComand(string)"/><para/>
        /// EX: If you create an <see cref="MGTextBlock"/> and set its text to:
        /// <code>[Command=ABC]This text invokes a delegate when clicked[/Command] but this text doesn't</code>
        /// then the <see cref="Action{MGElement}"/> with the name "ABC" will be invoked when clicking the substring "This text invokes a delegate when clicked"</summary>
        public IReadOnlyDictionary<string, Action<MGElement>> Commands => _Commands;

        /// <param name="Name">Must be unique. If the command is intended to be window-specific, 
        /// you may wish to prefix the command name with the <see cref="MGWindow"/>'s <see cref="MGElement.UniqueId"/> to ensure uniqueness.</param>
        public void AddCommand(string Name, Action<MGElement> Command)
        {
            _Commands.Add(Name, Command);
            OnCommandAdded?.Invoke(this, (Name, Command));
        }

        public bool RemoveComand(string Name)
        {
            if (_Commands.TryGetValue(Name, out Action<MGElement> Command))
            {
                _Commands.Remove(Name);
                OnCommandRemoved?.Invoke(this, (Name, Command));
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryGetCommand(string Name, out Action<MGElement> Command)
        {
            if (Name != null && _Commands.TryGetValue(Name, out Command))
            {
                return true;
            }
            else if (Parent?.TryGetCommand(Name, out Command) == true)
            {
                return true;
            }
            else
            {
                Command = null;
                return false;
            }
        }

        /// <summary>Invoked when a new value is added to <see cref="Commands"/> via <see cref="AddCommand(string, Action{MGElement})"/><para/>
        /// See also: <see cref="OnCommandRemoved"/></summary>
        public event EventHandler<(string Name, Action<MGElement> Command)> OnCommandAdded;
        /// <summary>Invoked when a value is removed from <see cref="Commands"/> via <see cref="RemoveComand(string)"/><para/>
        /// See also: <see cref="OnCommandAdded"/></summary>
        public event EventHandler<(string Name, Action<MGElement> Command)> OnCommandRemoved;

        #endregion Commands

        #region Named ToolTips
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, MGToolTip> _NamedToolTips = new();
        /// <summary>A lookup of named <see cref="MGToolTip"/>s that can be referenced by name in <see cref="MGTextBlock"/> inline text markup.<para/>
        /// See also:<br/><see cref="AddNamedToolTip(string, MGToolTip)"/><br/><see cref="RemoveNamedToolTip(string)"/><para/>
        /// EX: If you create an <see cref="MGTextBlock"/> and set its text to:
        /// <code>[ToolTip=ABC]This text has a ToolTip[/ToolTip] but this text doesn't</code>
        /// then the ToolTip with the name "ABC" will be shown when hovering over the substring.</summary>
        public IReadOnlyDictionary<string, MGToolTip> NamedToolTips => _NamedToolTips;

        public void AddNamedToolTip(string Name, MGToolTip ToolTip) => _NamedToolTips.Add(Name, ToolTip);
        public bool RemoveNamedToolTip(string Name) => _NamedToolTips.Remove(Name);

        public bool TryGetNamedToolTip(string Name, out MGToolTip ToolTip)
        {
            if (Name != null && _NamedToolTips.TryGetValue(Name, out ToolTip))
            {
                return true;
            }

            if (Parent?.TryGetNamedToolTip(Name, out ToolTip) == true)
            {
                return true;
            }

            ToolTip = null;
            return false;
        }
        #endregion Named ToolTips

        #region Themes
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, MGTheme> _Themes = new();
        public IReadOnlyDictionary<string, MGTheme> Themes => _Themes;

        public void AddTheme(string Name, MGTheme Theme)
        {
            _Themes.Add(Name, Theme);
            OnThemeAdded?.Invoke(this, (Name, Theme));
        }

        public bool RemoveTheme(string Name)
        {
            if (_Themes.TryGetValue(Name, out MGTheme Theme))
            {
                _Themes.Remove(Name);
                OnThemeRemoved?.Invoke(this, (Name, Theme));
                return true;
            }
            else
            {
                return false;
            }
        }

        public IReadOnlyDictionary<string, MGTheme> LoadThemesFromXaml(XamlDocumentSource Source,
            string DefaultFontFamily = null, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => ThemeDefinitionLoader.LoadAndRegister(this, Source, DefaultFontFamily, SanitizeXAMLString, ReplaceLinebreakLiterals);

        public IReadOnlyDictionary<string, MGTheme> LoadThemesFromXaml(XamlDocumentSource Source, string DefaultFontFamily,
            XamlLoaderMode Mode, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => ThemeDefinitionLoader.LoadAndRegister(this, Source, DefaultFontFamily, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);

        public IReadOnlyDictionary<string, MGControlTemplate> LoadControlTemplatesFromXaml(XamlDocumentSource Source,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => ControlTemplateLoader.LoadAndRegister(this, Source, SanitizeXAMLString, ReplaceLinebreakLiterals);

        public IReadOnlyDictionary<string, MGControlTemplate> LoadControlTemplatesFromXaml(XamlDocumentSource Source,
            XamlLoaderMode Mode, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => ControlTemplateLoader.LoadAndRegister(this, Source, Mode, SanitizeXAMLString, ReplaceLinebreakLiterals);

        /// <param name="DefaultValue">The default theme to return if there is no theme with the given <paramref name="Name"/>. Uses <see cref="DefaultTheme"/> if null.</param>
        /// <param name="WarnIfNotFound">If a <paramref name="Name"/> is specified but no corresponding theme is found, a warning will be written via <see cref="Debug.WriteLine(string?)"/></param>
        public MGTheme GetThemeOrDefault(string Name, MGTheme DefaultValue = null, bool WarnIfNotFound = true)
        {
            if (!string.IsNullOrEmpty(Name))
            {
                if (_Themes.TryGetValue(Name, out MGTheme Result))
                {
                    return Result;
                }
                else if (Parent != null)
                {
                    MGTheme ParentTheme = Parent.GetThemeOrDefault(Name, null, WarnIfNotFound: false);
                    if (ParentTheme != null)
                    {
                        return ParentTheme;
                    }
                }
                else if (WarnIfNotFound)
                {
                    Debug.WriteLine($"Warning - No {nameof(MGTheme)} was found with the name '{Name}' in {nameof(MGResources)}.{nameof(Themes)}");
                }
            }

            return DefaultValue ?? DefaultTheme;
        }

        public bool TryGetTheme(string Name, out MGTheme Theme)
        {
            if (Name != null && _Themes.TryGetValue(Name, out Theme))
            {
                return true;
            }

            if (Parent?.TryGetTheme(Name, out Theme) == true)
            {
                return true;
            }

            Theme = null;
            return false;
        }

        public event EventHandler<(string Name, MGTheme Theme)> OnThemeAdded;
        public event EventHandler<(string Name, MGTheme Theme)> OnThemeRemoved;
        public event EventHandler<(MGTheme PreviousTheme, MGTheme Theme)> OnDefaultThemeChanged;

        private MGTheme _DefaultTheme;
        public bool HasLocalDefaultTheme => _DefaultTheme != null;

        /// <summary>The effective default theme for this scope. If no local override exists, inherits from <see cref="Parent"/>.</summary>
        public MGTheme DefaultTheme
        {
            get => _DefaultTheme ?? Parent?.DefaultTheme ?? throw new InvalidOperationException($"{nameof(MGResources)} must define a {nameof(DefaultTheme)} or inherit one from its parent scope.");
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(DefaultTheme));
                }

                MGTheme PreviousTheme = DefaultTheme;
                if (!ReferenceEquals(_DefaultTheme, value))
                {
                    _DefaultTheme = value;
                    MGTheme CurrentTheme = DefaultTheme;
                    if (!ReferenceEquals(PreviousTheme, CurrentTheme))
                    {
                        OnDefaultThemeChanged?.Invoke(this, (PreviousTheme, CurrentTheme));
                    }
                }
            }
        }

        public void ClearDefaultThemeOverride()
        {
            if (_DefaultTheme != null)
            {
                MGTheme PreviousTheme = DefaultTheme;
                _DefaultTheme = null;
                MGTheme CurrentTheme = DefaultTheme;
                if (!ReferenceEquals(PreviousTheme, CurrentTheme))
                {
                    OnDefaultThemeChanged?.Invoke(this, (PreviousTheme, CurrentTheme));
                }
            }
        }
        #endregion Themes

        #region Styles
        #region Implicit Styles
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<MGElementType, Style> _ImplicitStyles = new();
        /// <summary>Desktop-level styles that are automatically applied to every element of their <see cref="Style.TargetType"/>
        /// when XAML is processed, before any inline element styles.<para/>
        /// Unlike <see cref="ExplicitStyles"/>, implicit styles have no <see cref="Style.Name"/> and are keyed by <see cref="MGElementType"/> only.<para/>
        /// See also: <see cref="AddImplicitStyle(Style)"/>, <see cref="RemoveImplicitStyle(MGElementType)"/></summary>
        public IReadOnlyDictionary<MGElementType, Style> ImplicitStyles => _ImplicitStyles;

        /// <summary>Registers an implicit style for the given element type.
        /// If a style for the same <see cref="Style.TargetType"/> already exists, new setters are merged in (overwriting existing setters for the same property).</summary>
        /// <exception cref="InvalidOperationException">Thrown if the style has a <see cref="Style.Name"/> (implicit styles must be anonymous).</exception>
        public void AddImplicitStyle(Style Style)
        {
            if (Style == null)
            {
                throw new ArgumentNullException(nameof(Style));
            }

            if (!Style.Setters.Any())
            {
                return;
            }

            if (Style.Name != null)
            {
                throw new InvalidOperationException("Implicit styles must not have a Name.");
            }

            MGElementType Type = Style.TargetType;
            if (!_ImplicitStyles.TryGetValue(Type, out Style Existing))
            {
                _ImplicitStyles.Add(Type, Style);
            }
            else
            {
                foreach (Setter Setter in Style.Setters)
                {
                    int ExistingIndex = Existing.Setters.FindIndex(s => s.Property == Setter.Property);
                    if (ExistingIndex >= 0)
                    {
                        Existing.Setters[ExistingIndex] = Setter;
                    }
                    else
                    {
                        Existing.Setters.Add(Setter);
                    }
                }
            }
        }

        /// <summary>Removes the implicit style for the given element type, if one exists.</summary>
        public bool RemoveImplicitStyle(MGElementType TargetType) => _ImplicitStyles.Remove(TargetType);
        #endregion Implicit Styles

        #region Explicit Styles
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, Style> _Styles = new();
        /// <summary>Named (explicit) styles that can be referenced by <see cref="MGUI.Core.UI.XAML.Element.StyleNames"/>.<para/>
        /// See also: <see cref="ImplicitStyles"/> for anonymous type-based desktop-level styles.</summary>
        public IReadOnlyDictionary<string, Style> Styles => _Styles;

        public bool TryGetStyle(string Name, out Style Style)
        {
            if (Name != null && _Styles.TryGetValue(Name, out Style))
            {
                return true;
            }

            if (Parent?.TryGetStyle(Name, out Style) == true)
            {
                return true;
            }

            Style = null;
            return false;
        }

        public IReadOnlyDictionary<MGElementType, Style> GetMergedImplicitStyles()
        {
            Dictionary<MGElementType, Style> Result = Parent?.GetMergedImplicitStyles().ToDictionary(x => x.Key, x => x.Value) ?? new();
            foreach (KeyValuePair<MGElementType, Style> KVP in _ImplicitStyles)
            {
                Result[KVP.Key] = KVP.Value;
            }

            return Result;
        }

        public void AddStyle(string Name, Style Style)
        {
            _Styles.Add(Name, Style);
            OnStyleAdded?.Invoke(this, (Name, Style));
        }

        public bool RemoveStyle(string Name)
        {
            if (_Styles.TryGetValue(Name, out Style Style))
            {
                _Styles.Remove(Name);
                OnStyleRemoved?.Invoke(this, (Name, Style));
                return true;
            }
            else
            {
                return false;
            }
        }

        public event EventHandler<(string Name, Style Style)> OnStyleAdded;
        public event EventHandler<(string Name, Style Style)> OnStyleRemoved;
        #endregion Explicit Styles
        #endregion Styles

        #region StaticResources
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, object> _StaticResources = new();
        public IReadOnlyDictionary<string, object> StaticResources => _StaticResources;

        public void AddStaticResource(string Name, object Value)
        {
            _StaticResources.Add(Name, Value);
            OnStaticResourceAdded?.Invoke(this, (Name, Value));
        }

        public void SetStaticResource(string Name, object Value)
        {
            if (_StaticResources.TryGetValue(Name, out object PreviousValue))
            {
                _StaticResources[Name] = Value;
                OnStaticResourceChanged?.Invoke(this, (Name, PreviousValue, Value));
            }
            else
            {
                _StaticResources.Add(Name, Value);
                OnStaticResourceAdded?.Invoke(this, (Name, Value));
                OnStaticResourceChanged?.Invoke(this, (Name, null, Value));
            }
        }

        public bool RemoveStaticResource(string Name)
        {
            if (_StaticResources.TryGetValue(Name, out object Value))
            {
                _StaticResources.Remove(Name);
                OnStaticResourceRemoved?.Invoke(this, (Name, Value));
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryGetStaticResource(string Name, out object Value)
        {
            if (Name != null && _StaticResources.TryGetValue(Name, out Value))
            {
                return true;
            }
            else if (Parent?.TryGetStaticResource(Name, out Value) == true)
            {
                return true;
            }
            else
            {
                Value = null;
                return false;
            }
        }

        public event EventHandler<(string Name, object Value)> OnStaticResourceAdded;
        public event EventHandler<(string Name, object PreviousValue, object Value)> OnStaticResourceChanged;
        public event EventHandler<(string Name, object Value)> OnStaticResourceRemoved;
        #endregion StaticResources

        #region Control Templates
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, MGControlTemplate> _ControlTemplates = new();
        public IReadOnlyDictionary<string, MGControlTemplate> ControlTemplates => _ControlTemplates;

        public void AddControlTemplate(MGControlTemplate Template)
        {
            _ControlTemplates.Add(Template.Name, Template);
            OnControlTemplateAdded?.Invoke(this, (Template.Name, Template));
        }

        public bool RemoveControlTemplate(string Name)
        {
            if (_ControlTemplates.TryGetValue(Name, out MGControlTemplate Template))
            {
                _ControlTemplates.Remove(Name);
                OnControlTemplateRemoved?.Invoke(this, (Name, Template));
                return true;
            }

            return false;
        }

        public bool TryGetControlTemplate(string Name, out MGControlTemplate Template)
        {
            if (Name != null && _ControlTemplates.TryGetValue(Name, out Template))
            {
                return true;
            }
            else if (Parent?.TryGetControlTemplate(Name, out Template) == true)
            {
                return true;
            }
            else
            {
                Template = null;
                return false;
            }
        }

        public event EventHandler<(string Name, MGControlTemplate Template)> OnControlTemplateAdded;
        public event EventHandler<(string Name, MGControlTemplate Template)> OnControlTemplateRemoved;
        #endregion Control Templates

        #region Element Templates
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<string, MGElementTemplate> _ElementTemplates = new();
        public IReadOnlyDictionary<string, MGElementTemplate> ElementTemplates => _ElementTemplates;

        public void AddElementTemplate(MGElementTemplate Template)
        {
            _ElementTemplates.Add(Template.Name, Template);
            OnElementTemplateAdded?.Invoke(this, (Template.Name, Template));
        }

        public bool RemoveElementTemplate(string Name)
        {
            if (_ElementTemplates.TryGetValue(Name, out MGElementTemplate Template))
            {
                _ElementTemplates.Remove(Name);
                OnElementTemplateRemoved?.Invoke(this, (Name, Template));
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TryGetElementTemplate(string Name, out MGElementTemplate Template)
        {
            if (Name != null && _ElementTemplates.TryGetValue(Name, out Template))
            {
                return true;
            }
            else if (Parent?.TryGetElementTemplate(Name, out Template) == true)
            {
                return true;
            }
            else
            {
                Template = null;
                return false;
            }
        }

        public event EventHandler<(string Name, MGElementTemplate Template)> OnElementTemplateAdded;
        public event EventHandler<(string Name, MGElementTemplate Template)> OnElementTemplateRemoved;
        #endregion Element Templates
    }
}
