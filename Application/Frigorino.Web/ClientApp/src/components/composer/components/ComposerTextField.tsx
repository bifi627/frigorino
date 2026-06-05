import {
    Autocomplete,
    Box,
    createFilterOptions,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { Suggestion, SuggestionsConfig } from "../types";

interface ComposerTextFieldProps {
    text: string;
    onTextChange: (value: string) => void;
    onEnter: () => void;
    inputRef: React.RefObject<HTMLInputElement | null>;
    placeholder: string;
    disabled: boolean;
    errorMessage?: string;
    suggestions?: SuggestionsConfig;
}

export const ComposerTextField = ({
    text,
    onTextChange,
    onEnter,
    inputRef,
    placeholder,
    disabled,
    errorMessage,
    suggestions,
}: ComposerTextFieldProps) => {
    const { t } = useTranslation();
    const minChars = suggestions?.minChars ?? 3;

    const filter = useMemo(
        () =>
            createFilterOptions<Suggestion>({
                stringify: (option) => option.label,
                matchFrom: "start",
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
                    <Box component="li" {...props} key={option.id}>
                        <Box
                            sx={{
                                display: "flex",
                                flexDirection: "column",
                                width: "100%",
                            }}
                        >
                            <Box sx={{ display: "flex", alignItems: "center" }}>
                                <Typography variant="body2" component="span">
                                    {option.label}
                                </Typography>
                                {option.badge}
                            </Box>
                            {option.secondaryLabel && (
                                <Typography
                                    variant="caption"
                                    sx={{ color: "text.secondary" }}
                                >
                                    {option.secondaryLabel}
                                </Typography>
                            )}
                        </Box>
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
                        error={Boolean(errorMessage)}
                        helperText={errorMessage}
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
                        }}
                        sx={{ "& .MuiOutlinedInput-root": { p: 0 } }}
                    />
                )}
                sx={{
                    "& .MuiAutocomplete-popupIndicator": { display: "none" },
                    "& .MuiAutocomplete-clearIndicator": { display: "none" },
                }}
            />
        </Box>
    );
};

ComposerTextField.displayName = "ComposerTextField";
