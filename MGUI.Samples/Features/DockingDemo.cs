using System;
using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    /// <summary>
    /// Demo scene for the MGUI Docking System.
    /// Showcases tabs, splitters, drag & drop, and dynamic layout management.
    /// </summary>
    public class DockingDemo : ViewModelBase
    {
        public ContentManager Content { get; }
        public MGDesktop Desktop { get; }
        public MGWindow Window { get; }
        private MGDockHost _dockHost;
        private const string LayoutFilePath = "docking_layout.json";

        private bool _IsVisible;
        public bool IsVisible
        {
            get => _IsVisible;
            set
            {
                if (_IsVisible != value)
                {
                    _IsVisible = value;
                    NPC(nameof(IsVisible));

                    if (IsVisible)
                        Desktop.Windows.Add(Window);
                    else
                        Desktop.Windows.Remove(Window);
                }
            }
        }

        public void Show() => IsVisible = true;
        public void Hide() => IsVisible = false;

        public DockingDemo(ContentManager content, MGDesktop desktop)
        {
            Content = content;
            Desktop = desktop;

            // Create window programmatically
            Window = new MGWindow(desktop, 100, 100, 1200, 800);
            Window.WindowStyle = WindowStyle.Default;
            Window.TitleText = "Docking System Demo";
            Window.MinWidth = 800;
            Window.MinHeight = 600;
            Window.WindowClosed += (sender, e) => IsVisible = false;

            // Create and configure dock host
            _dockHost = new MGDockHost(Window);
            SetupInitialLayout();

            // Set as window content
            Window.SetContent(_dockHost);
        }

        /// <summary>
        /// Sets up the initial docking layout with example panels.
        /// Layout: [Left Sidebar 25%] | [Center-Bottom Split 75%]
        /// </summary>
        private void SetupInitialLayout()
        {
            // Create demo panels with specific IDs for save/load
            var solutionExplorerPanel = new DockPanelNode("panel_solution_explorer")
            {
                Title = "Solution Explorer",
                Icon = null,
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => CreateSolutionExplorerContent()
            };

            var propertiesPanel = new DockPanelNode("panel_properties")
            {
                Title = "Properties",
                Icon = null,
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => CreatePropertiesContent()
            };

            var outputPanel = new DockPanelNode("panel_output")
            {
                Title = "Output",
                Icon = null,
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => CreateOutputContent()
            };

            var layoutManagerPanel = new DockPanelNode("panel_layout_manager")
            {
                Title = "Layout Manager",
                Icon = null,
                CanClose = false, // Cannot be closed
                CanFloat = true,
                ContentFactory = () => CreateLayoutManagerContent()
            };

            var document1Panel = new DockPanelNode("panel_document1")
            {
                Title = "Document1.cs",
                Icon = null,
                CanClose = false,
                CanFloat = true,
                ContentFactory = () => CreateDocumentContent("Document1.cs")
            };

            var document2Panel = new DockPanelNode("panel_readme")
            {
                Title = "README.md",
                Icon = null,
                CanClose = true,
                CanFloat = true,
                ContentFactory = () => CreateDocumentContent("README.md")
            };

            // Build tab groups
            var leftGroup = new DockTabGroupNode();
            leftGroup.AddPanel(solutionExplorerPanel, -1);
            leftGroup.AddPanel(propertiesPanel, -1);
            leftGroup.AddPanel(layoutManagerPanel, -1);
            leftGroup.SetActivePanel(solutionExplorerPanel.Id);

            var bottomGroup = new DockTabGroupNode();
            bottomGroup.AddPanel(outputPanel, -1);

            var centerGroup = new DockTabGroupNode();
            centerGroup.AddPanel(document1Panel, -1);
            centerGroup.AddPanel(document2Panel, -1);
            centerGroup.SetActivePanel(document1Panel.Id);

            // Build split hierarchy
            // Center-Bottom vertical split (70% center, 30% bottom)
            var centerBottomSplit = new DockSplitNode
            {
                Orientation = Orientation.Vertical,
                FirstChild = centerGroup,
                SecondChild = bottomGroup,
                SplitRatio = 0.7f,
                MinFirstSize = 100,
                MinSecondSize = 100
            };

            // Root horizontal split (25% left, 75% center-bottom)
            var rootSplit = new DockSplitNode
            {
                Orientation = Orientation.Horizontal,
                FirstChild = leftGroup,
                SecondChild = centerBottomSplit,
                SplitRatio = 0.25f,
                MinFirstSize = 150,
                MinSecondSize = 300
            };

            // Set as root layout
            _dockHost.LayoutModel.RootNode = rootSplit;
        }

        /// <summary>
        /// Creates content for the Solution Explorer panel.
        /// </summary>
        private MGElement CreateSolutionExplorerContent()
        {
            var stackPanel = new MGStackPanel(Window, Orientation.Vertical)
            {
                Spacing = 8,
                Padding = new Core.UI.XAML.Thickness(10).ToThickness()
            };

            var header = new MGTextBlock(Window, "[b]Solution Explorer[/b]")
            {
                FontSize = 14
            };

            var tree = new MGTreeView(Window);
            var rootItem = new MGTreeViewItem(Window) { Header = "MyProject (C#)" };
            tree.AddItem(rootItem);
            var srcFolder = new MGTreeViewItem(Window) { Header = "src" };
            rootItem.AddItem(srcFolder);
            srcFolder.AddItem(new MGTreeViewItem(Window) { Header = "Program.cs" });
            srcFolder.AddItem(new MGTreeViewItem(Window) { Header = "MainWindow.cs" });
            srcFolder.AddItem(new MGTreeViewItem(Window) { Header = "DockingSystem.cs" });
            var propsFolder = new MGTreeViewItem(Window) { Header = "Properties" };
            rootItem.AddItem(propsFolder);
            propsFolder.AddItem(new MGTreeViewItem(Window) { Header = "AssemblyInfo.cs" });
            rootItem.AddItem(new MGTreeViewItem(Window) { Header = "MyProject.csproj" });
            rootItem.AddItem(new MGTreeViewItem(Window) { Header = "README.md" });

            stackPanel.TryAddChild(header);
            stackPanel.TryAddChild(tree);

            return stackPanel;
        }

        /// <summary>
        /// Creates content for the Properties panel.
        /// </summary>
        private MGElement CreatePropertiesContent()
        {
            var stackPanel = new MGStackPanel(Window, Orientation.Vertical)
            {
                Spacing = 4,
                Padding = new Core.UI.XAML.Thickness(10).ToThickness()
            };

            stackPanel.TryAddChild(new MGTextBlock(Window, "[b]Properties[/b]") { FontSize = 14 });
            stackPanel.TryAddChild(new MGSeparator(Window, Orientation.Horizontal) { Margin = new Core.UI.XAML.Thickness(0, 4).ToThickness() });
            stackPanel.TryAddChild(new MGTextBlock(Window, "[b]Name:[/b] MyProject") { Margin = new Core.UI.XAML.Thickness(0, 2).ToThickness() });
            stackPanel.TryAddChild(new MGTextBlock(Window, "[b]Type:[/b] C# Project") { Margin = new Core.UI.XAML.Thickness(0, 2).ToThickness() });
            stackPanel.TryAddChild(new MGTextBlock(Window, "[b]Framework:[/b] .NET 9.0") { Margin = new Core.UI.XAML.Thickness(0, 2).ToThickness() });
            stackPanel.TryAddChild(new MGTextBlock(Window, "[b]Platform:[/b] Windows") { Margin = new Core.UI.XAML.Thickness(0, 2).ToThickness() });
            stackPanel.TryAddChild(new MGSeparator(Window, Orientation.Horizontal) { Margin = new Core.UI.XAML.Thickness(0, 8, 0, 4).ToThickness() });
            stackPanel.TryAddChild(new MGTextBlock(Window, "[i]Select an item to view its properties[/i]") { Opacity = 0.7f });

            return stackPanel;
        }

        /// <summary>
        /// Creates content for the Output panel.
        /// </summary>
        private MGElement CreateOutputContent()
        {
            var scrollViewer = new MGScrollViewer(Window);
            
            var textBlock = new MGTextBlock(Window, "")
            {
                Padding = new Core.UI.XAML.Thickness(10).ToThickness(),
                FontSize = 11
            };

            string outputText = "[b]Build Output[/b]\n" +
                                "========================================\n" +
                                "1> Building MyProject.csproj...\n" +
                                "1> Restore completed (0.2s)\n" +
                                "1> MGUI.Core net9.0-windows succeeded (1.0s)\n" +
                                "1> MyProject net9.0-windows succeeded (0.8s)\n" +
                                "========================================\n" +
                                "[c=LightGreen]Build succeeded[/c]\n" +
                                "    0 Warning(s)\n" +
                                "    0 Error(s)\n" +
                                "\n" +
                                "Time Elapsed: 00:00:02.15\n" +
                                "\n" +
                                "[i]Try dragging tabs between panels![/i]\n" +
                                "[i]Drag to edges to create splits.[/i]";

            textBlock.SetText(outputText);
            scrollViewer.SetContent(textBlock);

            return scrollViewer;
        }

        /// <summary>
        /// Creates content for a document panel.
        /// </summary>
        private MGElement CreateDocumentContent(string documentName)
        {
            var scrollViewer = new MGScrollViewer(Window);
            
            var textBlock = new MGTextBlock(Window, "")
            {
                Padding = new Core.UI.XAML.Thickness(10).ToThickness(),
                FontSize = 11
            };

            string content = documentName switch
            {
                "Document1.cs" => 
                    "[b]Document1.cs[/b]\n\n" +
                    "using System;\n" +
                    "using MGUI.Core.UI;\n\n" +
                    "namespace MyProject\n" +
                    "{\n" +
                    "    public class Document1\n" +
                    "    {\n" +
                    "        public void HelloWorld()\n" +
                    "        {\n" +
                    "            Console.WriteLine(\"Hello, Docking System!\");\n" +
                    "        }\n" +
                    "    }\n" +
                    "}",

                "README.md" =>
                    "[b]README.md[/b]\n\n" +
                    "# MGUI Docking System Demo\n\n" +
                    "Welcome to the MGUI Docking System!\n\n" +
                    "## Features\n" +
                    "- [c=Cyan]Drag & Drop[/c]: Click and drag tabs to move them\n" +
                    "- [c=Cyan]Split Panels[/c]: Drag to edges to create splits\n" +
                    "- [c=Cyan]Tab Groups[/c]: Drop in center to add to group\n" +
                    "- [c=Cyan]Resizable[/c]: Drag splitters to resize\n" +
                    "- [c=Cyan]Closable[/c]: Click X to close tabs\n\n" +
                    "## Try It Out\n" +
                    "1. Drag 'Output' tab to the center panel\n" +
                    "2. Drag 'Properties' to the left edge\n" +
                    "3. Resize splitters by dragging\n" +
                    "4. Close and reopen tabs\n\n" +
                    "[i]This is Phase 1 MVP - more features coming soon![/i]",

                _ => $"[b]{documentName}[/b]\n\nContent goes here..."
            };

            textBlock.SetText(content);
            scrollViewer.SetContent(textBlock);

            return scrollViewer;
        }

        /// <summary>
        /// Creates content for the Layout Manager panel with Save/Load buttons.
        /// </summary>
        private MGElement CreateLayoutManagerContent()
        {
            var stackPanel = new MGStackPanel(Window, Orientation.Vertical)
            {
                Spacing = 12,
                Padding = new Core.UI.XAML.Thickness(10).ToThickness()
            };

            // Header
            var header = new MGTextBlock(Window, "[b]Layout Manager[/b]")
            {
                FontSize = 14,
                Margin = new Core.UI.XAML.Thickness(0, 0, 0, 8).ToThickness()
            };
            stackPanel.TryAddChild(header);

            // Description
            var description = new MGTextBlock(Window, 
                "[i]This panel cannot be closed.[/i]\n\n" +
                "Use the buttons below to save and load\nthe current docking layout.")
            {
                FontSize = 10,
                Opacity = 0.8f,
                Margin = new Core.UI.XAML.Thickness(0, 0, 0, 8).ToThickness()
            };
            stackPanel.TryAddChild(description);

            stackPanel.TryAddChild(new MGSeparator(Window, Orientation.Horizontal) 
            { 
                Margin = new Core.UI.XAML.Thickness(0, 4, 0, 8).ToThickness() 
            });

            // Save Layout Button
            var saveButton = new MGButton(Window, (btn) =>
            {
                try
                {
                    SaveLayout();
                    ShowNotification("Layout saved successfully!", Color.LightGreen);
                }
                catch (Exception ex)
                {
                    ShowNotification($"Error saving layout:\n{ex.Message}", Color.OrangeRed);
                }
            })
            {
                MinWidth = 150,
                Padding = new Core.UI.XAML.Thickness(12, 8).ToThickness()
            };
            saveButton.SetContent(new MGTextBlock(Window, "Save Layout"));
            stackPanel.TryAddChild(saveButton);

            // Load Layout Button
            var loadButton = new MGButton(Window, (btn) =>
            {
                try
                {
                    LoadLayout();
                    ShowNotification("Layout loaded successfully!", Color.LightGreen);
                }
                catch (Exception ex)
                {
                    ShowNotification($"Error loading layout:\n{ex.Message}", Color.OrangeRed);
                }
            })
            {
                MinWidth = 150,
                Padding = new Core.UI.XAML.Thickness(12, 8).ToThickness(),
                Margin = new Core.UI.XAML.Thickness(0, 8, 0, 0).ToThickness()
            };
            loadButton.SetContent(new MGTextBlock(Window, "Load Layout"));
            stackPanel.TryAddChild(loadButton);

            stackPanel.TryAddChild(new MGSeparator(Window, Orientation.Horizontal) 
            { 
                Margin = new Core.UI.XAML.Thickness(0, 12, 0, 8).ToThickness() 
            });

            // Status info
            var statusText = new MGTextBlock(Window, 
                $"[c=Gray]Layout file:[/c]\n{LayoutFilePath}")
            {
                FontSize = 9,
                Opacity = 0.7f
            };
            stackPanel.TryAddChild(statusText);

            // Instructions
            var instructions = new MGTextBlock(Window,
                "[b]Testing Instructions:[/b]\n" +
                "1. Rearrange the panels by dragging\n" +
                "2. Click 'Save Layout'\n" +
                "3. Close and restart the app\n" +
                "4. Click 'Load Layout'\n" +
                "5. Your layout is restored!")
            {
                FontSize = 9,
                Opacity = 0.7f,
                Margin = new Core.UI.XAML.Thickness(0, 12, 0, 0).ToThickness()
            };
            stackPanel.TryAddChild(instructions);

            return stackPanel;
        }

        /// <summary>
        /// Saves the current docking layout to a JSON file.
        /// </summary>
        private void SaveLayout()
        {
            string json = _dockHost.SaveLayoutToJson(indented: true);
            File.WriteAllText(LayoutFilePath, json);
            System.Diagnostics.Debug.WriteLine($"[DockingDemo] Layout saved to: {Path.GetFullPath(LayoutFilePath)}");
        }

        /// <summary>
        /// Loads the docking layout from a JSON file.
        /// </summary>
        private void LoadLayout()
        {
            if (!File.Exists(LayoutFilePath))
            {
                throw new FileNotFoundException($"Layout file not found: {LayoutFilePath}");
            }

            string json = File.ReadAllText(LayoutFilePath);
            
            // Load with a factory that recreates panel content based on panel ID
            _dockHost.LoadLayoutFromJson(json, panelFactory: panelId =>
            {
                // Map panel IDs to their content factories
                Func<MGElement> factory = panelId switch
                {
                    "panel_solution_explorer" => () => CreateSolutionExplorerContent(),
                    "panel_properties" => () => CreatePropertiesContent(),
                    "panel_output" => () => CreateOutputContent(),
                    "panel_layout_manager" => () => CreateLayoutManagerContent(),
                    "panel_document1" => () => CreateDocumentContent("Document1.cs"),
                    "panel_readme" => () => CreateDocumentContent("README.md"),
                    _ => null
                };
                
                if (factory == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DockingDemo] Warning: Unknown panel ID '{panelId}'");
                }
                
                return factory;
            });

            System.Diagnostics.Debug.WriteLine($"[DockingDemo] Layout loaded from: {Path.GetFullPath(LayoutFilePath)}");
        }

        /// <summary>
        /// Shows a temporary notification message in the output window.
        /// </summary>
        private void ShowNotification(string message, Color color)
        {
            System.Diagnostics.Debug.WriteLine($"[DockingDemo] {message}");
            // In a real app, you might show this in a status bar or toast notification
        }
    }
}
