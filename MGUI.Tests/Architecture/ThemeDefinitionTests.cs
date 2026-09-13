using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

public class ThemeDefinitionTests
{
  private sealed class TemplateResolutionStub
  {
    public MGElementType ElementType { get; init; }
    public Type ControlRuntimeType { get; init; }
    public string? ControlTemplateName { get; set; }
    public string? DefaultControlTemplateName { get; set; }
    public MGResources Resources { get; }

    public TemplateResolutionStub(MGResources resources, MGElementType elementType, Type? controlRuntimeType = null)
    {
      Resources = resources;
      ElementType = elementType;
      ControlRuntimeType = controlRuntimeType ?? typeof(MGElement);
    }

    public string? ResolveControlTemplateName()
    {
      if (!string.IsNullOrWhiteSpace(ControlTemplateName))
      {
        return ControlTemplateName;
      }

      MGTheme theme = Resources.DefaultTheme;
      if (theme != null)
      {
        if (theme.TryGetControlTemplateMapping(ControlRuntimeType, out string runtimeTypeTemplateName)
          && !string.IsNullOrWhiteSpace(runtimeTypeTemplateName))
        {
          return runtimeTypeTemplateName;
        }

        if (theme.TryGetControlTemplateMapping(ElementType, out string themeTemplateName)
          && !string.IsNullOrWhiteSpace(themeTemplateName))
        {
          return themeTemplateName;
        }
      }

      return DefaultControlTemplateName;
    }
  }

    [Fact]
    public void ThemeDefinition_Can_Be_Parsed_From_Xaml()
    {
        string xaml = @"
<ThemeDefinition xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""TestTheme""
                 BasedOn=""BaseTheme"">
  <ThemeDefinition.ControlTemplates>
    <ThemeControlTemplateDefinition ElementType=""ComboBox"" TemplateName=""ComboBox.Fancy"" />
    <ThemeControlTemplateDefinition ControlTypeName=""MGWindow"" TemplateName=""Window.Fancy"" />
  </ThemeDefinition.ControlTemplates>
  <ThemeDefinition.FontSettings>
    <ThemeFontSettingsDefinition DefaultFontSize=""17"" />
  </ThemeDefinition.FontSettings>
  <ThemeDefinition.Properties>
    <ThemePropertyDefinition Target=""DropdownArrowColor"" Color=""Red"" />
  </ThemeDefinition.Properties>
</ThemeDefinition>
";

        ThemeDefinition definition = XAMLParser.ParseObjectDefinition<ThemeDefinition>(XamlDocumentSource.FromString(xaml));

        Assert.Equal("TestTheme", definition.Name);
        Assert.Equal("BaseTheme", definition.BasedOn);
        Assert.Equal(2, definition.ControlTemplates.Count);
        Assert.Equal(MGElementType.ComboBox, definition.ControlTemplates[0].ElementType);
        Assert.Equal("ComboBox.Fancy", definition.ControlTemplates[0].TemplateName);
        Assert.Equal("MGWindow", definition.ControlTemplates[1].ControlTypeName);
        Assert.Equal("Window.Fancy", definition.ControlTemplates[1].TemplateName);
        Assert.Equal(17, definition.FontSettings.DefaultFontSize);
        Assert.Single(definition.Properties);
        Assert.Equal(ThemePropertyTarget.DropdownArrowColor, definition.Properties[0].Target);
    }

