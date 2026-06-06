/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { CalendarToday } from "@mui/icons-material";
import { Box, Chip, IconButton } from "@mui/material";
import { useTranslation } from "react-i18next";
import { parseLocalDate } from "../../../utils/dateUtils";
import { ExpiryDatePicker } from "../../ExpiryDatePicker";
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
    return (
        <Box sx={{ width: "100%", p: 1 }} onClick={(e) => e.stopPropagation()}>
            <ExpiryDatePicker
                fullWidth
                value={value}
                onChange={setValue}
                disabled={disabled}
                dataTestId="composer-expiry-input"
            />
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
