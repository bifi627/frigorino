namespace Frigorino.Domain.Entities
{
    // Curated recipe tags, two facets in one flat enum. Stored as int in an integer[] column on
    // Recipe (value-set; no join table). Serialized as string names on the wire
    // (JsonStringEnumConverter, global). The AI suggester's strict-output schema derives its allowed
    // values from Enum.GetNames<RecipeTag>(), so adding/removing a value updates that schema with no
    // hand-edit — but OpenAiRecipeTagSuggester's system prompt should describe new values.
    // No member at 0: a recipe with no fitting tag simply has an empty set. Numeric ranges group the
    // facets (Course 1–19, Dietary 20+); the frontend grouping uses explicit arrays, not the ranges.
    public enum RecipeTag
    {
        // Course (1–19)
        Breakfast = 1,
        Starter = 2,
        Main = 3,
        Side = 4,
        Salad = 5,
        Soup = 6,
        Dessert = 7,
        Snack = 8,
        Drink = 9,
        Sauce = 10,
        Baking = 11,
        Bread = 12,

        // Dietary (20+)
        Vegetarian = 20,
        Vegan = 21,
        GlutenFree = 22,
        DairyFree = 23,
        LactoseFree = 24,
        LowCarb = 25,
    }
}