    [Fact]
    public void ThemeDefinitionBuilder_Applies_Partial_Overrides_Over_Base_Theme()
    {
        MGTheme baseTheme = new(MGTheme.BuiltInTheme.Dark_Blue, "Arial");
        ThemeDefinition definition = new()
        {
            Name = "OverlayTheme",
          ControlTemplates = new()
          {
            new ThemeControlTemplateDefinition
            {
              ElementType = MGElementType.ComboBox,
              TemplateName = "ComboBox.Fancy"
            },
            new ThemeControlTemplateDefinition
            {
              ElementType = MGElementType.TabControl,
              TemplateName = "TabControl.Minimal"
            },
            new ThemeControlTemplateDefinition
            {
              ControlTypeName = nameof(MGWindow),
              TemplateName = "Window.Fancy"
            }
          },
            FontSettings = new ThemeFontSettingsDefinition { DefaultFontSize = 18 },
            Properties = new()
            {
                new ThemePropertyDefinition
                {
                    Target = ThemePropertyTarget.DropdownArrowColor,
                    Color = new XAMLColor(255, 0, 0, 255)
                },
                new ThemePropertyDefinition
                {
                    Target = ThemePropertyTarget.ToolTipOffset,
                    Point = new ThemePointDefinition { X = 12, Y = 18 }
                }
            }
        };

        MGTheme built = ThemeDefinitionBuilder.Build(definition, "Arial", baseTheme);

        Assert.Equal(18, built.FontSettings.DefaultFontSize);
        Assert.Equal(Color.Red, built.DropdownArrowColor);
        Assert.Equal(new Point(12, 18), built.ToolTipOffset);
    Assert.True(built.TryGetControlTemplateMapping(MGElementType.ComboBox, out string comboBoxTemplate));
    Assert.Equal("ComboBox.Fancy", comboBoxTemplate);
    Assert.True(built.TryGetControlTemplateMapping(MGElementType.TabControl, out string tabControlTemplate));
    Assert.Equal("TabControl.Minimal", tabControlTemplate);
    Assert.True(built.TryGetControlTemplateMapping(typeof(MGWindow), out string windowTemplate));
    Assert.Equal("Window.Fancy", windowTemplate);
        Assert.Equal(baseTheme.Window.Padding, built.Window.Padding);
    }

  [Fact]
  public void ThemeDefinitionBuilder_Merges_Control_Template_Mappings_Over_Base_Theme()
  {
    MGTheme baseTheme = MGTheme.CreateEmpty("Arial");
    baseTheme.SetControlTemplateMapping(MGElementType.ComboBox, "ComboBox.Base");
    baseTheme.SetControlTemplateMapping(MGElementType.TextBox, "TextBox.Base");
    baseTheme.SetControlTemplateMapping(typeof(MGWindow), "Window.Base");

    ThemeDefinition definition = new()
    {
      Name = "DerivedTheme",
      ControlTemplates = new()
      {
        new ThemeControlTemplateDefinition
        {
          ElementType = MGElementType.ComboBox,
          TemplateName = "ComboBox.Derived"
        },
        new ThemeControlTemplateDefinition
        {
          ElementType = MGElementType.TabControl,
          TemplateName = "TabControl.Derived"
        },
        new ThemeControlTemplateDefinition
        {
          ControlTypeName = nameof(MGWindow),
          TemplateName = "Window.Derived"
        }
      }
    };

    MGTheme built = ThemeDefinitionBuilder.Build(definition, "Arial", baseTheme);

    Assert.True(built.TryGetControlTemplateMapping(MGElementType.ComboBox, out string comboBoxTemplate));
    Assert.Equal("ComboBox.Derived", comboBoxTemplate);
    Assert.True(built.TryGetControlTemplateMapping(MGElementType.TextBox, out string textBoxTemplate));
    Assert.Equal("TextBox.Base", textBoxTemplate);
    Assert.True(built.TryGetControlTemplateMapping(MGElementType.TabControl, out string tabControlTemplate));
    Assert.Equal("TabControl.Derived", tabControlTemplate);
    Assert.True(built.TryGetControlTemplateMapping(typeof(MGWindow), out string windowTemplate));
    Assert.Equal("Window.Derived", windowTemplate);
  }

  [Fact]
  public void ThemeDefinitionBuilder_Seeds_Unspecified_Text_Fallback_States_From_Normal_Value()
  {
    MGTheme baseTheme = MGTheme.CreateEmpty("Arial");
    ThemeDefinition definition = new()
    {
      Name = "DerivedTheme",
      Properties = new()
      {
        new ThemePropertyDefinition
        {
          Target = ThemePropertyTarget.TextBlockFallbackForeground,
          VisualStateColorBrush = new ThemeVisualStateColorBrushDefinition
          {
            NormalValue = new XAMLColor(210, 210, 210, 255),
            DisabledValue = new XAMLColor(110, 110, 110, 255)
          }
        }
      }
    };

    MGTheme built = ThemeDefinitionBuilder.Build(definition, "Arial", baseTheme);
    VisualStateColorBrush foreground = built.TextBlockFallbackForeground.GetValue(true);

    Assert.NotNull(foreground);
    Assert.Equal(new Color(210, 210, 210, 255), foreground.NormalValue);
    Assert.Equal(new Color(210, 210, 210, 255), foreground.SelectedValue);
    Assert.Equal(new Color(210, 210, 210, 255), foreground.FocusedValue);
    Assert.Equal(new Color(110, 110, 110, 255), foreground.DisabledValue);
  }

