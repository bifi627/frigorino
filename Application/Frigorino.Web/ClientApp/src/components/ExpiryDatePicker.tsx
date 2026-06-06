import { Box } from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import { formatIsoDate, parseLocalDate } from "../utils/dateUtils";

interface ExpiryDatePickerProps {
    /** Calendar date as "YYYY-MM-DD", or null when unset. */
    value: string | null;
    /** Emits a valid "YYYY-MM-DD", or null while empty/incomplete. */
    onChange: (value: string | null) => void;
    label?: string;
    disabled?: boolean;
    error?: boolean;
    helperText?: string;
    fullWidth?: boolean;
    /**
     * Test handle placed on the wrapping element. The MUI X field renders its value into a
     * hidden, aria-hidden <input> while editing happens in a separate visible "sections"
     * container, so a testid on the input itself isn't clickable — Playwright clicks this
     * wrapper to focus the field, then types.
     */
    dataTestId?: string;
}

// Single source of truth for expiry entry: a typeable masked field AND a calendar popover.
// Converts the app's "YYYY-MM-DD" string at the boundary so no Date ever leaks into state
// (avoids UTC day-shift bugs). Emits null until the typed value is a complete valid date.
export const ExpiryDatePicker = ({
    value,
    onChange,
    label,
    disabled,
    error,
    helperText,
    fullWidth,
    dataTestId,
}: ExpiryDatePickerProps) => {
    const dateValue = value ? parseLocalDate(value) : null;
    return (
        <Box
            data-testid={dataTestId}
            sx={{ width: fullWidth ? "100%" : undefined }}
        >
            <DatePicker
                label={label}
                value={dateValue}
                disabled={disabled}
                onChange={(next) => onChange(formatIsoDate(next))}
                slotProps={{
                    field: { clearable: true },
                    textField: {
                        fullWidth,
                        size: "small",
                        error,
                        helperText,
                    },
                }}
            />
        </Box>
    );
};
