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

export const pageContainerSx = {
    py: { xs: 2, sm: 3 },
    px: { xs: 1, sm: 2 },
};
