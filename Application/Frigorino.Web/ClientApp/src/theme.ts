import { createTheme, responsiveFontSizes } from "@mui/material/styles";

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
} as const;

export type SectionKey = keyof typeof sectionColors;

export const pageContainerSx = {
    py: { xs: 2, sm: 3 },
    px: { xs: 1, sm: 2 },
};
