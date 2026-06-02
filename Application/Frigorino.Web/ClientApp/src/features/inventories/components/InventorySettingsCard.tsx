import {
    Alert,
    Card,
    CardContent,
    FormControlLabel,
    Switch,
    TextField,
    Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useUserSettings } from "../../settings/useUserSettings";
import { useMyInventoryNotification } from "../useMyInventoryNotification";
import { useUpdateMyInventoryNotification } from "../useUpdateMyInventoryNotification";

interface Props {
    householdId: number;
    inventoryId: number;
}

export function InventorySettingsCard({ householdId, inventoryId }: Props) {
    const { t } = useTranslation();
    const { data } = useMyInventoryNotification(householdId, inventoryId);
    const { data: userSettings, isSuccess: userSettingsLoaded } =
        useUserSettings();
    const globalNotificationsEnabled =
        userSettings?.expiryNotificationsEnabled ?? false;
    const update = useUpdateMyInventoryNotification();
    const [notificationsEnabled, setNotificationsEnabled] = useState(true);
    const [override, setOverride] = useState(false);
    const [value, setValue] = useState("7");

    useEffect(() => {
        if (data) {
            setNotificationsEnabled(data.enabled);
            setOverride(data.leadDays !== null);
            if (data.leadDays !== null) {
                setValue(String(data.leadDays));
            }
        }
    }, [data]);

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
