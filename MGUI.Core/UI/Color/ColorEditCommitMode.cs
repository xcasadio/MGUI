namespace MGUI.Core.UI
{
    public enum ColorEditCommitMode
    {
        /// <summary>Every preview value is committed immediately.</summary>
        Live,
        /// <summary>Pointer drags preview continuously and commit once when the pointer is released.</summary>
        OnMouseRelease,
        /// <summary>Preview values remain pending until CommitEdit or CancelEdit is invoked.</summary>
        ExplicitOkCancel
    }
}