    [Fact]
    public void ThemeDefinitionLoader_Resolves_BasedOn_From_Same_Document_And_Resources()
    {
        MGResources resources = new(new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, "Arial"));
        resources.AddTheme("BaseTheme", new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, "Arial"));

        string xaml = @"
<ThemeDefinitionsDocument xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"">
  <ThemeDefinition Name=""ChildTheme"" BasedOn=""BaseTheme"">
    <ThemeDefinition.Properties>
      <ThemePropertyDefinition Target=""DropdownArrowColor"" Color=""Black"" />
    </ThemeDefinition.Properties>
  </ThemeDefinition>
  <ThemeDefinition Name=""GrandChildTheme"" BasedOn=""ChildTheme"">
    <ThemeDefinition.FontSettings>
      <ThemeFontSettingsDefinition DefaultFontSize=""19"" />
    </ThemeDefinition.FontSettings>
  </ThemeDefinition>
</ThemeDefinitionsDocument>
";

        IReadOnlyDictionary<string, MGTheme> themes = resources.LoadThemesFromXaml(XamlDocumentSource.FromString(xaml));

        Assert.Equal(2, themes.Count);
        Assert.Equal(Color.Black, themes["ChildTheme"].DropdownArrowColor);
        Assert.Equal(19, themes["GrandChildTheme"].FontSettings.DefaultFontSize);
        Assert.Equal(Color.Black, themes["GrandChildTheme"].DropdownArrowColor);
        Assert.Same(themes["GrandChildTheme"], resources.GetThemeOrDefault("GrandChildTheme", null, false));
    }

    [Fact]
    public void ThemeDefinitionLoader_Resolves_BuiltIn_Base_Themes()
    {
        MGResources resources = new(MGTheme.CreateEmpty("Arial"));

        string xaml = @"
<ThemeDefinition xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""DerivedTheme""
                 BasedOn=""Dark_Blue"">
  <ThemeDefinition.FontSettings>
    <ThemeFontSettingsDefinition DefaultFontSize=""15"" />
  </ThemeDefinition.FontSettings>
</ThemeDefinition>
";

        IReadOnlyDictionary<string, MGTheme> themes = resources.LoadThemesFromXaml(XamlDocumentSource.FromString(xaml));

        Assert.Equal(15, themes["DerivedTheme"].FontSettings.DefaultFontSize);
        Assert.Equal(Color.White, themes["DerivedTheme"].DropdownArrowColor);
    }

      [Fact]
      public void ThemeDefinitionLoader_Loads_Control_Template_Mappings_From_Xaml_Document()
      {
        MGResources resources = new(MGTheme.CreateEmpty("Arial"));

        string xaml = @"
    <ThemeDefinitionsDocument xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"">
      <ThemeDefinition Name=""BaseTheme"">
      <ThemeDefinition.ControlTemplates>
        <ThemeControlTemplateDefinition ElementType=""ComboBox"" TemplateName=""ComboBox.Base"" />
        <ThemeControlTemplateDefinition ElementType=""TextBox"" TemplateName=""TextBox.Base"" />
        <ThemeControlTemplateDefinition ControlTypeName=""MGWindow"" TemplateName=""Window.Base"" />
      </ThemeDefinition.ControlTemplates>
      </ThemeDefinition>
      <ThemeDefinition Name=""DerivedTheme"" BasedOn=""BaseTheme"">
      <ThemeDefinition.ControlTemplates>
        <ThemeControlTemplateDefinition ElementType=""ComboBox"" TemplateName=""ComboBox.Derived"" />
        <ThemeControlTemplateDefinition ElementType=""TabControl"" TemplateName=""TabControl.Derived"" />
        <ThemeControlTemplateDefinition ControlTypeName=""MGOverlay"" TemplateName=""Overlay.Derived"" />
      </ThemeDefinition.ControlTemplates>
      </ThemeDefinition>
    </ThemeDefinitionsDocument>
    ";

        IReadOnlyDictionary<string, MGTheme> themes = resources.LoadThemesFromXaml(XamlDocumentSource.FromString(xaml));

        Assert.True(themes["DerivedTheme"].TryGetControlTemplateMapping(MGElementType.ComboBox, out string comboBoxTemplate));
        Assert.Equal("ComboBox.Derived", comboBoxTemplate);
        Assert.True(themes["DerivedTheme"].TryGetControlTemplateMapping(MGElementType.TextBox, out string textBoxTemplate));
        Assert.Equal("TextBox.Base", textBoxTemplate);
        Assert.True(themes["DerivedTheme"].TryGetControlTemplateMapping(MGElementType.TabControl, out string tabControlTemplate));
        Assert.Equal("TabControl.Derived", tabControlTemplate);
        Assert.True(themes["DerivedTheme"].TryGetControlTemplateMapping(typeof(MGWindow), out string windowTemplate));
        Assert.Equal("Window.Base", windowTemplate);
        Assert.True(themes["DerivedTheme"].TryGetControlTemplateMapping(typeof(MGOverlay), out string overlayTemplate));
        Assert.Equal("Overlay.Derived", overlayTemplate);
      }

      [Fact]
      public void Theme_Template_Resolution_Uses_Theme_When_No_Local_Override_Is_Set()
      {
        MGTheme theme = MGTheme.CreateEmpty("Arial");
        theme.SetControlTemplateMapping(MGElementType.ComboBox, "ComboBox.Themed");
        MGResources resources = new(theme);
        TemplateResolutionStub stub = new(resources, MGElementType.ComboBox)
        {
          DefaultControlTemplateName = "ComboBox.Default"
        };

        Assert.Equal("ComboBox.Themed", stub.ResolveControlTemplateName());
      }

      [Fact]
      public void Theme_Template_Resolution_Preserves_Local_Override_Over_Theme()
      {
        MGTheme theme = MGTheme.CreateEmpty("Arial");
        theme.SetControlTemplateMapping(MGElementType.ComboBox, "ComboBox.Themed");
        MGResources resources = new(theme);
        TemplateResolutionStub stub = new(resources, MGElementType.ComboBox)
        {
          ControlTemplateName = "ComboBox.Local",
          DefaultControlTemplateName = "ComboBox.Default"
        };

        Assert.Equal("ComboBox.Local", stub.ResolveControlTemplateName());
      }

      [Fact]
      public void Theme_Template_Resolution_Prefers_Runtime_Control_Type_Over_Element_Type()
      {
        MGTheme theme = MGTheme.CreateEmpty("Arial");
        theme.SetControlTemplateMapping(MGElementType.Window, "Window.ByElementType");
        theme.SetControlTemplateMapping(typeof(MGWindow), "Window.ByRuntimeType");
        MGResources resources = new(theme);
        TemplateResolutionStub stub = new(resources, MGElementType.Window, typeof(MGWindow))
        {
          DefaultControlTemplateName = "Window.Default"
        };

        Assert.Equal("Window.ByRuntimeType", stub.ResolveControlTemplateName());
      }

      [Fact]
      public void Theme_Template_Resolution_Tracks_Parent_And_Child_Theme_Scopes()
      {
        MGTheme parentTheme = MGTheme.CreateEmpty("Arial");
        parentTheme.SetControlTemplateMapping(MGElementType.ComboBox, "ComboBox.Parent");
        MGTheme childTheme = MGTheme.CreateEmpty("Arial");
        childTheme.SetControlTemplateMapping(MGElementType.ComboBox, "ComboBox.Child");

        MGResources parentResources = new(parentTheme);
        MGResources childResources = new(parentResources, UIResourceScope.Subtree);
        TemplateResolutionStub stub = new(childResources, MGElementType.ComboBox)
        {
          DefaultControlTemplateName = "ComboBox.Default"
        };

        Assert.Equal("ComboBox.Parent", stub.ResolveControlTemplateName());

        childResources.DefaultTheme = childTheme;

        Assert.Equal("ComboBox.Child", stub.ResolveControlTemplateName());
      }

      [Fact]
      public void Theme_Template_Resolution_Updates_When_Runtime_Theme_Changes()
      {
        MGTheme theme1 = MGTheme.CreateEmpty("Arial");
        theme1.SetControlTemplateMapping(MGElementType.ComboBox, "ComboBox.Theme1");
        MGTheme theme2 = MGTheme.CreateEmpty("Arial");
        theme2.SetControlTemplateMapping(MGElementType.ComboBox, "ComboBox.Theme2");

        MGResources resources = new(theme1);
        TemplateResolutionStub stub = new(resources, MGElementType.ComboBox)
        {
          DefaultControlTemplateName = "ComboBox.Default"
        };

        Assert.Equal("ComboBox.Theme1", stub.ResolveControlTemplateName());

        resources.DefaultTheme = theme2;

        Assert.Equal("ComboBox.Theme2", stub.ResolveControlTemplateName());
      }

    [Fact]
    public void ThemeDefinitionLoader_Rejects_Cycles()
    {
        ThemeDefinition first = new() { Name = "A", BasedOn = "B" };
        ThemeDefinition second = new() { Name = "B", BasedOn = "A" };

        Assert.Throws<InvalidOperationException>(() =>
            ThemeDefinitionLoader.BuildThemes(new[] { first, second }, _ => null, "Arial"));
    }

      [Fact]
      public void BuiltInTheme_Dark_Can_Be_Created()
      {
        MGTheme theme = new(MGTheme.BuiltInTheme.Dark, "Arial");

        Assert.Equal(new Color(30, 30, 30), ((MGSolidFillBrush)theme.GetBackgroundBrush(MGElementType.Window).NormalValue).Color);
        Assert.Equal(new Color(37, 37, 38), ((MGSolidFillBrush)theme.ComboBoxDropdownBackground.GetValue(true).NormalValue).Color);
        Assert.Equal(new Color(210, 210, 210), theme.DropdownArrowColor);
        Assert.Equal(Color.Transparent, theme.Docking.TabActiveAccentColor);
        Assert.Equal(CheckIndicatorStyle.FilledSquare, theme.CheckBoxCheckedIndicatorStyle);
      }

      [Fact]
      public void BuiltInTheme_Dark_Maps_Control_Templates()
      {
        MGTheme theme = new(MGTheme.BuiltInTheme.Dark, "Arial");

        Assert.True(theme.TryGetControlTemplateMapping(MGElementType.Window, out string windowTemplate));
        Assert.Equal("Dark.Window", windowTemplate);
        Assert.True(theme.TryGetControlTemplateMapping(MGElementType.ListBox, out string listBoxTemplate));
        Assert.Equal("Dark.ListBox", listBoxTemplate);
        Assert.True(theme.TryGetControlTemplateMapping(MGElementType.ComboBox, out string comboBoxTemplate));
        Assert.Equal("Dark.ComboBox", comboBoxTemplate);
        Assert.True(theme.TryGetControlTemplateMapping(MGElementType.TabControl, out string tabControlTemplate));
        Assert.Equal("Dark.TabControl", tabControlTemplate);
        Assert.True(theme.TryGetControlTemplateMapping(typeof(MGDockTabItem), out string dockTabTemplate));
        Assert.Equal("Dark.DockTabItem", dockTabTemplate);
      }

      [Fact]
      public void BuiltInTheme_Dark_Does_Not_Replace_Default_Constructor()
      {
        MGTheme defaultTheme = new("Arial");
        MGTheme darkBlueTheme = new(MGTheme.BuiltInTheme.Dark_Blue, "Arial");

        Assert.Equal(((MGSolidFillBrush)darkBlueTheme.GetBackgroundBrush(MGElementType.Window).NormalValue).Color,
          ((MGSolidFillBrush)defaultTheme.GetBackgroundBrush(MGElementType.Window).NormalValue).Color);
        Assert.Equal(darkBlueTheme.DropdownArrowColor, defaultTheme.DropdownArrowColor);
      }
}