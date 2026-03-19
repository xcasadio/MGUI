using Microsoft.Xna.Framework.Content;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using System;
using System.Linq;

namespace MGUI.Samples.Features
{
    public class FocusInputReviewSample : SampleBase
    {
        private readonly MGTextBlock FocusStatusText;
        private readonly MGTextBlock InteractionStatusText;
        private readonly MGTextBlock ContextMenuStatusText;
        private readonly MGTextBlock PopupStatusText;
        private readonly MGTextBox SearchTextBox;
        private readonly MGTextBox ContextMenuTextBox;
        private readonly MGTextBox CoveredTextBox;
        private readonly MGTextBox OverlayTextBox;
        private readonly MGComboBox<string> FilterComboBox;
        private readonly MGListBox<string> ResultsListBox;
        private readonly MGListBox<string> PopupListBox;
        private readonly MGContextMenu ReviewContextMenu;
        private readonly MGOverlay BlockingOverlay;
        private readonly MGWindow PopupWindow;
        private readonly MGTextBox PopupTextBox;

        public FocusInputReviewSample(ContentManager content, MGDesktop desktop)
            : base(content, desktop, "Features", "FocusInputReview.xaml")
        {
            FocusStatusText = Window.GetElementByName<MGTextBlock>("FocusStatusText");
            InteractionStatusText = Window.GetElementByName<MGTextBlock>("InteractionStatusText");
            ContextMenuStatusText = Window.GetElementByName<MGTextBlock>("ContextMenuStatusText");
            PopupStatusText = Window.GetElementByName<MGTextBlock>("PopupStatusText");
            SearchTextBox = Window.GetElementByName<MGTextBox>("SearchTextBox");
            ContextMenuTextBox = Window.GetElementByName<MGTextBox>("ContextMenuTextBox");
            CoveredTextBox = Window.GetElementByName<MGTextBox>("CoveredTextBox");
            OverlayTextBox = Window.GetElementByName<MGTextBox>("OverlayTextBox");
            FilterComboBox = Window.GetElementByName<MGComboBox<string>>("FilterComboBox");
            ResultsListBox = Window.GetElementByName<MGListBox<string>>("ResultsListBox");
            ReviewContextMenu = Window.GetElementByName<MGContextMenu>("ReviewContextMenu");
            BlockingOverlay = Window.GetElementByName<MGOverlay>("BlockingOverlay");

            PopupWindow = CreatePopupWindow();
            PopupTextBox = PopupWindow.GetElementByName<MGTextBox>("PopupTextBox");
            PopupListBox = PopupWindow.GetElementByName<MGListBox<string>>("PopupListBox");

            FilterComboBox.SetItemsSource(new[]
            {
                "All items",
                "Focusable controls",
                "Popup-backed controls",
                "Overlay blockers"
            });
            FilterComboBox.SelectedIndex = 0;

            ResultsListBox.SetItemsSource(new[]
            {
                "SearchTextBox",
                "FilterComboBox",
                "ResultsListBox",
                "ContextMenuTextBox",
                "PopupTextBox",
                "OverlayTextBox"
            });

            PopupListBox.SetItemsSource(new[]
            {
                "Popup item A",
                "Popup item B",
                "Popup item C"
            });

            Desktop.FocusedKeyboardHandlerChanged += (_, __) => UpdateFocusStatus();
            VisibilityChanged += (_, isVisible) =>
            {
                if (isVisible)
                {
                    SearchTextBox.Focus();
                }
                else
                {
                    ResetTransientState();
                }

                UpdateFocusStatus();
            };

            FilterComboBox.SelectedItemChanged += (_, e) => UpdateInteraction($"ComboBox selection changed to {e.NewValue ?? "none"}");
            FilterComboBox.DropdownOpened += (_, __) => UpdateInteraction($"ComboBox dropdown {(FilterComboBox.IsDropdownOpen ? "opened" : "closed")}");
            ResultsListBox.SelectionChanged += (_, selectedItems) => UpdateInteraction($"ListBox selection changed to {selectedItems.FirstOrDefault()?.Data ?? "none"}");
            PopupListBox.SelectionChanged += (_, selectedItems) => UpdateInteraction($"Popup list selection changed to {selectedItems.FirstOrDefault()?.Data ?? "none"}");

            ReviewContextMenu.ItemSelected += (_, e) =>
            {
                ContextMenuStatusText.Text = $"Context menu: selected {e.CommandId}";
                UpdateInteraction($"Context menu command executed: {e.CommandId}");
                ContextMenuTextBox.Focus();
            };

            Window.GetElementByName<MGButton>("OpenPopupButton").AddCommandHandler((_, __) => OpenPopup());
            Window.GetElementByName<MGButton>("BringPopupToFrontButton").AddCommandHandler((_, __) => BringPopupToFront());
            Window.GetElementByName<MGButton>("ClosePopupButton").AddCommandHandler((_, __) => ClosePopup());
            Window.GetElementByName<MGButton>("OpenOverlayButton").AddCommandHandler((_, __) => OpenOverlay());
            Window.GetElementByName<MGButton>("CloseOverlayButton").AddCommandHandler((_, __) => CloseOverlay());
            Window.GetElementByName<MGButton>("CoveredButton").AddCommandHandler((_, __) => UpdateInteraction("Covered button invoked while overlay is closed"));

            UpdateFocusStatus();
        }

