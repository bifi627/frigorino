import { ContentCopy, Delete, Save } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    IconButton,
    Stack,
    TextField,
    Tooltip,
} from "@mui/material";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type {
    ProductCategory,
    SortBlueprintResponse,
} from "../../../lib/api/types.gen";
import { useCreateSortBlueprint } from "../useCreateSortBlueprint";
import { useDeleteSortBlueprint } from "../useDeleteSortBlueprint";
import { useUpdateSortBlueprint } from "../useUpdateSortBlueprint";
import { BlueprintEditor } from "./BlueprintEditor";

interface Props {
    householdId: number;
    // Existing blueprint, or null for an unsaved draft (create flow).
    blueprint: SortBlueprintResponse | null;
    onCreated?: () => void;
}

// Existing blueprints persist implicitly: renaming, reordering, adding or removing aisles
// auto-saves after a short debounce. Only a brand-new draft keeps an explicit Create button,
// since there is nothing to update until the row exists.
const AUTO_SAVE_DELAY_MS = 600;

export function BlueprintCard({ householdId, blueprint, onCreated }: Props) {
    const { t } = useTranslation();
    const create = useCreateSortBlueprint();
    const update = useUpdateSortBlueprint();
    const remove = useDeleteSortBlueprint();

    const [name, setName] = useState(blueprint?.name ?? "");
    const [included, setIncluded] = useState<ProductCategory[]>(
        blueprint?.categories ?? [],
    );

    const blueprintId = blueprint?.id;
    const canCreate =
        name.trim().length > 0 && included.length > 0 && !create.isPending;

    // Debounced auto-save for an existing blueprint. We compare against the last-persisted
    // snapshot (rather than a "skip first render" flag, which double-fires under StrictMode):
    // the prop-hydrated state matches the snapshot, so mounting never saves — only genuine
    // edits diverge and persist. While the state is momentarily invalid (blank name / no
    // aisles) we hold off, since the backend rejects those.
    const saveBlueprint = update.mutateAsync;
    const lastSaved = useRef(
        JSON.stringify({
            name: (blueprint?.name ?? "").trim(),
            categories: blueprint?.categories ?? [],
        }),
    );
    useEffect(() => {
        if (blueprintId == null) {
            return;
        }
        const trimmed = name.trim();
        if (trimmed.length === 0 || included.length === 0) {
            return;
        }
        const snapshot = JSON.stringify({
            name: trimmed,
            categories: included,
        });
        if (snapshot === lastSaved.current) {
            return;
        }
        const handle = window.setTimeout(() => {
            saveBlueprint({
                path: { householdId, blueprintId },
                body: { name: trimmed, categories: included },
            })
                .then(() => {
                    lastSaved.current = snapshot;
                })
                .catch(() => toast.error(t("blueprints.saveFailed")));
        }, AUTO_SAVE_DELAY_MS);
        return () => window.clearTimeout(handle);
    }, [name, included, blueprintId, householdId, saveBlueprint, t]);

    const handleCreate = async () => {
        try {
            await create.mutateAsync({
                path: { householdId },
                body: { name: name.trim(), categories: included },
            });
            toast.success(t("blueprints.saved"));
            onCreated?.();
        } catch {
            toast.error(t("blueprints.saveFailed"));
        }
    };

    const handleDuplicate = async () => {
        try {
            await create.mutateAsync({
                path: { householdId },
                body: {
                    name: `${name.trim()} ${t("blueprints.copySuffix")}`,
                    categories: included,
                },
            });
            toast.success(t("blueprints.saved"));
        } catch {
            toast.error(t("blueprints.saveFailed"));
        }
    };

    const handleDelete = async () => {
        if (!blueprint) {
            return;
        }
        try {
            // The hook surfaces the "deleted" toast with an Undo action on success.
            await remove.mutateAsync({
                path: { householdId, blueprintId: blueprint.id },
            });
        } catch {
            toast.error(t("blueprints.deleteFailed"));
        }
    };

    return (
        <Card
            elevation={2}
            sx={{ mb: { xs: 2, sm: 3 } }}
            data-testid="blueprint-card"
        >
            <CardContent>
                <Stack
                    direction="row"
                    spacing={1}
                    sx={{ alignItems: "center" }}
                >
                    <TextField
                        fullWidth
                        size="small"
                        label={t("blueprints.nameLabel")}
                        placeholder={t("blueprints.namePlaceholder")}
                        value={name}
                        disabled={create.isPending}
                        onChange={(e) => setName(e.target.value)}
                        slotProps={{
                            htmlInput: {
                                "data-testid": "blueprint-name-input",
                            },
                        }}
                    />
                    {blueprint && (
                        <>
                            {update.isPending && (
                                <CircularProgress
                                    size={18}
                                    data-testid="blueprint-saving"
                                />
                            )}
                            <Tooltip title={t("blueprints.duplicate")}>
                                <IconButton
                                    onClick={handleDuplicate}
                                    disabled={create.isPending}
                                    data-testid="blueprint-duplicate"
                                >
                                    <ContentCopy />
                                </IconButton>
                            </Tooltip>
                            <Tooltip title={t("blueprints.delete")}>
                                <IconButton
                                    color="error"
                                    onClick={handleDelete}
                                    disabled={remove.isPending}
                                    data-testid="blueprint-delete"
                                >
                                    <Delete />
                                </IconButton>
                            </Tooltip>
                        </>
                    )}
                </Stack>

                <BlueprintEditor
                    included={included}
                    onChange={setIncluded}
                    disabled={create.isPending}
                />

                {!blueprint && (
                    <Box
                        sx={{
                            mt: 2,
                            display: "flex",
                            justifyContent: "flex-end",
                        }}
                    >
                        <Button
                            variant="contained"
                            startIcon={<Save />}
                            disabled={!canCreate}
                            onClick={handleCreate}
                            data-testid="blueprint-save"
                        >
                            {t("blueprints.save")}
                        </Button>
                    </Box>
                )}
            </CardContent>
        </Card>
    );
}
