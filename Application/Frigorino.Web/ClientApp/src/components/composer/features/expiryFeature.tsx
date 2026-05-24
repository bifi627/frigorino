/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components, mirroring the
   existing pattern in components/inputs/context/AddInputContext.tsx. */
import { CalendarToday, Clear, Today } from "@mui/icons-material";
import { Box, IconButton, TextField, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const formatForDisplay = (date: Date | null) =>
    date ? date.toLocaleDateString("de-DE", { day: "2-digit", month: "2-digit" }) : "";

const formatForInput = (date: Date | null) =>
    date ? date.toISOString().split("T")[0] : "";

const ExpiryToggle = ({ value, open, toggleOpen }: FeatureSlot<Date | null>) => (
    <IconButton onClick={toggleOpen} size="small">
        {value ? (
            <Typography
                variant="caption"
                sx={{ fontWeight: "bold", color: "primary.main", minWidth: "30px" }}
            >
                {formatForDisplay(value)}
            </Typography>
        ) : (
            <CalendarToday
                fontSize="small"
                sx={{ color: open ? "primary.main" : "inherit" }}
            />
        )}
    </IconButton>
);

const ExpiryPanel = ({ value, setValue, disabled }: FeatureSlot<Date | null>) => {
    const { t } = useTranslation();
    const handleChange = (dateString: string) => {
        if (!dateString) {
            setValue(null);
            return;
        }
        const date = new Date(dateString);
        setValue(isNaN(date.getTime()) ? null : date);
    };
    return (
        <Box
            sx={{ display: "flex", alignItems: "center", gap: 0.75, width: "100%", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                fullWidth
                variant="outlined"
                placeholder={t("common.date")}
                type="date"
                value={formatForInput(value)}
                onChange={(e) => handleChange(e.target.value)}
                disabled={disabled}
                size="small"
            />
            <IconButton
                size="small"
                onClick={() => setValue(new Date())}
                title={t("common.setToday")}
            >
                <Today fontSize="small" />
            </IconButton>
            <IconButton
                size="small"
                onClick={() => setValue(null)}
                disabled={!value}
                title={t("common.clear")}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const expiryFeature = defineModifier({
    id: "expiry",
    initial: null as Date | null,
    isEmpty: (value) => value === null,
    renderToggle: (slot) => <ExpiryToggle {...slot} />,
    renderPanel: (slot) => <ExpiryPanel {...slot} />,
});
