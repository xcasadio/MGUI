using System;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Styling
{
    public sealed class MGVisualStateProjection : IDisposable
    {
        private MGElement Source { get; set; }
        private Action<VisualState, VisualState> ApplyAction { get; }

        public MGVisualStateProjection(MGElement Source, Action<VisualState, VisualState> ApplyAction, bool ApplyImmediately = true)
        {
            this.Source = Source;
            this.ApplyAction = ApplyAction ?? throw new ArgumentNullException(nameof(ApplyAction));

            if (Source != null)
            {
                Source.VisualStateChanged += Source_VisualStateChanged;
                if (ApplyImmediately)
                {
                    Apply(Source.VisualState, Source.VisualState);
                }
            }
        }

        private void Source_VisualStateChanged(object sender, EventArgs<VisualState> e)
            => Apply(e.PreviousValue, e.NewValue);

        internal void Apply(VisualState PreviousState, VisualState CurrentState)
            => ApplyAction(PreviousState, CurrentState);

        public void Dispose()
        {
            if (Source != null)
            {
                Source.VisualStateChanged -= Source_VisualStateChanged;
                Source = null;
            }
        }
    }
}