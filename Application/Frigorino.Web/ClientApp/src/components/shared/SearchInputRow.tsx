import { Close } from "@mui/icons-material";
import {
    Container,
    IconButton,
    InputAdornment,
    TextField,
} from "@mui/material";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { featureContentPx } from "../../theme";

interface SearchInputRowProps {
    open: boolean;
    query: string;
    onQueryChange: (value: string) => void;
    onClose: () => void;
    placeholder?: string;
    /** Test-id stem: renders `${testIdPrefix}-input` and `${testIdPrefix}-clear`. */
    testIdPrefix: string;
}

export const SearchInputRow = ({
    open,
    query,
    onQueryChange,
    onClose,
    placeholder,
    testIdPrefix,
}: SearchInputRowProps) => {
    const { t } = useTranslation();
    const inputRef = useRef<HTMLInputElement>(null);

    // rAF so focus lands after the row mounts (mirrors the composer autofocus pattern).
    useEffect(() => {
        if (!open) {
            return;
        }
        const id = requestAnimationFrame(() => inputRef.current?.focus());
        return () => cancelAnimationFrame(id);
    }, [open]);

    if (!open) {
        return null;
    }

    return (
        <Container
            maxWidth="sm"
            sx={{ px: featureContentPx, pb: 1, flexShrink: 0 }}
        >
            <TextField
                inputRef={inputRef}
                value={query}
                onChange={(event) => onQueryChange(event.target.value)}
                placeholder={placeholder ?? t("common.search")}
                size="small"
                fullWidth
                slotProps={{
                    htmlInput: { "data-testid": `${testIdPrefix}-input` },
                    input: {
                        endAdornment: (
                            <InputAdornment position="end">
                                <IconButton
                                    size="small"
                                    onClick={onClose}
                                    data-testid={`${testIdPrefix}-clear`}
                                    aria-label={t("common.search")}
                                >
                                    <Close fontSize="small" />
                                </IconButton>
                            </InputAdornment>
                        ),
                    },
                }}
            />
        </Container>
    );
};
