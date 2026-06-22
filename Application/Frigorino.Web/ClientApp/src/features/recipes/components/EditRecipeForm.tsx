import { Add, Remove, Restaurant } from "@mui/icons-material";
import { Box, IconButton, Stack, TextField, Typography } from "@mui/material";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { sectionColors } from "../../../theme";
import { useUpdateRecipe } from "../useUpdateRecipe";

const SAVE_DEBOUNCE_MS = 600;
const coral = sectionColors.recipes;

interface EditRecipeFormProps {
    householdId: number;
    recipe: RecipeResponse;
}

export const EditRecipeForm = ({ householdId, recipe }: EditRecipeFormProps) => {
    const { t } = useTranslation();
    const updateRecipeMutation = useUpdateRecipe();

    // Seeded once on mount; parent keys this by recipe.id so switching recipes remounts.
    const [editedName, setEditedName] = useState(recipe.name || "");
    const [editedDescription, setEditedDescription] = useState(
        recipe.description ?? "",
    );
    const [editedServings, setEditedServings] = useState<number | null>(
        recipe.servings ?? null,
    );
    const [dirty, setDirty] = useState(false);

    const nameInvalid = editedName.trim().length === 0;
    const servingsInvalid =
        editedServings !== null &&
        (!Number.isInteger(editedServings) ||
            editedServings < 1 ||
            editedServings > 99);

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
                    servings: cur.servings,
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

    // Stepper: clamp 1..99; unset (null) starts at 1 going up.
    const stepServings = useCallback(
        (delta: number) => {
            setEditedServings((prev) => {
                const base = prev ?? (delta > 0 ? 0 : 1);
                return Math.min(99, Math.max(1, base + delta));
            });
            setDirty(true);
            scheduleSave();
        },
        [scheduleSave],
    );

    let status: "saving" | "saved" | "idle" = "idle";
    if (updateRecipeMutation.isPending) {
        status = "saving";
    } else if (!dirty && updateRecipeMutation.isSuccess) {
        status = "saved";
    }

    return (
        <Box>
            <TextField
                variant="standard"
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
                helperText={nameInvalid ? t("recipes.recipeNameRequired") : ""}
                placeholder={t("recipes.recipeName")}
                slotProps={{
                    input: {
                        sx: {
                            fontSize: "1.7rem",
                            fontWeight: 700,
                            lineHeight: 1.2,
                        },
                    },
                    htmlInput: { "data-testid": "recipe-name-input" },
                }}
            />

            <Stack
                direction="row"
                spacing={0.5}
                sx={{
                    alignItems: "center",
                    border: 1,
                    borderColor: servingsInvalid ? "error.main" : "divider",
                    borderRadius: 999,
                    pl: 1.25,
                    pr: 0.25,
                    py: 0.25,
                    mt: 1.5,
                    width: "fit-content",
                }}
                data-testid="recipe-servings-stepper"
            >
                <Restaurant fontSize="small" sx={{ color: coral }} />
                <Typography
                    variant="body2"
                    sx={{ fontWeight: 700, minWidth: 16, textAlign: "center" }}
                    data-testid="recipe-servings-value"
                >
                    {editedServings ?? "–"}
                </Typography>
                <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ mr: 0.5 }}
                >
                    {t("recipes.servings")}
                </Typography>
                <IconButton
                    size="small"
                    onClick={() => stepServings(-1)}
                    disabled={editedServings !== null && editedServings <= 1}
                    data-testid="recipe-servings-decrement"
                >
                    <Remove fontSize="small" />
                </IconButton>
                <IconButton
                    size="small"
                    onClick={() => stepServings(1)}
                    disabled={editedServings !== null && editedServings >= 99}
                    data-testid="recipe-servings-increment"
                >
                    <Add fontSize="small" />
                </IconButton>
            </Stack>

            <TextField
                variant="standard"
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
                sx={{ mt: 1.5 }}
                slotProps={{
                    input: {
                        sx: {
                            fontSize: "0.875rem",
                            fontStyle: "italic",
                            color: "text.secondary",
                        },
                    },
                    htmlInput: {
                        maxLength: 1000,
                        "data-testid": "recipe-description-input",
                    },
                }}
            />

            <Box
                data-testid="recipe-metadata-status"
                data-status={status}
                sx={{ minHeight: 20, mt: 0.5 }}
            >
                <Typography variant="caption" color="text.secondary">
                    {status === "saving"
                        ? t("common.saving")
                        : status === "saved"
                          ? t("common.saved")
                          : ""}
                </Typography>
            </Box>
        </Box>
    );
};
