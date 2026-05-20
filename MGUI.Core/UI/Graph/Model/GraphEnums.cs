namespace MGUI.Core.UI.Graph
{
    public enum GraphPortDirection
    {
        Input,
        Output,
    }

    public enum GraphPortCardinality
    {
        Single,
        Multiple,
    }

    public enum GraphValueType
    {
        Float,
        Int,
        Bool,
        String,
        Vector2,
        Vector3,
        Vector4,
        Color,
        Texture2D,
        Material,
        Entity,
        Exec,
        Object,
        Custom,
        Wildcard,
    }
}