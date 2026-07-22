namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        public class ValidationResult
        {
            public CheckCategory Category;
            public string Fix;
            public string Message;
            public ValidationStatus Status;

            /// <summary>Category is required, never defaulted: it routes the result to its gate and its
            /// vendor group, so an omitted one used to silently file the result under SDK Versions.</summary>
            public ValidationResult(ValidationStatus status, string message, string fix, CheckCategory category)
            {
                Status = status;
                Message = message;
                Fix = fix;
                Category = category;
            }
        }
    }
}
