import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import type { InputHTMLAttributes } from "react";
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
    /** Forwarded to the underlying <input> so Playwright can target/type into it. */
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
    // data-* attributes are valid on the rendered <input> at runtime, but React's typed
    // htmlInput slot (InputHTMLAttributes) doesn't model them outside JSX — hence the cast.
    const htmlInput = dataTestId
        ? ({
              "data-testid": dataTestId,
          } as unknown as InputHTMLAttributes<HTMLInputElement>)
        : undefined;
    return (
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
                    slotProps: { htmlInput },
                },
            }}
        />
    );
};
