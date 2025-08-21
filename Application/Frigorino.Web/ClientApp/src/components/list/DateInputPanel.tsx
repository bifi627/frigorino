import { CalendarToday, Clear, Today } from "@mui/icons-material";
import {
    Box,
    IconButton,
    TextField,
    Typography,
} from "@mui/material";
import { memo, useRef } from "react";

interface DateToggleProps {
    value: Date | null;
    onToggle: () => void;
    active: boolean;
}

export const DateToggle = memo(
    ({ value, onToggle, active }: DateToggleProps) => {
        const formatDateForDisplay = (date: Date | null) => {
            if (!date) return "";
            return date.toLocaleDateString("de-DE", {
                day: "2-digit",
                month: "2-digit",
            });
        };

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
                        {formatDateForDisplay(value)}
                    </Typography>
                ) : (
                    <>
                        <CalendarToday
                            fontSize="small"
                            sx={{ color: active ? "primary.main" : "inherit" }}
                        />
                    </>
                )}
            </IconButton>
        );
    },
);

DateToggle.displayName = "DateToggle";

interface DateInputPanelProps {
    value: Date | null;
    onChange: (value: Date | null) => void;
    isLoading?: boolean;
    onKeyPress?: (event: React.KeyboardEvent) => void;
}

export const DateInputPanel = memo(
    ({
        value,
        onChange,
        isLoading = false,
        onKeyPress,
    }: DateInputPanelProps) => {
        const dateInputRef = useRef<HTMLInputElement>(null);

        const formatDateForInput = (date: Date | null) => {
            if (!date) return "";
            return date.toISOString().split("T")[0];
        };

        const handleDateChange = (dateString: string) => {
            if (!dateString) {
                onChange(null);
                return;
            }
            const date = new Date(dateString);
            if (isNaN(date.getTime())) {
                onChange(null);
                return;
            }
            onChange(date);
        };

        return (
            <Box
                className="date-section"
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 0.75,
                    width: "100%",
                    p: 1,
                }}
            >
                {/* Date Input Field */}
                <TextField
                    fullWidth
                    variant="outlined"
                    placeholder="Datum"
                    type="date"
                    value={formatDateForInput(value)}
                    onChange={(e) => handleDateChange(e.target.value)}
                    onKeyPress={onKeyPress}
                    onClick={(e) => e.stopPropagation()}
                    disabled={isLoading}
                    size="medium"
                    inputRef={dateInputRef}
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

                {/* Action Buttons Inline */}
                <IconButton
                    size="small"
                    onClick={() => onChange(new Date())}
                    title="Heute"
                >
                    <Today fontSize="small" />
                </IconButton>

                <IconButton
                    size="small"
                    onClick={() => onChange(null)}
                    disabled={!value}
                    title="Löschen"
                >
                    <Clear fontSize="small" />
                </IconButton>
            </Box>
        );
    },
);

// Add display name for debugging
DateInputPanel.displayName = "DateInputPanel";