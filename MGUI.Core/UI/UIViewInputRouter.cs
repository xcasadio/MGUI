using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI
{
    /// <summary>Chooses which <see cref="UIView"/> should receive routed input when several views are active.</summary>
    public class UIViewInputRouter
    {
        public UIView PreferredInputView { get; private set; }

        public void SetPreferredInputView(UIView View)
            => PreferredInputView = View;

        public void ClearPreferredInputView()
            => PreferredInputView = null;

        public UIView ResolveTargetView(IReadOnlyList<UIView> Views, UIView HoveredView = null)
        {
            if (PreferredInputView != null && Views?.Contains(PreferredInputView) == true)
                return PreferredInputView;

            return HoveredView ?? Views?.LastOrDefault();
        }

        public UIView RouteToTargetView(IReadOnlyList<UIView> Views, UIView HoveredView = null)
        {
            UIView target = ResolveTargetView(Views, HoveredView);
            if (target != null)
                PreferredInputView = target;

            return target;
        }
    }
}