namespace Frigorino.Domain.Entities
{
    public enum ClassificationCategory
    {
        Fixed = 1,
        Estimated,
        None,
        Error,
    }

    public class ArticleClassification
    {
        public int Id { get; set; }
        public string OriginalName { get; set; } = string.Empty;
        public ClassificationCategory Category { get; set; }
        public int ExpirationDuration { get; set; }
        public string HintCategory { get; set; } = string.Empty;
        public string HintEstimation { get; set; } = string.Empty;

    }
}
