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

            public ValidationResult(ValidationStatus status, string message, string fix = null, CheckCategory category = CheckCategory.VersionMismatches)
            {
                Status = status;
                Message = message;
                Fix = fix;
                Category = category;
            }
        }
    }
}
