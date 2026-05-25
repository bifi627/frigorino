import { Close, Edit } from "@mui/icons-material";
import { Box, IconButton, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

interface EditHeaderProps {
    label?: string;
    onCancel: () => void;
}

export const EditHeader = ({ label, onCancel }: EditHeaderProps) => {
    const { t } = useTranslation();
    return (
        <Box
            sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                px: 0.5,
                py: 0.25,
                bgcolor: "warning.50",
                borderRadius: 1,
                mb: 0.5,
            }}
        >
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <Edit fontSize="small" color="warning" />
                <Typography variant="caption" sx={{ color: "warning.dark" }}>
                    {label ?? t("common.edit")}
                </Typography>
            </Box>
            <IconButton
                size="small"
                onClick={onCancel}
                aria-label={t("common.cancel")}
                sx={{
                    color: "warning.dark",
                    "&:hover": { bgcolor: "warning.100" },
                }}
            >
                <Close fontSize="small" />
            </IconButton>
        </Box>
    );
};

EditHeader.displayName = "EditHeader";
