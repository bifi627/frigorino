import {
    ChecklistOutlined,
    HomeOutlined,
    KitchenOutlined,
    RestaurantOutlined,
    SwapVertOutlined,
    type SvgIconComponent,
} from "@mui/icons-material";
import { type SectionKey } from "../theme";

// Section icon components, kept alongside `sectionColors` (theme.ts) so every
// surface — dashboard cards and page headers — shows the same glyph + hue per
// section. Lists = checklist, Inventory = fridge, Recipes = cutlery,
// Household = home, Blueprints = swap-vert (the drag-to-reorder walk-order).
export const sectionIcons: Record<SectionKey, SvgIconComponent> = {
    household: HomeOutlined,
    lists: ChecklistOutlined,
    inventory: KitchenOutlined,
    recipes: RestaurantOutlined,
    blueprints: SwapVertOutlined,
};
