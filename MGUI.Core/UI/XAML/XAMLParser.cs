using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Brushes.Border_Brushes;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using MGUI.Shared.Helpers;

#if UseWPF
using System.Xaml;
#else
using Portable.Xaml;
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    public class XAMLParser
    {
        private const string XMLNameSpaceBaseUri = @"http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        public const string XMLLocalNameSpacePrefix = "MGUI";
        public static readonly string XMLLocalNameSpaceUri = $"clr-namespace:{nameof(MGUI)}.{nameof(Core)}.{nameof(UI)}.{nameof(XAML)};assembly={nameof(MGUI)}.{nameof(Core)}";

        private static readonly string XMLNameSpaces =
            $"xmlns=\"{XMLNameSpaceBaseUri}\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:{XMLLocalNameSpacePrefix}=\"{XMLLocalNameSpaceUri}\"";
            //$"xmlns=\"{XMLLocalNameSpaceUri}\" xmlns:x=\"{XMLNameSpaceBaseUri}\""; // This URI avoids using a prefix for the MGUI namespace

        private static readonly Dictionary<string, string> ElementNameAliases = new()
        {
            { "ContentPresenter", nameof(ContentPresenter) },
            { "HeaderedContentPresenter", nameof(HeaderedContentPresenter) },

            { "Border", nameof(Border) },
            { "Button", nameof(Button) },
            { "CheckBox", nameof(CheckBox) },
            { "ComboBox", nameof(ComboBox) },

            { "ContextMenu", nameof(ContextMenu) },
            { "ContextMenuButton", nameof(ContextMenuButton) },
            { "ContextMenuToggle", nameof(ContextMenuToggle) },
            { "ContextMenuSeparator", nameof(ContextMenuSeparator) },
            { "ContextMenuRadioButton", nameof(ContextMenuRadioButton) },

            { "MenuBar", nameof(MenuBar) },
            { "MenuBarItem", nameof(MenuBarItem) },

            { "TreeView", nameof(TreeView) },
            { "TreeViewItem", nameof(TreeViewItem) },

            { "Expander", nameof(Expander) },
            { "GroupBox", nameof(GroupBox) },
            { "Image", nameof(Image) },
            { "ListBox", nameof(ListBox) },
            { "ListView", nameof(ListView) },
            { "ListViewColumn", nameof(ListViewColumn) },

            { "OverlayHost", nameof(OverlayHost) },
            { "Overlay", nameof(Overlay) },

            { "PasswordBox", nameof(PasswordBox) },
            { "ProgressBar", nameof(ProgressBar) },
            { "RadioButton", nameof(RadioButton) },
            { "RatingControl", nameof(RatingControl) },
            { "Rectangle", nameof(Rectangle) },
            { "ResizeGrip", nameof(ResizeGrip) },
            { "ScrollViewer", nameof(ScrollViewer) },
            { "Separator", nameof(Separator) },
            { "Slider", nameof(Slider) },
            { "Spacer", nameof(Spacer) },
            { "Spoiler", nameof(Spoiler) },
            { "Stopwatch", nameof(Stopwatch) },
            { "TabControl", nameof(TabControl) },
            { "TabItem", nameof(TabItem) },
            { "TextBlock", nameof(TextBlock) },
            { "TextBox", nameof(TextBox) },
            { "Timer", nameof(Timer) },
            { "ToggleButton", nameof(ToggleButton) },
            { "ToolTip", nameof(ToolTip) },
            { "Window", nameof(Window) },

            { "RowDefinition", nameof(RowDefinition) },
            { "ColumnDefinition", nameof(ColumnDefinition) },
            { "GridSplitter", nameof(GridSplitter) },
            { "Grid", nameof(Grid) },
            { "UniformGrid", nameof(UniformGrid) },
            { "DockPanel", nameof(DockPanel) },
            { "StackPanel", nameof(StackPanel) },
            { "OverlayPanel", nameof(OverlayPanel) },
            { "ResponsiveRoot", nameof(ResponsiveRoot) },

            { "Style", nameof(Style) },
            { "Setter", nameof(Setter) },
            { "ControlTemplates", nameof(ControlTemplatesDocument) },
            { "ControlTemplate", nameof(ControlTemplateDefinition) },
            { "TemplatePart", nameof(TemplatePartDefinition) },

            //  Abbreviated names
            { "CP", nameof(ContentPresenter) },
            { "HCP", nameof(HeaderedContentPresenter) },
            { "CM", nameof(ContextMenu) },
            { "CMB", nameof(ContextMenuButton) },
            { "CMT", nameof(ContextMenuToggle) },
            { "CMS", nameof(ContextMenuSeparator) },
            { "CMR", nameof(ContextMenuRadioButton) },
            { "MB", nameof(MenuBar) },
            { "MBI", nameof(MenuBarItem) },
            { "GB", nameof(GroupBox) },
            { "LB", nameof(ListBox) },
            { "LV", nameof(ListView) },
            { "LVC", nameof(ListViewColumn) },
            { "RB", nameof(RadioButton) },
            { "RC", nameof(RatingControl) },
            { "RG", nameof(ResizeGrip) },
            { "SV", nameof(ScrollViewer) },
            { "TC", nameof(TabControl) },
            { "TI", nameof(TabItem) },
            { "TB", nameof(TextBlock) },
            { "TT", nameof(ToolTip) },
            { "TV", nameof(TreeView) },
            { "TVI", nameof(TreeViewItem) },
            { "RD", nameof(RowDefinition) },
            { "CD", nameof(ColumnDefinition) },
            { "GS", nameof(GridSplitter) },
            { "UG", nameof(UniformGrid) },
            { "DP", nameof(DockPanel) },
            { "SP", nameof(StackPanel) },
            { "OP", nameof(OverlayPanel) },
            { "RR", nameof(ResponsiveRoot) }
        };

        private static string ValidateXAMLString(string XAMLString)
        {
            XAMLString = XAMLString.Trim();

            //  Check whether the namespace declarations are already present anywhere in the string.
            //  We search the full string (not just the first line) to correctly handle XAML documents
            //  that start with XML comments (<!-- ... -->) or processing instructions before the root element.
            if (!XAMLString.Contains(XMLNameSpaces))
            {
                //  Find the actual root element tag, skipping any XML comments (<!-- ... -->) and
                //  processing instructions (<? ... ?>) that may precede it.
                int rootTagStart = -1;
                int searchPos = 0;
                while (searchPos < XAMLString.Length)
                {
                    int tagStart = XAMLString.IndexOf('<', searchPos);
                    if (tagStart < 0)
                    {
                        break;
                    }

                    if (tagStart + 1 >= XAMLString.Length)
                    {
                        break;
                    }

                    char nextChar = XAMLString[tagStart + 1];
                    if (nextChar != '!' && nextChar != '?')
                    {
                        // This is an element tag, not a comment or PI — this is the root element
                        rootTagStart = tagStart;
                        break;
                    }

                    // Skip past this comment or processing instruction
                    int tagEnd = XAMLString.IndexOf('>', tagStart + 1);
                    if (tagEnd < 0)
                    {
                        break;
                    }

                    searchPos = tagEnd + 1;
                }

                if (rootTagStart >= 0)
                {
                    //  Insert the required xml namespaces into the root element tag
                    string afterOpenTag = XAMLString.Substring(rootTagStart);
                    int SpaceIndex = afterOpenTag.IndexOf(' ');
                    if (SpaceIndex >= 0)
                    {
                        XAMLString = $"{XAMLString.Substring(0, rootTagStart + SpaceIndex)} {XMLNameSpaces} {XAMLString.Substring(rootTagStart + SpaceIndex + 1)}";
                    }
                    else
                    {
                        int InsertionIndex = rootTagStart + afterOpenTag.IndexOf('>');
                        XAMLString = $"{XAMLString.Substring(0, InsertionIndex)} {XMLNameSpaces} {XAMLString.Substring(InsertionIndex)}";
                    }
                }
            }

            //  Replace all element names with their fully-qualified name, such as:
            //  "<Button/>"                 --> "<MGUI:Button/>"
            //  "<Button Content="Foo" />"  --> "<MGUI:Button Content="Foo" />"
            //  "<Button.Content>"          --> "<MGUI:Button.Content>"
            //  Where the "MGUI" XML namespace prefix refers to the XMLLocalNameSpaceUri static string
            XDocument Document = XDocument.Parse(XAMLString);

            foreach (var Element in Document.Descendants())
            {
                string ElementName = Element.Name.LocalName;
                foreach (KeyValuePair<string, string> KVP in ElementNameAliases)
                {
                    if (ElementName.StartsWith(KVP.Key))
                    {
                        Element.Name = XName.Get(ElementName.ReplaceFirstOccurrence(KVP.Key, KVP.Value), XMLLocalNameSpaceUri);
                        break;
                    }
                }
            }

            using (StringWriter SW = new())
            {
                using (XmlWriter XW = XmlWriter.Create(SW, new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true }))
                {
                    Document.WriteTo(XW);
                }

                string Result = SW.ToString();
                return Result;
            }
        }

        /// <param name="SanitizeXAMLString">If true, the markup loaded from <paramref name="Source"/> will be pre-processed via the following logic:<para/>
        /// 1. Trim leading and trailing whitespace<br/>
        /// 2. Insert required XML namespaces (such as "xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation")<br/>
        /// 3. Replace type names with their fully-qualified names, such as "Button" -> "MGUI:Button" where the "MGUI" namespace prefix points to the URI defined by <see cref="XMLLocalNameSpaceUri"/><para/>
        /// If your XAML already contains fully-qualified types, you probably should set this to false.</param>
        /// <param name="ReplaceLinebreakLiterals">If true, the literal string @"\n" will be replaced with "&#38;#x0a;", which is the XAML encoding of the linebreak character '\n'.<br/>
        /// If false, setting the text of an <see cref="MGTextBlock"/> requires encoding the '\n' character as "&#38;#x0a;"<para/>
        /// See also: <see href="https://stackoverflow.com/a/183435/11689514"/></param>
        private static string PrepareMarkup(XamlDocumentSource Source, bool SanitizeXAMLString, bool ReplaceLinebreakLiterals)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }

            string XAMLString = Source.LoadContent();
            if (SanitizeXAMLString)
            {
                XAMLString = ValidateXAMLString(XAMLString);
            }

            if (ReplaceLinebreakLiterals)
            {
                XAMLString = XAMLString.Replace(@"\n", "&#x0a;");
            }

            return XAMLString;
        }

        public static TDefinition ParseDefinition<TDefinition>(XamlDocumentSource Source, MGResources Resources = null,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            where TDefinition : Element
        {
            string XAMLString = PrepareMarkup(Source, SanitizeXAMLString, ReplaceLinebreakLiterals);
            TDefinition Parsed = (TDefinition)XamlServices.Parse(XAMLString);

            if (Resources != null)
            {
                Parsed.ProcessStyles(Resources);
            }

            return Parsed;
        }

        public static TDefinition ParseObjectDefinition<TDefinition>(XamlDocumentSource Source,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        {
            string XAMLString = PrepareMarkup(Source, SanitizeXAMLString, ReplaceLinebreakLiterals);
            return (TDefinition)XamlServices.Parse(XAMLString);
        }

        public static Element ParseElementDefinition(XamlDocumentSource Source, MGResources Resources = null,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => ParseDefinition<Element>(Source, Resources, SanitizeXAMLString, ReplaceLinebreakLiterals);

        public static Window ParseWindowDefinition(XamlDocumentSource Source, MGResources Resources = null,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => ParseDefinition<Window>(Source, Resources, SanitizeXAMLString, ReplaceLinebreakLiterals);

        public static T Load<T>(MGWindow Window, XamlDocumentSource Source, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            where T : MGElement
        {
            Element Parsed = ParseElementDefinition(Source, Window.GetResources(), SanitizeXAMLString, ReplaceLinebreakLiterals);
            return Parsed.ToElement<T>(Window, null);
        }

        public static T Load<T>(MGWindow Window, string XAMLString, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            where T : MGElement
            => Load<T>(Window, XamlDocumentSource.FromString(XAMLString), SanitizeXAMLString, ReplaceLinebreakLiterals);

        public static MGElement LoadPreview(MGWindow Window, XamlDocumentSource Source, object DataContext = null,
            bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        {
            MGElement Result = Load<MGElement>(Window, Source, SanitizeXAMLString, ReplaceLinebreakLiterals);
            Result.DataContextOverride = DataContext;
            return Result;
        }

        /// <param name="SanitizeXAMLString">If true, the markup loaded from <paramref name="Source"/> will be pre-processed via the following logic:<para/>
        /// 1. Trim leading and trailing whitespace<br/>
        /// 2. Insert required XML namespaces (such as "xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation")<br/>
        /// 3. Replace type names with their fully-qualified names, such as "Button" -> "MGUI:Button" where the "MGUI" namespace prefix points to the URI defined by <see cref="XMLLocalNameSpaceUri"/><para/>
        /// If your XAML already contains fully-qualified types, you probably should set this to false.</param>
        /// <param name="ReplaceLinebreakLiterals">If true, the literal string @"\n" will be replaced with "&#38;#x0a;", which is the XAML encoding of the linebreak character '\n'.<br/>
        /// If false, setting the text of an <see cref="MGTextBlock"/> requires encoding the '\n' character as "&#38;#x0a;"<para/>
        /// See also: <see href="https://stackoverflow.com/a/183435/11689514"/></param>
        public static MGWindow LoadRootWindow(MGDesktop Desktop, XamlDocumentSource Source, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        {
            Window Parsed = ParseWindowDefinition(Source, Desktop.Resources, SanitizeXAMLString, ReplaceLinebreakLiterals);
            return Parsed.ToElement(Desktop);
        }

        public static MGWindow LoadRootWindow(MGDesktop Desktop, string XAMLString, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            => LoadRootWindow(Desktop, XamlDocumentSource.FromString(XAMLString), SanitizeXAMLString, ReplaceLinebreakLiterals);
    }
}
