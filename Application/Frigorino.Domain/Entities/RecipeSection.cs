namespace Frigorino.Domain.Entities
{
    // An ordered part of a recipe (e.g. "Dough", "Filling"). Name is optional — an unnamed
    // section renders under the localized "Ingredients" header. Behaviour (create/rename/delete/
    // reorder) lives on the parent Recipe aggregate; this is a plain data holder.
    public class RecipeSection
    {
        public const int NameMaxLength = 100;
        public const int DescriptionMaxLength = 2000;

        public int Id { get; set; }
        public int RecipeId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }

        // Lexicographic ordering key (fractional index), unique per RECIPE.
        public string Rank { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Recipe Recipe { get; set; } = null!;
        public ICollection<RecipeItem> Items { get; set; } = new List<RecipeItem>();
    }
}
