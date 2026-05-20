namespace MGUI.Core.UI.Graph
{
    public sealed class GraphConnectionValidationResult
    {
        public bool IsValid { get; }
        public string Code { get; }
        public string Message { get; }

        private GraphConnectionValidationResult(bool isValid, string code, string message)
        {
            IsValid = isValid;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public static GraphConnectionValidationResult Valid { get; } = new(true, string.Empty, string.Empty);

        public static GraphConnectionValidationResult Invalid(string code, string message) => new(false, code, message);
    }
}