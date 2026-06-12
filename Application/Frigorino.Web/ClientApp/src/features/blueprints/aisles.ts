import type { ProductCategory } from "../../lib/api/types.gen";

// All 23 real aisles in the canonical default walk-order. Excludes the Unknown/Other sentinels —
// they are never part of a blueprint; items in those categories (or unclassified) sink to the
// bottom on apply. Keep in sync with SortBlueprint.DefaultOrder on the backend.
export const ALL_AISLES: ProductCategory[] = [
    "Produce",
    "Bakery",
    "DeliAndColdCuts",
    "Meat",
    "Fish",
    "DairyAndEggs",
    "Cheese",
    "Frozen",
    "Cereal",
    "Pantry",
    "CannedGoods",
    "Sauces",
    "OilsAndVinegar",
    "Spices",
    "Spreads",
    "Snacks",
    "Sweets",
    "Beverages",
    "Alcohol",
    "HouseholdAndCleaning",
    "HealthAndBeauty",
    "Baby",
    "Pet",
];

// i18n key for an aisle's display name (e.g. "blueprints.aisles.Produce").
export const aisleLabelKey = (category: ProductCategory): string =>
    `blueprints.aisles.${category}`;
