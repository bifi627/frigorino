import { Edit } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

interface EditHeaderProps {
    label?: string;
}

// Slim left-aligned edit cue — a compact marker, not a full-width bar. The Composer
// lays this out in a row with the cancel (×) button pushed to the far right.
export const EditHeader = ({ label }: EditHeaderProps) => {
    const { t } = useTranslation();
    return (
        <Box
            sx={{
                display: "inline-flex",
                alignItems: "center",
                gap: 0.5,
                px: 0.75,
                py: 0.25,
                mb: 0.5,
                bgcolor: "warning.50",
                borderRadius: 1,
            }}
        >
            <Edit sx={{ fontSize: 14 }} color="warning" />
            <Typography variant="caption" sx={{ color: "warning.dark" }}>
                {label ?? t("common.edit")}
            </Typography>
        </Box>
    );
};

EditHeader.displayName = "EditHeader";
