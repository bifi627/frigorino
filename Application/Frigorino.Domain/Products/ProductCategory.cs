namespace Frigorino.Domain.Products
{
    // The "what kind / which aisle" facet of a product, orthogonal to ExpiryProfile (how it
    // expires). Stored as an int column on Product (EF default — no migration when values change).
    // Two sentinels + 23 supermarket aisles:
    //   Unknown = 0 is default(ProductCategory) AND the classification-failure fallback (we could
    //     not classify it: nonsense / refusal / inconsistent model output).
    //   Other is a recognized item that is NOT a stocked grocery/household good (a task, a one-off).
    // The aisles exist so a future "sort a list by store walk-order" feature has a meaningful axis;
    // nothing reads this facet yet. The classifier's strict-output schema derives its enum from
    // Enum.GetNames<ProductCategory>(), so adding/removing a value here updates the schema with no
    // hand-edit — but the OpenAiItemClassifier system prompt must describe each value.
    public enum ProductCategory
    {
        Unknown = 0,
        Other = 1,
        Produce = 2,
        Bakery = 3,
        Meat = 4,
        Fish = 5,
        DairyAndEggs = 6,
        Cheese = 7,
        DeliAndColdCuts = 8,
        Frozen = 9,
        Pantry = 10,
        CannedGoods = 11,
        Sauces = 12,
        OilsAndVinegar = 13,
        Spices = 14,
        Cereal = 15,
        Spreads = 16,
        Snacks = 17,
        Sweets = 18,
        Beverages = 19,
        Alcohol = 20,
        HouseholdAndCleaning = 21,
        HealthAndBeauty = 22,
        Baby = 23,
        Pet = 24,
    }
}
