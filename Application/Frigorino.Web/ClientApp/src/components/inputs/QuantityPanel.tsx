import { Add, Remove, ShoppingBag } from "@mui/icons-material";
import {
    Box,
    Button,
    ButtonGroup,
    IconButton,
    TextField,
    Typography,
} from "@mui/material";
import { t } from "i18next";
import { memo, useRef } from "react";

interface QuantityToggleProps {
    value: string;
    onToggle: () => void;
    active: boolean;
}

export const QuantityToggle = memo(
    ({ value, onToggle, active }: QuantityToggleProps) => {
        return (
            <IconButton onClick={onToggle} size="small">
                {value ? (
                    <Typography
                        variant="caption"
                        sx={{
                            fontWeight: "bold",
                            color: "primary.main",
                            minWidth: "30px",
                        }}
                    >
                        {value}
                    </Typography>
                ) : (
                    <>
                        <ShoppingBag
                            fontSize="small"
                            sx={{ color: active ? "primary.main" : "inherit" }}
                        />
                    </>
                )}
            </IconButton>
        );
    },
);

QuantityToggle.displayName = "QuantityToggle";

interface QuantityPanelProps {
    value: string;
    onChange: (value: string) => void;
    isLoading?: boolean;
    onKeyPress?: (event: React.KeyboardEvent) => void;
}

export const QuantityPanel = memo(
    ({
        value,
        onChange,
        isLoading = false,
        onKeyPress,
    }: QuantityPanelProps) => {
        const quantityInputRef = useRef<HTMLInputElement>(null);

        return (
            <Box
                className="quantity-section"
                sx={{
                    display: "flex",
                    gap: 0.75,
                    alignItems: "center",
                }}
            >
                <TextField
                    fullWidth
                    variant="outlined"
                    placeholder={t("lists.quantity")}
                    value={value}
                    onChange={(e) => onChange(e.target.value)}
                    onKeyDown={onKeyPress}
                    onClick={(e) => e.stopPropagation()}
                    disabled={isLoading}
                    size="small"
                    inputRef={quantityInputRef}
                    sx={{
                        "& .MuiOutlinedInput-root": {
                            borderRadius: 2,
                        },
                    }}
                    InputProps={{
                        sx: {
                            "& .MuiOutlinedInput-notchedOutline": {
                                border: "1px solid",
                                borderColor: "divider",
                            },
                            "& .MuiInputBase-input": {
                                py: 0.75,
                            },
                            "&:hover .MuiOutlinedInput-notchedOutline": {
                                borderColor: "primary.main",
                            },
                            "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
                                borderColor: "primary.main",
                                borderWidth: 2,
                            },
                        },
                    }}
                />

                {/* Quick Quantity Buttons */}
                <ButtonGroup variant="outlined" size="small">
                    {[1, 2, 5].map((num) => (
                        <Button
                            key={num}
                            onClick={() => onChange(num.toString())}
                            variant={
                                value === num.toString()
                                    ? "contained"
                                    : "outlined"
                            }
                            size="small"
                            sx={{
                                minWidth: 40,
                                backgroundColor:
                                    value === num.toString()
                                        ? "primary.main"
                                        : "transparent",
                                color:
                                    value === num.toString()
                                        ? "white"
                                        : "text.primary",
                            }}
                        >
                            {num}
                        </Button>
                    ))}
                </ButtonGroup>

                {/* Quantity Adjustment Buttons */}
                <IconButton
                    size="small"
                    onClick={() => {
                        const current = parseInt(value) || 0;
                        if (current > 0) onChange((current - 1).toString());
                    }}
                    disabled={!value || parseInt(value) <= 0}
                >
                    <Remove fontSize="small" />
                </IconButton>
                <IconButton
                    size="small"
                    onClick={() => {
                        const current = parseInt(value) || 0;
                        onChange((current + 1).toString());
                    }}
                >
                    <Add fontSize="small" />
                </IconButton>
            </Box>
        );
    },
);

// Add display name for debugging
QuantityPanel.displayName = "QuantityPanel";
