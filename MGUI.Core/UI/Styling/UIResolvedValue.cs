namespace MGUI.Core.UI.Styling
{
    public readonly record struct UIResolvedValue<T>(T Value, UIValueResolutionSource Source, bool IsSet = true)
    {
        public bool IsAnimated => Source.Kind == UIValueSourceKind.Animation;

        public bool IsLocal => Source.IsLocal;

        public bool HasInvalidation(UIInvalidationKind invalidation)
            => (Source.Invalidation & invalidation) == invalidation;

        public static UIResolvedValue<T> Unset(UIInvalidationKind invalidation = UIInvalidationKind.None)
            => new(default, UIValueResolutionSource.Default(invalidation), false);
    }
}