using Frigorino.Domain.Products;

namespace Frigorino.Domain.Entities
{
    // One ordered aisle within a blueprint. Rows are replaced wholesale on edit; the
    // composite key (BlueprintId, Category) enforces "an aisle appears at most once".
    public class SortBlueprintCategory
    {
        public int BlueprintId { get; set; }
        public ProductCategory Category { get; set; }
        public int OrderIndex { get; set; }

        public SortBlueprint Blueprint { get; set; } = null!;
    }
}
