import { Box, CircularProgress } from "@mui/material";

export function FullPageSpinner() {
    return (
        <Box
            sx={{
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                minHeight: "100vh",
            }}
        >
            <CircularProgress size={40} />
        </Box>
    );
}
