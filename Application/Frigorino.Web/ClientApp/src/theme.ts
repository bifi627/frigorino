import { alpha, createTheme, responsiveFontSizes } from "@mui/material/styles";

export const appTheme = responsiveFontSizes(
    createTheme({
        palette: {
            mode: "dark",
            // Fresh-green primary suits a food/fridge app; used sparingly per direction A.
            primary: { main: "#43A047" },
            // Warm amber replaces the default clashing pink.
            secondary: { main: "#FFB300" },
        },
        shape: { borderRadius: 8 },
        components: {
            MuiButton: {
                styleOverrides: {
                    root: { textTransform: "none" },
                },
            },
        },
    }),
);

// Section identity colors ("quiet identity": applied only to the small section
// icon glyph for wayfinding — chrome stays neutral, and red/amber remain
// reserved for expiry status. Inventory deliberately stays cool so it never
// collides with the amber/red expiry chips shown on its own cards.
export const sectionColors = {
    household: "#5FA86F", // green — matches the brand identity in the picker
    lists: "#5A92CB", // blue
    inventory: "#4BA1A1", // teal
    recipes: "#D18A77", // warm coral
    blueprints: "#8E7CC3", // muted violet — distinct from the lists blue
} as const;

export type SectionKey = keyof typeof sectionColors;

// Neutral grey for non-identity action buttons (e.g. the overflow menu) — visible
// on the dark surface without competing with the section-colored identity actions.
export const neutralActionColor = "#9E9E9E";

// Soft, tinted, bordered action button. Reads as clearly tappable while staying
// quiet — a filled-but-muted glyph instead of a ghost icon. Pass a section color
// for identity actions (add / open / edit) or neutralActionColor for secondary
// ones. Shared by the dashboard cards and feature headers so action buttons look
// the same everywhere.
export const tintedActionButtonSx = (color: string) => ({
    color,
    bgcolor: alpha(color, 0.14),
    border: `1px solid ${alpha(color, 0.35)}`,
    transition: "all 0.2s ease",
    "&:hover": {
        bgcolor: alpha(color, 0.24),
        borderColor: color,
    },
    "&:active": {
        bgcolor: alpha(color, 0.32),
    },
});

export const pageContainerSx = {
    py: { xs: 2, sm: 3 },
    px: { xs: 1, sm: 2 },
};

// Shared horizontal inset for the feature screens (list/inventory header, item
// list, composer footer, promote bar) so their left/right edges line up. One
// source of truth keeps them from drifting apart.
export const featureContentPx = 3;
