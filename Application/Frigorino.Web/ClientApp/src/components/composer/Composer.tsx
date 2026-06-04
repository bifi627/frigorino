import { Close } from "@mui/icons-material";
import { Box, Collapse, IconButton } from "@mui/material";
import { useCallback, useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { ComposerTextField } from "./components/ComposerTextField";
import { EditHeader } from "./components/EditHeader";
import { SendButton } from "./components/SendButton";
import {
    isModifierValueEmpty,
    useComposerState,
} from "./hooks/useComposerState";
import type {
    AnyActionFeature,
    AnyFeature,
    AnyModifierFeature,
    Completion,
    ComposerProps,
    FeatureSlot,
} from "./types";

export function Composer<const F extends readonly AnyFeature[] = []>({
    features,
    onComplete,
    placeholder,
    disabled = false,
    editing,
    initialDraft,
    initialOpenId,
    suggestions,
    duplicate,
}: ComposerProps<F>) {
    const { t } = useTranslation();

    const featureList = useMemo<readonly AnyFeature[]>(
        () => features ?? [],
        [features],
    );

    const {
        text,
        setText,
        values,
        setValue,
        openId,
        toggleOpen,
        inputRef,
        focusInput,
        reset,
    } = useComposerState({
        features: featureList,
        initialDraft,
        initialOpenId,
    });

    const isEditing = editing?.active ?? false;
    const trimmed = text.trim();

    const dup = useMemo(
        () => (duplicate && trimmed ? duplicate.check(trimmed) : null),
        [duplicate, trimmed],
    );
    const blocked = Boolean(dup?.block);

    const modifiersValid = useMemo(
        () =>
            featureList
                .filter((f): f is AnyModifierFeature => f.kind === "modifier")
                .every((f) => f.isValid?.(values[f.id]) ?? true),
        [featureList, values],
    );

    const completeText = useCallback(() => {
        if (!trimmed || !modifiersValid) {
            return;
        }
        if (dup) {
            if (dup.onResolve) {
                dup.onResolve();
                reset();
                requestAnimationFrame(focusInput);
                return;
            }
            if (dup.block) {
                return;
            }
        }
        const completion = {
            kind: "text",
            mode: isEditing ? "edit" : "create",
            text: trimmed,
            ...values,
        } as Completion<F>;
        onComplete(completion);
        reset();
        requestAnimationFrame(focusInput);
    }, [
        trimmed,
        modifiersValid,
        dup,
        isEditing,
        values,
        onComplete,
        reset,
        focusInput,
    ]);

    const completeAction = useCallback(
        (id: string, payload: Record<string, unknown>) => {
            onComplete({ kind: id, ...payload } as Completion<F>);
            reset();
        },
        [onComplete, reset],
    );

    const handleCancelEdit = () => {
        reset();
        editing?.onCancel();
        focusInput();
    };

    // When entering edit mode, focus the field so editing can start immediately and
    // the mobile keyboard opens. The composer is remounted per edited item (keyed by
    // id in the footer), so this runs once on entering edit. Skipped when a modifier
    // panel is opened instead (e.g. editing a comment/quantity via its chip) so we
    // don't pull focus from that panel, and skipped in add mode so the keyboard
    // doesn't pop open on every list load. rAF lets the input mount before focusing.
    useEffect(() => {
        if (!isEditing || initialOpenId) {
            return;
        }
        const raf = requestAnimationFrame(focusInput);
        return () => cancelAnimationFrame(raf);
    }, [isEditing, initialOpenId, focusInput]);

    const handleContainerClick = (event: React.MouseEvent) => {
        if ((event.target as HTMLElement).closest(".composer-panel")) {
            return;
        }
        focusInput();
    };

    // Keep the mobile soft keyboard open. Tapping any non-input control inside the
    // composer (icon buttons, chips, padding) would by default move focus off the
    // focused text field and collapse the keyboard, making the layout jump.
    // preventDefault on mousedown keeps focus where it is while still firing the
    // control's click. Real text inputs (the field, the comment textarea) are
    // excluded so they can take focus normally.
    const keepKeyboardOpen = (event: React.MouseEvent) => {
        const target = event.target as HTMLElement;
        const isTextInput = target.closest(
            "input, textarea, [contenteditable='true']",
        );
        if (isTextInput) {
            return;
        }
        event.preventDefault();
    };

    const modifierFeatures = useMemo(
        () =>
            featureList.filter(
                (f): f is AnyModifierFeature => f.kind === "modifier",
            ),
        [featureList],
    );
    const actionFeatures = useMemo(
        () =>
            featureList.filter(
                (f): f is AnyActionFeature => f.kind === "action",
            ),
        [featureList],
    );

    const chipFeatures = modifierFeatures.filter(
        (feature) =>
            feature.renderChip &&
            // While a modifier's panel is open its field already shows the value,
            // so suppress the summary chip to avoid showing it twice. The chip
            // returns as the summary once the panel closes.
            openId !== feature.id &&
            !isModifierValueEmpty(feature, values[feature.id]),
    );

    const slotFor = (feature: AnyModifierFeature): FeatureSlot<unknown> => ({
        value: values[feature.id],
        setValue: (value) => setValue(feature.id, value),
        open: openId === feature.id,
        toggleOpen: () => toggleOpen(feature.id),
        disabled,
    });

    const fieldPlaceholder =
        placeholder ??
        (isEditing ? t("common.editItem") : t("common.addItemPlaceholder"));

    return (
        <Box
            onClick={handleContainerClick}
            onMouseDown={keepKeyboardOpen}
            sx={{
                width: "100%",
                cursor: "text",
            }}
        >
            {isEditing && editing && <EditHeader label={editing.label} />}

            {chipFeatures.length > 0 && (
                <Box
                    sx={{
                        display: "flex",
                        flexWrap: "wrap",
                        gap: 0.5,
                        mb: 0.5,
                    }}
                >
                    {chipFeatures.map((feature) => (
                        <Box
                            key={feature.id}
                            className="composer-panel"
                            data-testid={`composer-chip-${feature.id}`}
                            sx={{
                                display: "inline-flex",
                                alignItems: "center",
                            }}
                        >
                            {feature.renderChip?.(slotFor(feature))}
                        </Box>
                    ))}
                </Box>
            )}

            {modifierFeatures.map((feature) =>
                feature.renderPanel ? (
                    <Collapse
                        key={feature.id}
                        className="composer-panel"
                        in={openId === feature.id}
                    >
                        <Box
                            sx={{ mb: 0.5 }}
                            data-testid={`composer-panel-${feature.id}`}
                        >
                            {feature.renderPanel(slotFor(feature))}
                        </Box>
                    </Collapse>
                ) : null,
            )}

            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                <Box
                    sx={{
                        flex: 1,
                        minWidth: 0,
                        display: "flex",
                        alignItems: "center",
                        gap: 0.25,
                        pl: 1.5,
                        pr: 0.5,
                        bgcolor: "action.hover",
                        // Pill shape (≈12px). Distinct from card radius on purpose.
                        borderRadius: 3,
                        // The pill is the input surface now (no outer card), so it
                        // carries the edit/focus highlight border.
                        border: "1px solid",
                        borderColor: isEditing ? "warning.main" : "primary.200",
                        transition: "border-color 0.2s ease",
                        "&:hover, &:focus-within": {
                            borderColor: isEditing
                                ? "warning.dark"
                                : "primary.main",
                        },
                        // Inline icons read as in-field adornments, not standalone
                        // 44px buttons — overrides the per-feature minWidth/minHeight.
                        "& .MuiButtonBase-root": {
                            minWidth: 38,
                            minHeight: 38,
                        },
                    }}
                >
                    <ComposerTextField
                        text={text}
                        onTextChange={setText}
                        onEnter={completeText}
                        inputRef={inputRef}
                        placeholder={fieldPlaceholder}
                        disabled={disabled}
                        errorMessage={dup?.message}
                        suggestions={suggestions}
                    />

                    {modifierFeatures.map((feature) =>
                        feature.renderToggle ? (
                            <Box
                                key={feature.id}
                                className="composer-panel"
                                data-testid={`composer-toggle-${feature.id}`}
                            >
                                {feature.renderToggle(slotFor(feature))}
                            </Box>
                        ) : null,
                    )}

                    {!trimmed &&
                        actionFeatures.map((feature) => (
                            <Box key={feature.id} className="composer-panel">
                                {feature.renderTrigger({
                                    complete: (payload) =>
                                        completeAction(
                                            feature.id,
                                            payload as Record<string, unknown>,
                                        ),
                                    disabled,
                                })}
                            </Box>
                        ))}
                </Box>

                {isEditing && (
                    <IconButton
                        onClick={handleCancelEdit}
                        aria-label={t("common.cancel")}
                        sx={{
                            minWidth: 44,
                            minHeight: 44,
                            color: "text.secondary",
                            "&:hover": { bgcolor: "action.hover" },
                        }}
                    >
                        <Close />
                    </IconButton>
                )}

                <SendButton
                    onClick={completeText}
                    disabled={
                        !trimmed || disabled || blocked || !modifiersValid
                    }
                    editing={isEditing}
                    duplicate={Boolean(dup)}
                />
            </Box>
        </Box>
    );
}
