import { ContentCopy, Delete, Save } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    CardContent,
    IconButton,
    Stack,
    TextField,
    Tooltip,
} from "@mui/material";
import { useState } from "react";
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
    canManage: boolean;
    // Existing blueprint, or null for an unsaved draft (create flow).
    blueprint: SortBlueprintResponse | null;
    onCreated?: () => void;
}

export function BlueprintCard({ householdId, canManage, blueprint, onCreated }: Props) {
    const { t } = useTranslation();
    const create = useCreateSortBlueprint();
    const update = useUpdateSortBlueprint();
    const remove = useDeleteSortBlueprint();

    const [name, setName] = useState(blueprint?.name ?? "");
    const [included, setIncluded] = useState<ProductCategory[]>(
        blueprint?.categories ?? [],
    );

    const isSaving = create.isPending || update.isPending;
    const canSave = canManage && name.trim().length > 0 && included.length > 0 && !isSaving;

    const handleSave = async () => {
        try {
            if (blueprint) {
                await update.mutateAsync({
                    path: { householdId, blueprintId: blueprint.id },
                    body: { name: name.trim(), categories: included },
                });
            } else {
                await create.mutateAsync({
                    path: { householdId },
                    body: { name: name.trim(), categories: included },
                });
                onCreated?.();
            }
            toast.success(t("blueprints.saved"));
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
            await remove.mutateAsync({
                path: { householdId, blueprintId: blueprint.id },
            });
            toast.success(t("blueprints.deleted"));
        } catch {
            toast.error(t("blueprints.deleteFailed"));
        }
    };

    return (
        <Card elevation={2} sx={{ mb: { xs: 2, sm: 3 } }} data-testid="blueprint-card">
            <CardContent>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                    <TextField
                        fullWidth
                        size="small"
                        label={t("blueprints.nameLabel")}
                        placeholder={t("blueprints.namePlaceholder")}
                        value={name}
                        disabled={!canManage || isSaving}
                        onChange={(e) => setName(e.target.value)}
                        slotProps={{
                            htmlInput: { "data-testid": "blueprint-name-input" },
                        }}
                    />
                    {blueprint && canManage && (
                        <>
                            <Tooltip title={t("blueprints.duplicate")}>
                                <IconButton
                                    onClick={handleDuplicate}
                                    disabled={isSaving}
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
                    disabled={!canManage || isSaving}
                />

                {canManage && (
                    <Box sx={{ mt: 2, display: "flex", justifyContent: "flex-end" }}>
                        <Button
                            variant="contained"
                            startIcon={<Save />}
                            disabled={!canSave}
                            onClick={handleSave}
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
