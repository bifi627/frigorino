/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { CalendarToday, Clear, Today } from "@mui/icons-material";
import { Box, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { parseLocalDate, todayIsoDate } from "../../../utils/dateUtils";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

// Expiry is a calendar date carried as a "YYYY-MM-DD" string end to end — the value an
// <input type="date"> natively reads/writes. No Date object, so no local↔UTC day shifts.
const formatForDisplay = (value: string | null) =>
    value
        ? parseLocalDate(value).toLocaleDateString("de-DE", {
              day: "2-digit",
              month: "2-digit",
          })
        : "";

const ExpiryToggle = ({
    value,
    open,
    toggleOpen,
}: FeatureSlot<string | null>) => {
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

const ExpiryChip = ({ value, toggleOpen }: FeatureSlot<string | null>) => {
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
}: FeatureSlot<string | null>) => {
    const { t } = useTranslation();
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
                value={value ?? ""}
                onChange={(e) => setValue(e.target.value || null)}
                disabled={disabled}
                size="small"
            />
            <IconButton
                onClick={() => setValue(todayIsoDate())}
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
    initial: null as string | null,
    isEmpty: (value) => value === null,
    renderToggle: (slot) => <ExpiryToggle {...slot} />,
    renderPanel: (slot) => <ExpiryPanel {...slot} />,
    renderChip: (slot) => <ExpiryChip {...slot} />,
});
