import { Box, Card, CardContent, Stack, TextField, Typography } from "@mui/material";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { useUpdateRecipe } from "../useUpdateRecipe";

const SAVE_DEBOUNCE_MS = 600;

interface EditRecipeFormProps {
    householdId: number;
    recipe: RecipeResponse;
}

export const EditRecipeForm = ({
    householdId,
    recipe,
}: EditRecipeFormProps) => {
    const { t } = useTranslation();
    const updateRecipeMutation = useUpdateRecipe();

    // Seeded once on mount. The parent keys this form by recipe.id, so switching to a different
    // recipe remounts and reseeds — no reset-on-prop effect (which would also clobber edits).
    const [editedName, setEditedName] = useState(recipe.name || "");
    const [editedDescription, setEditedDescription] = useState(
        recipe.description ?? "",
    );
    const [editedServings, setEditedServings] = useState(
        recipe.servings != null ? String(recipe.servings) : "",
    );
    const [dirty, setDirty] = useState(false);

    const nameInvalid = editedName.trim().length === 0;
    const servingsNum = editedServings === "" ? null : Number(editedServings);
    const servingsInvalid =
        editedServings !== "" &&
        (!Number.isInteger(servingsNum) ||
            (servingsNum as number) < 1 ||
            (servingsNum as number) > 99);

    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    // Latest field state, read by the debounced/blur flush without re-creating the timer.
    const latest = useRef({
        name: editedName,
        description: editedDescription,
        servings: editedServings,
        nameInvalid,
        servingsInvalid,
        dirty,
    });
    // Keep latest.current in sync after every render (useLayoutEffect so it's updated before
    // any pending debounce timer or blur handler fires).
    useLayoutEffect(() => {
        latest.current = {
            name: editedName,
            description: editedDescription,
            servings: editedServings,
            nameInvalid,
            servingsInvalid,
            dirty,
        };
    });

    const { mutate } = updateRecipeMutation;
    const recipeId = recipe.id;

    const save = useCallback(() => {
        if (!recipeId) return;
        const cur = latest.current;
        if (cur.nameInvalid || cur.servingsInvalid) return;
        if (!cur.dirty) return;
        mutate(
            {
                path: { householdId, recipeId },
                body: {
                    name: cur.name.trim(),
                    description: cur.description.trim() || null,
                    servings: cur.servings === "" ? null : Number(cur.servings),
                },
            },
            { onSuccess: () => setDirty(false) },
        );
    }, [householdId, recipeId, mutate]);

    const scheduleSave = useCallback(() => {
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(() => {
            timerRef.current = null;
            save();
        }, SAVE_DEBOUNCE_MS);
    }, [save]);

    const flushSave = useCallback(() => {
        if (timerRef.current) {
            clearTimeout(timerRef.current);
            timerRef.current = null;
        }
        save();
    }, [save]);

    useEffect(
        () => () => {
            if (timerRef.current) clearTimeout(timerRef.current);
        },
        [],
    );

    let status: "saving" | "saved" | "idle" = "idle";
    if (updateRecipeMutation.isPending) {
        status = "saving";
    } else if (!dirty && updateRecipeMutation.isSuccess) {
        status = "saved";
    }

    return (
        <Card elevation={4}>
            <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                <Stack spacing={3}>
                    <TextField
                        label={t("recipes.recipeName")}
                        value={editedName}
                        onChange={(e) => {
                            setEditedName(e.target.value);
                            setDirty(true);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        fullWidth
                        required
                        error={nameInvalid}
                        helperText={
                            nameInvalid ? t("recipes.recipeNameRequired") : ""
                        }
                    />

                    <TextField
                        label={t("recipes.description")}
                        value={editedDescription}
                        onChange={(e) => {
                            setEditedDescription(e.target.value);
                            setDirty(true);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        fullWidth
                        multiline
                        minRows={2}
                        placeholder={t("recipes.descriptionPlaceholder")}
                        slotProps={{
                            htmlInput: {
                                maxLength: 1000,
                                "data-testid": "recipe-description-input",
                            },
                        }}
                    />

                    <TextField
                        type="number"
                        label={t("recipes.servings")}
                        value={editedServings}
                        onChange={(e) => {
                            setEditedServings(e.target.value);
                            setDirty(true);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        sx={{ width: 140 }}
                        error={servingsInvalid}
                        helperText={
                            servingsInvalid ? t("recipes.servingsRange") : ""
                        }
                        slotProps={{
                            htmlInput: {
                                min: 1,
                                max: 99,
                                "data-testid": "recipe-servings-input",
                            },
                        }}
                    />

                    <Box
                        data-testid="recipe-metadata-status"
                        data-status={status}
                        sx={{ minHeight: 20 }}
                    >
                        <Typography variant="caption" color="text.secondary">
                            {status === "saving"
                                ? t("common.saving")
                                : status === "saved"
                                  ? t("common.saved")
                                  : ""}
                        </Typography>
                    </Box>
                </Stack>
            </CardContent>
        </Card>
    );
};
