using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MGUI.Shared.Helpers;

namespace MGUI.Samples.Controls
{
    public class ListBoxSamples : SampleBase
    {
        private readonly MGListBox<object> PerfList;
        private readonly MGTextBlock TbFps;
        private readonly MGTextBlock TbFrameMs;
        private readonly MGTextBlock TbElements;
        private readonly MGTextBlock TbMode;
        private readonly Stopwatch FpsWatch = Stopwatch.StartNew();
        private readonly EventHandler<EventArgs> OnEndUpdateHandler;
        private long LastTick;
        private int FrameCount;
        private int CurrentItemCount = 5_000;

        public ListBoxSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Controls)}", "ListBox.xaml")
        {
            MGListBox<string> ClickTestListBox = Window.GetElementByName<MGListBox<string>>("ClickTestListBox");
            MGListBox<string> HeaderlessListBox = Window.GetElementByName<MGListBox<string>>("HeaderlessListBox");
            MGTextBlock ClickStatus = Window.GetElementByName<MGTextBlock>("ListBoxClickStatus");
            MGTextBlock DoubleClickStatus = Window.GetElementByName<MGTextBlock>("ListBoxDoubleClickStatus");
            PerfList = Window.GetElementByName<MGListBox<object>>("PerfList");
            TbFps = Window.GetElementByName<MGTextBlock>("TbFps");
            TbFrameMs = Window.GetElementByName<MGTextBlock>("TbFrameMs");
            TbElements = Window.GetElementByName<MGTextBlock>("TbElements");
            TbMode = Window.GetElementByName<MGTextBlock>("TbMode");

            ClickTestListBox.SetItemsSource(new List<string>()
            {
                "Alpha",
                "Bravo",
                "Charlie",
                "Delta",
                "Echo",
                "Foxtrot"
            });

            HeaderlessListBox.SetItemsSource(new List<string>()
            {
                "Alpha",
                "Beta",
                "Gamma",
                "Delta",
                "Epsilon"
            });

            ClickTestListBox.SelectionChanged += (sender, selectedItems) =>
            {
                string SelectedValue = selectedItems.FirstOrDefault()?.Data ?? "none";
                string NewText = $"Last click: [b]{SelectedValue}[/b]";
                ClickStatus.SetText(NewText, NewText.Length == ClickStatus.Text.Length);
            };

            ClickTestListBox.MouseHandler.LMBDoubleClickedInside += (sender, e) =>
            {
                string ClickedValue = ClickTestListBox.ReleasedItem?.Data ?? ClickTestListBox.SelectedValue ?? "none";
                string NewText = $"Last double-click: [b]{ClickedValue}[/b]";
                DoubleClickStatus.SetText(NewText, NewText.Length == DoubleClickStatus.Text.Length);
            };

            Window.GetElementByName<MGButton>("BtnLoad5k").MouseHandler.LMBReleasedInside += (_, __) => LoadPerfItems(5_000);
            Window.GetElementByName<MGButton>("BtnLoad10k").MouseHandler.LMBReleasedInside += (_, __) => LoadPerfItems(10_000);
            Window.GetElementByName<MGButton>("BtnLoad50k").MouseHandler.LMBReleasedInside += (_, __) => LoadPerfItems(50_000);

            Window.GetElementByName<MGButton>("BtnModeNever").MouseHandler.LMBReleasedInside += (_, __) =>
            {
                PerfList.VirtualizationMode = ListBoxVirtualizationMode.Never;
                LoadPerfItems(CurrentItemCount);
            };
            Window.GetElementByName<MGButton>("BtnModeAuto").MouseHandler.LMBReleasedInside += (_, __) =>
            {
                PerfList.VirtualizationMode = ListBoxVirtualizationMode.Auto;
                LoadPerfItems(CurrentItemCount);
            };
            Window.GetElementByName<MGButton>("BtnModeAlways").MouseHandler.LMBReleasedInside += (_, __) =>
            {
                PerfList.VirtualizationMode = ListBoxVirtualizationMode.Always;
                LoadPerfItems(CurrentItemCount);
            };

            OnEndUpdateHandler = HandleEndUpdate;
            if (Desktop.Runtime is MainRenderer renderer && renderer.Host is IObservableUpdate observable)
            {
                observable.EndUpdate += OnEndUpdateHandler;
            }

            Window.WindowClosed += (_, __) =>
            {
                if (Desktop.Runtime is MainRenderer renderer && renderer.Host is IObservableUpdate observableHost)
                {
                    observableHost.EndUpdate -= OnEndUpdateHandler;
                }
            };

            LastTick = FpsWatch.ElapsedTicks;
            LoadPerfItems(CurrentItemCount);
        }

        private void LoadPerfItems(int count)
        {
            CurrentItemCount = count;
            var items = new List<object>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add($"Item {i + 1:N0}");
            }

            PerfList.SetItemsSource(items);
            RefreshPerfStats(0.0);
        }

        private void HandleEndUpdate(object sender, EventArgs e)
        {
            long now = FpsWatch.ElapsedTicks;
            double dtMs = (now - LastTick) * 1000.0 / Stopwatch.Frequency;
            LastTick = now;
            FrameCount++;

            double elapsedSeconds = FpsWatch.Elapsed.TotalSeconds;
            if (elapsedSeconds >= 0.5)
            {
                double fps = FrameCount / elapsedSeconds;
                TbFps.Text = $"FPS: {fps:F1}";
                TbFrameMs.Text = $"Frame time: {dtMs:F2} ms";
                FrameCount = 0;
                FpsWatch.Restart();
            }

            RefreshPerfStats(dtMs);
        }

        private void RefreshPerfStats(double dtMs)
        {
            bool isVirtualizing = PerfList.IsVirtualizing;
            int realizedCount;

            if (isVirtualizing)
            {
                VirtualizingStackPanel panel = PerfList.VirtualizingPanel;
                if (panel != null)
                {
                    int first = panel.FirstRealizedIndex;
                    int last = panel.LastRealizedIndex;
                    realizedCount = (first >= 0 && last >= first) ? (last - first + 1) : 0;
                }
                else
                {
                    realizedCount = 0;
                }
            }
            else
            {
                realizedCount = PerfList.ListBoxItems?.Count ?? 0;
            }

            TbElements.Text = $"Elements: total={CurrentItemCount:N0}   realized={realizedCount}";
            TbMode.Text = $"Mode: {PerfList.VirtualizationMode}  |  Virtualizing: {isVirtualizing}";
        }
    }
}
