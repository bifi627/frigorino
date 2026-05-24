import { Delete } from "@mui/icons-material";
import { Box, Collapse, IconButton, Paper } from "@mui/material";
import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { ComposerTextField } from "./components/ComposerTextField";
import { EditHeader } from "./components/EditHeader";
import { SendButton } from "./components/SendButton";
import { isModifierValueEmpty, useComposerState } from "./hooks/useComposerState";
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
    suggestions,
    duplicate,
}: ComposerProps<F>) {
    const { t } = useTranslation();

    const featureList = useMemo<readonly AnyFeature[]>(
        () => features ?? [],
        [features],
    );

    const { text, setText, values, setValue, openId, toggleOpen, inputRef, focusInput, reset } =
        useComposerState({ features: featureList, initialDraft });

    const isEditing = editing?.active ?? false;
    const trimmed = text.trim();

    const dup = useMemo(
        () => (duplicate && trimmed ? duplicate.check(trimmed) : null),
        [duplicate, trimmed],
    );
    const blocked = Boolean(dup?.block);

    const completeText = useCallback(() => {
        if (!trimmed) {
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
    }, [trimmed, dup, isEditing, values, onComplete, reset, focusInput]);

    const completeAction = useCallback(
        (id: string, payload: Record<string, unknown>) => {
            onComplete({ kind: id, ...payload } as Completion<F>);
            reset();
        },
        [onComplete, reset],
    );

    const handleDiscard = () => {
        reset();
        focusInput();
    };

    const handleCancelEdit = () => {
        reset();
        editing?.onCancel();
        focusInput();
    };

    const handleContainerClick = (event: React.MouseEvent) => {
        if ((event.target as HTMLElement).closest(".composer-panel")) {
            return;
        }
        focusInput();
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
        <Paper
            elevation={3}
            onClick={handleContainerClick}
            sx={{
                width: "100%",
                p: 1,
                bgcolor: "background.paper",
                border: "1px solid",
                borderColor: isEditing ? "warning.main" : "primary.200",
                cursor: "text",
                transition: "all 0.3s ease",
                "&:hover, &:focus-within": {
                    borderColor: isEditing ? "warning.dark" : "primary.main",
                    boxShadow: 3,
                },
            }}
        >
            {isEditing && editing && (
                <EditHeader label={editing.label} onCancel={handleCancelEdit} />
            )}

            {chipFeatures.length > 0 && (
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5, mb: 0.5 }}>
                    {chipFeatures.map((feature) => (
                        <Box
                            key={feature.id}
                            className="composer-panel"
                            data-testid={`composer-chip-${feature.id}`}
                            sx={{ display: "inline-flex", alignItems: "center" }}
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
                {trimmed && !isEditing && (
                    <IconButton
                        onClick={handleDiscard}
                        title={t("common.discardInput")}
                        aria-label={t("common.discardInput")}
                        sx={{
                            minWidth: 44,
                            minHeight: 44,
                            color: "text.secondary",
                            bgcolor: "action.hover",
                            "&:hover": { color: "error.main", bgcolor: "error.50" },
                        }}
                    >
                        <Delete />
                    </IconButton>
                )}

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

                {actionFeatures.map((feature) => (
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

                <SendButton
                    onClick={completeText}
                    disabled={!trimmed || disabled || blocked}
                    editing={isEditing}
                    duplicate={Boolean(dup)}
                />
            </Box>
        </Paper>
    );
}
