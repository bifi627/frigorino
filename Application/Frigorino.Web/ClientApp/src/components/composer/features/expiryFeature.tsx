/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { CalendarToday, Clear, Today } from "@mui/icons-material";
import { Box, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const formatForDisplay = (date: Date | null) =>
    date
        ? date.toLocaleDateString("de-DE", { day: "2-digit", month: "2-digit" })
        : "";

const formatForInput = (date: Date | null) =>
    date ? date.toISOString().split("T")[0] : "";

const ExpiryToggle = ({
    value,
    open,
    toggleOpen,
}: FeatureSlot<Date | null>) => {
    const { t } = useTranslation();
    return (
        <IconButton
            onClick={toggleOpen}
            aria-label={t("common.date")}
            sx={{
                minWidth: 44,
                minHeight: 44,
                color: value || open ? "primary.main" : "inherit",
            }}
        >
            <CalendarToday fontSize="small" />
        </IconButton>
    );
};

const ExpiryChip = ({ value, toggleOpen }: FeatureSlot<Date | null>) => {
    const { t } = useTranslation();
    return (
        <Chip
            clickable
            onClick={toggleOpen}
            aria-label={`${t("common.edit")} ${t("common.date")}`}
            size="small"
            icon={<CalendarToday fontSize="small" />}
            label={formatForDisplay(value)}
            sx={{ minHeight: 32 }}
        />
    );
};

const ExpiryPanel = ({
    value,
    setValue,
    disabled,
}: FeatureSlot<Date | null>) => {
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
            sx={{
                display: "flex",
                alignItems: "center",
                gap: 0.75,
                width: "100%",
                p: 1,
            }}
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
                onClick={() => setValue(new Date())}
                title={t("common.setToday")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Today fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => setValue(null)}
                disabled={!value}
                title={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
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
    renderChip: (slot) => <ExpiryChip {...slot} />,
});
