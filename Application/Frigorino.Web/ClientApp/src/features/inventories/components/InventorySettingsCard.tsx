import {
    Alert,
    Card,
    CardContent,
    FormControlLabel,
    Switch,
    TextField,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useUserSettings } from "../../settings/useUserSettings";
import { useMyInventoryNotification } from "../useMyInventoryNotification";
import { useUpdateMyInventoryNotification } from "../useUpdateMyInventoryNotification";

interface Props {
    householdId: number;
    inventoryId: number;
}

type InventoryNotification = NonNullable<
    ReturnType<typeof useMyInventoryNotification>["data"]
>;

export function InventorySettingsCard({ householdId, inventoryId }: Props) {
    const { data } = useMyInventoryNotification(householdId, inventoryId);

    // Remount the inner form once the notification settings load so the controls seed from server
    // data via useState initializers (instead of a reset-in-effect). Keyed on load state — not on
    // data identity — so a background refetch doesn't clobber an in-progress edit.
    return (
        <InventorySettingsCardInner
            key={data ? "ready" : "loading"}
            householdId={householdId}
            inventoryId={inventoryId}
            data={data}
        />
    );
}

interface InnerProps {
    householdId: number;
    inventoryId: number;
    data: InventoryNotification | undefined;
}

function InventorySettingsCardInner({
    householdId,
    inventoryId,
    data,
}: InnerProps) {
    const { t } = useTranslation();
    const { data: userSettings, isSuccess: userSettingsLoaded } =
        useUserSettings();
    const globalNotificationsEnabled =
        userSettings?.expiryNotificationsEnabled ?? false;
    const update = useUpdateMyInventoryNotification();
    const [notificationsEnabled, setNotificationsEnabled] = useState(() =>
        data ? data.enabled : true,
    );
    const [override, setOverride] = useState(() =>
        data ? data.leadDays !== null : false,
    );
    const [value, setValue] = useState(() =>
        data && data.leadDays !== null ? String(data.leadDays) : "7",
    );

    const save = async (enabled: boolean, leadDays: number | null) => {
        try {
            await update.mutateAsync({
                path: { householdId, inventoryId },
                body: { enabled, leadDays },
            });
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    const handleNotificationsToggle = async (checked: boolean) => {
        setNotificationsEnabled(checked);
        await save(checked, override ? Number(value) : null);
    };

    const handleToggle = async (checked: boolean) => {
        setOverride(checked);
        await save(notificationsEnabled, checked ? Number(value) : null);
    };

    const handleBlur = async () => {
        const days = Number(value);
        if (!override || !Number.isInteger(days) || days < 0) {
            return;
        }
        if (data && data.leadDays === days) {
            return;
        }
        await save(notificationsEnabled, days);
    };

    return (
        <Card elevation={2} sx={{ mt: { xs: 2, sm: 3 } }}>
            <CardContent>
                <Typography variant="h6" sx={{ mb: 1 }}>
                    {t("settings.inventorySettings")}
                </Typography>
                {userSettingsLoaded && !globalNotificationsEnabled && (
                    <Alert
                        severity="info"
                        sx={{ mb: 2 }}
                        data-testid="inventory-notifications-global-off-hint"
                    >
                        {t("settings.inventoryNotificationsRequiresGlobal")}
                    </Alert>
                )}
                <FormControlLabel
                    control={
                        <Switch
                            data-testid="inventory-notifications-switch"
                            checked={notificationsEnabled}
                            disabled={update.isPending}
                            onChange={(e) =>
                                handleNotificationsToggle(e.target.checked)
                            }
                        />
                    }
                    label={t("settings.inventoryNotificationsEnable")}
                />
                <FormControlLabel
                    control={
                        <Switch
                            data-testid="inventory-expiry-override-switch"
                            checked={override}
                            disabled={update.isPending}
                            onChange={(e) => handleToggle(e.target.checked)}
                        />
                    }
                    label={t("settings.expiryLeadOverride")}
                />
                {override && (
                    <TextField
                        type="number"
                        fullWidth
                        size="small"
                        sx={{ mt: 1 }}
                        label={t("settings.expiryLeadDays")}
                        helperText={t("settings.expiryLeadHelp")}
                        value={value}
                        disabled={update.isPending}
                        onChange={(e) => setValue(e.target.value)}
                        onBlur={handleBlur}
                        slotProps={{
                            htmlInput: {
                                min: 0,
                                max: 365,
                                "data-testid": "inventory-expiry-lead-input",
                            },
                        }}
                    />
                )}
            </CardContent>
        </Card>
    );
}
