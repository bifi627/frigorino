namespace Frigorino.Domain.Entities
{
    // An external source link for a recipe (blog post, video, …). A required URL plus an
    // optional display label. Ordering, validation, and lifecycle (add/update/delete/restore/
    // reorder) live on the parent Recipe aggregate; this is a plain data holder.
    public class RecipeLink
    {
        public const int UrlMaxLength = 2048;
        public const int LabelMaxLength = 255;

        public int Id { get; set; }
        public int RecipeId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Label { get; set; }

        // Lexicographic ordering key (fractional index), unique per RECIPE.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Recipe Recipe { get; set; } = null!;
    }
}
