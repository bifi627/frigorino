/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { Add, Clear, Remove, ShoppingBag } from "@mui/icons-material";
import { Box, Button, ButtonGroup, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const QuantityToggle = ({ value, open, toggleOpen }: FeatureSlot<string>) => (
    <IconButton
        onClick={toggleOpen}
        sx={{
            minWidth: 44,
            minHeight: 44,
            color: value || open ? "primary.main" : "inherit",
        }}
    >
        <ShoppingBag fontSize="small" />
    </IconButton>
);

const QuantityChip = ({ value, toggleOpen }: FeatureSlot<string>) => (
    <Chip
        clickable
        onClick={toggleOpen}
        size="small"
        icon={<ShoppingBag fontSize="small" />}
        label={value}
        sx={{ minHeight: 32 }}
    />
);

const QuantityPanel = ({ value, setValue, disabled }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <Box
            sx={{ display: "flex", gap: 0.75, alignItems: "center", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                fullWidth
                variant="outlined"
                placeholder={t("common.quantity")}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                disabled={disabled}
                size="small"
            />
            <ButtonGroup variant="outlined" size="small">
                {[1, 2, 5].map((num) => (
                    <Button
                        key={num}
                        onClick={() => setValue(num.toString())}
                        variant={value === num.toString() ? "contained" : "outlined"}
                        size="small"
                        sx={{ minWidth: 44, minHeight: 44 }}
                    >
                        {num}
                    </Button>
                ))}
            </ButtonGroup>
            <IconButton
                onClick={() => {
                    const current = parseInt(value) || 0;
                    if (current > 0) setValue((current - 1).toString());
                }}
                disabled={!value || parseInt(value) <= 0}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Remove fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => {
                    const current = parseInt(value) || 0;
                    setValue((current + 1).toString());
                }}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Add fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => setValue("")}
                disabled={!value}
                title={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const quantityFeature = defineModifier({
    id: "quantity",
    initial: "" as string,
    isEmpty: (value) => value.trim() === "",
    renderToggle: (slot) => <QuantityToggle {...slot} />,
    renderPanel: (slot) => <QuantityPanel {...slot} />,
    renderChip: (slot) => <QuantityChip {...slot} />,
});
