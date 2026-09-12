namespace MGUI.Core.UI.Animation
{
    /// <summary>A plain (not store-backed) <see cref="IUIAnimationTarget{T}"/> over a getter and a setter, for application-defined properties
    /// and tests. Register it in <see cref="UIAnimationTargets"/> to address it by path, or hand it to <see cref="UIPropertyAnimation{T}.Target"/> directly.</summary>
    public sealed class UIDelegateAnimationTarget<T> : IUIAnimationTarget<T>
    {
        private readonly Func<MGElement, T> _getter;
        private readonly Action<MGElement, T> _setter;

        public UIDelegateAnimationTarget(string path, Func<MGElement, T> getter, Action<MGElement, T> setter)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A target path is required.", nameof(path));
            }

            Path = path;
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        }

        public string Path { get; }

        public bool IsStoreBacked => false;

        public T GetValue(MGElement element) => _getter(element);

        public void SetValue(MGElement element, T value, string animationName) => _setter(element, value);

        public void RestoreBaseValue(MGElement element, T baseValue) => _setter(element, baseValue);

        public override string ToString() => $"{nameof(UIDelegateAnimationTarget<T>)}({Path})";
    }
}
