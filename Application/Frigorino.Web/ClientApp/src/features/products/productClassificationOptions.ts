import type { ExpiryHandling, ProductCategory } from "../../lib/api/types.gen";

// Selectable values exclude the Unknown sentinel — a user never deliberately picks
// "couldn't classify". Order is the catalog/aisle order from the backend enum.
export const PRODUCT_CATEGORY_OPTIONS: ProductCategory[] = [
    "Other",
    "Produce",
    "Bakery",
    "Meat",
    "Fish",
    "DairyAndEggs",
    "Cheese",
    "DeliAndColdCuts",
    "Frozen",
    "Pantry",
    "CannedGoods",
    "Sauces",
    "OilsAndVinegar",
    "Spices",
    "Cereal",
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

export const EXPIRY_HANDLING_OPTIONS: ExpiryHandling[] = [
    "NonPerishable",
    "UserEntersFromPackage",
    "AiRecommendsShelfLife",
];
