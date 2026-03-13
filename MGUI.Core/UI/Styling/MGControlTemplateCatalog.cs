using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;

namespace MGUI.Core.UI.Styling
{
    public static class MGControlTemplateCatalog
    {
        public const string WindowTemplateName = "Window.Default";
        public const string OverlayTemplateName = "Overlay.Default";
        public const string ContextMenuTemplateName = "ContextMenu.Default";
        public const string ContextMenuItemTemplateName = "ContextMenuItem.Default";

        public static void RegisterDefaults(MGResources Resources)
        {
            if (Resources == null)
            {
                return;
            }

            Register(Resources, WindowTemplateName, ApplyWindowTemplate);
            Register(Resources, OverlayTemplateName, ApplyOverlayTemplate);
            Register(Resources, ContextMenuTemplateName, ApplyContextMenuTemplate);
            Register(Resources, ContextMenuItemTemplateName, ApplyContextMenuItemTemplate);
        }

        private static void Register(MGResources Resources, string Name, Action<MGControlTemplateContext> Apply)
        {
            if (!Resources.TryGetControlTemplate(Name, out _))
            {
                Resources.AddControlTemplate(new(Name, Apply));
            }
        }

        private static void ApplyWindowTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGWindow Window)
            {
                return;
            }

            MGTheme Theme = Window.GetTheme();
            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGWindow.BorderPartName);
            MGDockPanel TitleBar = Context.GetRequiredPart<MGDockPanel>(MGWindow.TitleBarPartName);
            MGTextBlock TitleText = Context.GetRequiredPart<MGTextBlock>(MGWindow.TitleBarTextPartName);
            MGButton CloseButton = Context.GetRequiredPart<MGButton>(MGWindow.CloseButtonPartName);

            Window.Padding = new(5);
            Border.BorderThickness = new(2);
            Border.BorderBrush = MGUniformBorderBrush.Black;

            TitleBar.Padding = new(2);
            TitleBar.MinHeight = 24;
            TitleBar.BackgroundBrush = Theme.TitleBackground.GetValue(true);
            TitleBar.DrawBackgroundEnabled = false;

            CloseButton.MinWidth = 12;
            CloseButton.MinHeight = 12;
            CloseButton.BackgroundBrush = new(Color.Crimson.AsFillBrush() * 0.5f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
            CloseButton.BorderBrush = MGUniformBorderBrush.Black;
            CloseButton.BorderThickness = new(1);
            CloseButton.Margin = new(1, 1, 1, 1 + Border.BorderThickness.Bottom);
            CloseButton.Padding = new(4, -1);
            CloseButton.VerticalAlignment = VerticalAlignment.Center;
            CloseButton.VerticalContentAlignment = VerticalAlignment.Center;
            CloseButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            if (CloseButton.Content == null)
            {
                CloseButton.SetContent(new MGTextBlock(Window, "[b][shadow=Black 1 1]x[/shadow][/b]", Color.White));
            }

            TitleText.Margin = new(4, 0);
            TitleText.Padding = new(0);
            TitleText.HorizontalAlignment = HorizontalAlignment.Stretch;
            TitleText.VerticalAlignment = VerticalAlignment.Center;
            TitleText.TextAlignment = HorizontalAlignment.Left;
            TitleText.DefaultTextForeground = new(Color.White, Color.White, Color.White);
        }

        private static void ApplyOverlayTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGOverlay Overlay)
            {
                return;
            }

            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGOverlay.BorderPartName);
            MGButton CloseButton = Context.GetRequiredPart<MGButton>(MGOverlay.CloseButtonPartName);

            Overlay.Padding = new(5);
            Border.BorderThickness = new(1);
            Border.BorderBrush = MGUniformBorderBrush.Black;

            CloseButton.MinWidth = 12;
            CloseButton.MinHeight = 12;
            CloseButton.BackgroundBrush = new(Color.Crimson.AsFillBrush() * 0.8f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
            CloseButton.BorderBrush = MGUniformBorderBrush.Black;
            CloseButton.BorderThickness = new(1);
            CloseButton.Padding = new(4, -1);
            if (CloseButton.Content == null)
            {
                CloseButton.SetContent(new MGTextBlock(Overlay.Host.ParentWindow, "[b][shadow=Black 1 1]x[/shadow][/b]", Color.White));
            }
        }

        private static void ApplyContextMenuTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGContextMenu Menu)
            {
                return;
            }

            Menu.Padding = new(1);
            Menu.BorderBrush = MGUniformBorderBrush.Gray;
            Menu.BorderThickness = new(1);
        }

        private static void ApplyContextMenuItemTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGWrappedContextMenuItem)
            {
                return;
            }

            MGContentPresenter HeaderPresenter = Context.GetRequiredPart<MGContentPresenter>(MGWrappedContextMenuItem.HeaderPresenterPartName);
            MGTextBlock ShortcutText = Context.GetRequiredPart<MGTextBlock>(MGWrappedContextMenuItem.ShortcutTextPartName);
            MGElement Arrow = Context.GetRequiredPart<MGElement>(MGWrappedContextMenuItem.SubmenuArrowPartName);

            HeaderPresenter.Margin = new(0, 0, 5, 0);
            HeaderPresenter.BackgroundBrush = new(null);
            ShortcutText.Margin = new Thickness(18, 0, 0, 0);
            ShortcutText.Foreground = new(Color.LightGray, Color.LightGray, Color.LightGray);
            Arrow.Margin = new(0, 5, MGWrappedContextMenuItem.DefaultSubmenuArrowRightMargin, 5);
        }
    }
}