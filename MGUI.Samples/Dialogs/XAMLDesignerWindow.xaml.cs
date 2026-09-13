using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MGUI.Core.UI.Brushes.FillBrushes;

namespace MGUI.Samples.Dialogs
{
    public class XAMLDesignerWindow : SampleBase
    {
        public XAMLDesignerWindow(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, $"{nameof(Dialogs)}", $"{nameof(XAMLDesignerWindow)}.xaml")
        {
            ApplyScenarioId("SCN-MARKUP-001");

            if (Window.BackgroundBrush.NormalValue is MGSolidFillBrush SolidFill)
            {
                Window.BackgroundBrush.NormalValue = SolidFill * 0.5f;
            }
        }
    }
}
