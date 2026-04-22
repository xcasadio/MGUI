using MGUI.Core.UI;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Samples.Controls
{
    public readonly record struct ProcessRecord(int Pid, string Name, string State, string Owner, int WorkingSetMb, double CpuPercent, DateTime LastUpdate);

    public class DataGridLiteSamples : SampleBase
    {
        public DataGridLiteSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, nameof(Controls), "DataGridLite.xaml")
        {
            ApplyScenarioId("SCN-GRID-001");

            MGDataGrid<ProcessRecord> grid = Window.GetElementByName<MGDataGrid<ProcessRecord>>("ProcessGrid");
            MGComboBox<string> selectionModeComboBox = Window.GetElementByName<MGComboBox<string>>("SelectionModeComboBox");
            MGTextBlock selectionSummary = Window.GetElementByName<MGTextBlock>("SelectionSummary");

            grid.AddTextColumn(new ListViewColumnWidth(72), "PID", row => row.Pid, row => row.Pid, HorizontalAlignment.Right);
            grid.AddTextColumn(new ListViewColumnWidth(2.2), "Process", row => row.Name, row => row.Name);
            grid.AddTextColumn(new ListViewColumnWidth(94), "State", row => row.State, row => row.State, HorizontalAlignment.Left,
                row => GetStateColor(row.State));
            grid.AddTextColumn(new ListViewColumnWidth(120), "Owner", row => row.Owner, row => row.Owner);
            grid.AddTextColumn(new ListViewColumnWidth(118), "Working Set", row => $"{row.WorkingSetMb} MB", row => row.WorkingSetMb, HorizontalAlignment.Right);
            grid.AddTextColumn(new ListViewColumnWidth(88), "CPU %", row => row.CpuPercent.ToString("F1"), row => row.CpuPercent, HorizontalAlignment.Right,
                row => row.CpuPercent >= 65 ? Color.Orange : row.CpuPercent >= 40 ? Color.Khaki : null);
            grid.AddTextColumn(new ListViewColumnWidth(150), "Last Update", row => row.LastUpdate.ToString("HH:mm:ss"), row => row.LastUpdate, HorizontalAlignment.Right);

            List<ProcessRecord> rows = CreateRows();
            grid.SetItemsSource(rows);
            ApplyBalancedWidths(grid);

            void RefreshSelectionSummary()
            {
                if (grid.TryGetSelectedItem(out ProcessRecord selected))
                {
                    selectionSummary.Text = $"Selection: PID {selected.Pid}  {selected.Name}  CPU {selected.CpuPercent:F1}%  WS {selected.WorkingSetMb} MB";
                }
                else
                {
                    selectionSummary.Text = "Selection: <none>";
                }
            }

            void ApplySelectionMode(string selectedMode)
            {
                grid.SelectionMode = selectedMode switch
                {
                    "Row" => GridSelectionMode.Row,
                    _ => GridSelectionMode.None,
                };
            }

            ApplySelectionMode(selectionModeComboBox.SelectedItem);
            RefreshSelectionSummary();

            selectionModeComboBox.SelectedItemChanged += (_, e) => ApplySelectionMode(e.NewValue);
            grid.SelectedItemChanged += (_, _) => RefreshSelectionSummary();

            Window.GetElementByName<MGButton>("SortCpuDescButton").MouseHandler.LMBReleasedInside += (_, __) => grid.SortByColumnIndex(5, SortDirection.Descending);
            Window.GetElementByName<MGButton>("ClearSortButton").MouseHandler.LMBReleasedInside += (_, __) => grid.ClearSort();
            Window.GetElementByName<MGButton>("JumpLastButton").MouseHandler.LMBReleasedInside += (_, __) => grid.SelectRow(rows.Count - 1);
            Window.GetElementByName<MGButton>("CompactWidthsButton").MouseHandler.LMBReleasedInside += (_, __) => ApplyCompactWidths(grid);
            Window.GetElementByName<MGButton>("BalancedWidthsButton").MouseHandler.LMBReleasedInside += (_, __) => ApplyBalancedWidths(grid);
        }

        private static void ApplyCompactWidths(MGDataGrid<ProcessRecord> grid)
        {
            grid.ResizeColumnPixels(0, 64);
            grid.ResizeColumnWeight(1, 1.6);
            grid.ResizeColumnPixels(2, 82);
            grid.ResizeColumnPixels(3, 110);
            grid.ResizeColumnPixels(4, 104);
            grid.ResizeColumnPixels(5, 78);
            grid.ResizeColumnPixels(6, 136);
        }

        private static void ApplyBalancedWidths(MGDataGrid<ProcessRecord> grid)
        {
            grid.ResizeColumnPixels(0, 72);
            grid.ResizeColumnWeight(1, 2.2);
            grid.ResizeColumnPixels(2, 94);
            grid.ResizeColumnPixels(3, 120);
            grid.ResizeColumnPixels(4, 118);
            grid.ResizeColumnPixels(5, 88);
            grid.ResizeColumnPixels(6, 150);
        }

        private static Color GetStateColor(string state) => state switch
        {
            "Running" => Color.LimeGreen,
            "Waiting" => Color.Khaki,
            "Blocked" => Color.Orange,
            "Paused" => Color.CornflowerBlue,
            _ => Color.LightGray,
        };

        private static List<ProcessRecord> CreateRows()
        {
            string[] names = new[]
            {
                "RendererHost", "UiReplay", "AssetWatcher", "DockWorkspace", "SceneCapture", "InputTrace", "ThemeBroker", "ClipDebugger",
                "TextMetrics", "TelemetrySink", "HotReload", "GameplayHud", "AudioBridge", "ParticleSync", "NavCache", "ScriptConsole"
            };
            string[] owners = new[] { "Runtime", "Tools", "Editor", "Gameplay" };
            string[] states = new[] { "Running", "Waiting", "Blocked", "Paused" };

            List<ProcessRecord> rows = new();
            DateTime anchor = DateTime.Today.AddHours(9);
            for (int index = 0; index < 40; index++)
            {
                rows.Add(new ProcessRecord(
                    4200 + index,
                    $"{names[index % names.Length]}-{index:D2}",
                    states[index % states.Length],
                    owners[index % owners.Length],
                    96 + index * 7,
                    Math.Round(((index * 13) % 91) + (index % 3) * 0.4, 1),
                    anchor.AddSeconds(index * 17)));
            }

            return rows;
        }
    }
}