        private MGWindow CreatePopupWindow()
        {
            MGWindow popupWindow = new(Window, 385, 150, 280, 220)
            {
                TitleText = "Nested Review Window",
                IsCloseButtonVisible = false,
                CanCloseWindow = false
            };

            MGStackPanel content = new(popupWindow, Orientation.Vertical)
            {
                Spacing = 6,
                Padding = new(8)
            };

            content.TryAddChild(new MGTextBlock(popupWindow,
                "This non-modal nested window is used to verify that focus stays on the active child when it is front-most."));

            MGTextBox popupTextBox = new(popupWindow)
            {
                Name = "PopupTextBox",
                PlaceholderText = "Type here after opening the popup"
            };
            content.TryAddChild(popupTextBox);

            MGListBox<string> popupListBox = new(popupWindow)
            {
                Name = "PopupListBox",
                PreferredHeight = 90
            };
            content.TryAddChild(popupListBox);

            content.TryAddChild(new MGTextBlock(popupWindow,
                "Use the parent buttons to reopen or bring this child window to front."));

            popupWindow.SetContent(content);
            return popupWindow;
        }

        private void OpenPopup()
        {
            if (!Window.NestedWindows.Contains(PopupWindow))
            {
                Window.AddNestedWindow(PopupWindow);
            }

            Window.BringToFront(PopupWindow);
            PopupStatusText.Text = "Child window: open";
            UpdateInteraction("Nested child window opened");
            PopupTextBox.Focus();
        }

        private void BringPopupToFront()
        {
            if (!Window.NestedWindows.Contains(PopupWindow))
            {
                OpenPopup();
                return;
            }

            Window.BringToFront(PopupWindow);
            PopupTextBox.Focus();
            UpdateInteraction("Nested child window brought to front");
        }

        private void ClosePopup()
        {
            if (Window.RemoveNestedWindow(PopupWindow))
            {
                PopupStatusText.Text = "Child window: closed";
                UpdateInteraction("Nested child window closed");
            }

            SearchTextBox.Focus();
        }

        private void OpenOverlay()
        {
            BlockingOverlay.IsOpen = true;
            UpdateInteraction("Modal overlay opened");
            OverlayTextBox.Focus();
        }

        private void CloseOverlay()
        {
            BlockingOverlay.IsOpen = false;
            UpdateInteraction("Modal overlay closed");
            CoveredTextBox.Focus();
        }

        private void ResetTransientState()
        {
            BlockingOverlay.IsOpen = false;
            Window.RemoveNestedWindow(PopupWindow);
            PopupStatusText.Text = "Child window: closed";
            ContextMenuStatusText.Text = "Context menu: closed";
        }

        private void UpdateInteraction(string message)
        {
            InteractionStatusText.Text = $"Last interaction: {message}";
            UpdateFocusStatus();
        }

        private void UpdateFocusStatus()
        {
            MGElement focused = Desktop.FocusedKeyboardHandler;
            FocusStatusText.Text = $"Focused element: {DescribeElement(focused)}";
        }

        private static string DescribeElement(MGElement element)
        {
            if (element == null)
            {
                return "none";
            }

            return string.IsNullOrWhiteSpace(element.Name)
                ? element.GetType().Name
                : $"{element.Name} ({element.GetType().Name})";
        }
    }
}