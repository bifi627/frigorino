import {
    alpha,
    Autocomplete,
    Box,
    createFilterOptions,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { highlightMatch } from "../highlightMatch";
import type { Suggestion, SuggestionsConfig } from "../types";

interface ComposerTextFieldProps {
    text: string;
    onTextChange: (value: string) => void;
    onEnter: () => void;
    inputRef: React.RefObject<HTMLInputElement | null>;
    placeholder: string;
    disabled: boolean;
    /** Inline status under the field (e.g. duplicate notice). */
    message?: string;
    /** Color of the inline status: orange notice/block vs green restore. */
    messageTone?: "warning" | "success";
    suggestions?: SuggestionsConfig;
}

export const ComposerTextField = ({
    text,
    onTextChange,
    onEnter,
    inputRef,
    placeholder,
    disabled,
    message,
    messageTone = "warning",
    suggestions,
}: ComposerTextFieldProps) => {
    const { t } = useTranslation();
    const minChars = suggestions?.minChars ?? 3;

    // Default matchFrom is "any" (substring) — pairs with the contains filter in
    // useItemComposer so middle/end matches surface and get highlighted.
    const filter = useMemo(
        () =>
            createFilterOptions<Suggestion>({
                stringify: (option) => option.label,
                limit: 5,
            }),
        [],
    );

    const options = useMemo(
        () =>
            suggestions && text.trim().length >= minChars
                ? suggestions.getItems(text)
                : [],
        [suggestions, text, minChars],
    );

    const helperColor =
        messageTone === "success" ? "success.main" : "warning.main";

    return (
        <Box sx={{ flex: 1 }}>
            <Autocomplete
                data-testid="autocomplete-input-textfield"
                freeSolo
                options={options}
                getOptionLabel={(option) =>
                    typeof option === "string" ? option : option.label
                }
                filterOptions={(opts, params) =>
                    params.inputValue.trim().length < minChars
                        ? []
                        : filter(opts, params)
                }
                inputValue={text}
                onInputChange={(_, value, reason) => {
                    // Only react to real typing. MUI fires a "reset" event on
                    // mount (and on selection) that would otherwise wipe a
                    // seeded value when editing an item; selections are handled
                    // in onChange below.
                    if (reason === "input") {
                        onTextChange(value);
                    }
                }}
                onChange={(_, value) => {
                    if (value && typeof value !== "string") {
                        if (suggestions?.onSelect) {
                            suggestions.onSelect(value);
                        } else {
                            onTextChange(value.label);
                        }
                    }
                }}
                noOptionsText={
                    text.trim().length >= minChars
                        ? t("common.noMatchingItems")
                        : t("common.typeAtLeastCharacters")
                }
                renderOption={(props, option) => (
                    <Box
                        component="li"
                        {...props}
                        key={option.id}
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 1,
                            mx: 0.5,
                            px: 1.5,
                            py: 1,
                            borderRadius: 2,
                            "&:hover, &.Mui-focused": {
                                bgcolor: (theme) =>
                                    alpha(theme.palette.primary.main, 0.14),
                            },
                        }}
                    >
                        <Box
                            sx={{
                                flex: 1,
                                minWidth: 0,
                                display: "flex",
                                alignItems: "center",
                                gap: 0.5,
                            }}
                        >
                            <Typography variant="body2" component="span" noWrap>
                                {highlightMatch(option.label, text)}
                            </Typography>
                            {option.badge}
                        </Box>
                        {option.secondaryLabel && (
                            <Typography
                                variant="caption"
                                sx={{
                                    color: "text.disabled",
                                    whiteSpace: "nowrap",
                                    flexShrink: 0,
                                }}
                            >
                                {option.secondaryLabel}
                            </Typography>
                        )}
                    </Box>
                )}
                renderInput={(params) => (
                    <TextField
                        {...params}
                        fullWidth
                        // Render as a single-row <textarea> rather than an
                        // <input>. Android keyboards (e.g. SwiftKey) only show
                        // their autofill toolbar (passwords/cards/addresses) for
                        // single-line <input> fields; a textarea is never treated
                        // as autofillable, so this suppresses that bar. Enter is
                        // already intercepted below to submit (preventDefault), so
                        // no newline is inserted.
                        multiline
                        minRows={1}
                        maxRows={1}
                        variant="outlined"
                        placeholder={placeholder}
                        disabled={disabled}
                        inputRef={inputRef}
                        helperText={message}
                        onKeyDown={(event) => {
                            if (event.key === "Enter" && !event.shiftKey) {
                                event.preventDefault();
                                event.stopPropagation();
                                onEnter();
                            }
                        }}
                        slotProps={{
                            ...params.slotProps,
                            input: {
                                ...params.slotProps.input,
                                sx: {
                                    "& .MuiOutlinedInput-notchedOutline": {
                                        border: "none",
                                    },
                                    "& .MuiInputBase-input": { py: 1 },
                                },
                            },
                            formHelperText: {
                                sx: { color: helperColor, ml: 1.5, mt: 0.25 },
                            },
                        }}
                        sx={{ "& .MuiOutlinedInput-root": { p: 0 } }}
                    />
                )}
                slotProps={{
                    paper: {
                        sx: {
                            mt: 0.75,
                            bgcolor: "background.default",
                            border: "1px solid",
                            borderColor: (theme) =>
                                alpha(theme.palette.primary.main, 0.55),
                            borderRadius: 3,
                            boxShadow: 8,
                            overflow: "hidden",
                        },
                    },
                    listbox: { sx: { py: 0.5 } },
                }}
                sx={{
                    "& .MuiAutocomplete-popupIndicator": { display: "none" },
                    "& .MuiAutocomplete-clearIndicator": { display: "none" },
                }}
            />
        </Box>
    );
};

ComposerTextField.displayName = "ComposerTextField";
