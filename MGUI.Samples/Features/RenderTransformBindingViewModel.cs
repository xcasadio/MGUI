using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;

namespace MGUI.Samples.Features
{
    /// <summary>The <see cref="RenderTransformBindingSample"/> window's <c>WindowDataContext</c> (ADR-0020, "Bindable
    /// render transform"): the flying image's translation and scale, both driven per tick by the sample's own timer.
    /// Both properties are <see cref="Vector2"/>, exactly the type of the <c>RenderTransform.Translation</c>/
    /// <c>RenderTransform.Scale</c> they are bound to (via <c>RenderTransformTranslation</c>/<c>RenderTransformScale</c>,
    /// see <see cref="MGUI.Core.UI.XAML.Element.RenderTransformTranslation"/>), so each push takes the
    /// allocation-free typed-copy path.</summary>
    public sealed class RenderTransformBindingViewModel : ViewModelBase
    {
        private Vector2 _flyingTranslation;
        /// <summary>Bound (OneWay) to the flying image's <c>RenderTransformTranslation</c>, itself renamed by
        /// <c>BindingPathMappings</c> onto <c>MGElement.RenderTransform.Translation</c>.</summary>
        public Vector2 FlyingTranslation
        {
            get => _flyingTranslation;
            set
            {
                if (_flyingTranslation != value)
                {
                    _flyingTranslation = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private Vector2 _flyingScale = Vector2.One;
        /// <summary>Bound (OneWay) to the flying image's <c>RenderTransformScale</c>, itself renamed by
        /// <c>BindingPathMappings</c> onto <c>MGElement.RenderTransform.Scale</c>.</summary>
        public Vector2 FlyingScale
        {
            get => _flyingScale;
            set
            {
                if (_flyingScale != value)
                {
                    _flyingScale = value;
                    NotifyPropertyChanged();
                }
            }
        }
    }
}
