/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components, mirroring the
   existing pattern in components/inputs/context/AddInputContext.tsx. */
import { Add, Remove, ShoppingBag } from "@mui/icons-material";
import {
    Box,
    Button,
    ButtonGroup,
    IconButton,
    TextField,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const QuantityToggle = ({ value, open, toggleOpen }: FeatureSlot<string>) => (
    <IconButton onClick={toggleOpen} size="small">
        {value ? (
            <Typography
                variant="caption"
                sx={{ fontWeight: "bold", color: "primary.main", minWidth: "30px" }}
            >
                {value}
            </Typography>
        ) : (
            <ShoppingBag
                fontSize="small"
                sx={{ color: open ? "primary.main" : "inherit" }}
            />
        )}
    </IconButton>
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
                        sx={{ minWidth: 40 }}
                    >
                        {num}
                    </Button>
                ))}
            </ButtonGroup>
            <IconButton
                size="small"
                onClick={() => {
                    const current = parseInt(value) || 0;
                    if (current > 0) setValue((current - 1).toString());
                }}
                disabled={!value || parseInt(value) <= 0}
            >
                <Remove fontSize="small" />
            </IconButton>
            <IconButton
                size="small"
                onClick={() => {
                    const current = parseInt(value) || 0;
                    setValue((current + 1).toString());
                }}
            >
                <Add fontSize="small" />
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
});
