namespace ForteMove.Models.Common
{
    public sealed class ValidationError
    {
        public ValidationError(string field, string message)
        {
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Field { get; private set; }

        public string Message { get; private set; }
    }
}
