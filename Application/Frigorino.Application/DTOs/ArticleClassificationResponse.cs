namespace Frigorino.Application.DTOs
{
    public class ArticleClassificationResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int MinExpirationDuration { get; set; }
        public int MaxExpirationDuration { get; set; }
        public string? Error { get; set; }
        public ClassificationHint Hint { get; set; } = new ClassificationHint();
    }

    public class ClassificationHint
    {
        public string? HintCategory { get; set; }
        public string HintEstimation { get; set; } = string.Empty;
    }
}
