namespace MGUI.Core.UI.TextEditing
{
    public readonly record struct MGTextEditResult(MGTextRange RemovedRange, MGTextRange InsertedRange, string RemovedText, string InsertedText, int Version)
    {
        public int Delta => (InsertedText?.Length ?? 0) - (RemovedText?.Length ?? 0);
        public bool ChangedText => Delta != 0 || RemovedText != InsertedText;
        public int CaretIndexAfterEdit => InsertedRange.EndIndex;
    }
}