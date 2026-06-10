import {
    Card,
    CardContent,
    Container,
    MenuItem,
    TextField,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { PageHeadActionBar } from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
import { NotificationsCard } from "../components/NotificationsCard";
import { useUpdateUserSettings } from "../useUpdateUserSettings";
import { useUserSettings } from "../useUserSettings";

const LANGUAGES = [
    { code: "en", label: "English" },
    { code: "de", label: "Deutsch" },
];

export function UserSettingsPage() {
    const { t, i18n } = useTranslation();
    const { data, isLoading } = useUserSettings();
    const updateSettings = useUpdateUserSettings();

    const currentLanguage = data?.language ?? i18n.language;

    const handleLanguageChange = async (language: string) => {
        try {
            await updateSettings.mutateAsync({ body: { language } });
            await i18n.changeLanguage(language);
            toast.success(t("settings.saved"));
        } catch {
            toast.error(t("settings.saveFailed"));
        }
    };

    return (
        <>
            <PageHeadActionBar
                title={t("settings.userSettings")}
                directActions={[]}
                menuActions={[]}
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Card elevation={2}>
                    <CardContent>
                        <TextField
                            select
                            fullWidth
                            size="small"
                            data-testid="settings-language-select"
                            label={t("settings.language")}
                            helperText={t("settings.languageHelp")}
                            value={currentLanguage}
                            disabled={isLoading || updateSettings.isPending}
                            onChange={(e) =>
                                handleLanguageChange(e.target.value)
                            }
                            slotProps={{
                                htmlInput: {
                                    "data-testid": "settings-language-value",
                                },
                            }}
                        >
                            {LANGUAGES.map((lang) => (
                                <MenuItem
                                    key={lang.code}
                                    value={lang.code}
                                    data-testid={`settings-language-option-${lang.code}`}
                                >
                                    {lang.label}
                                </MenuItem>
                            ))}
                        </TextField>
                    </CardContent>
                </Card>

                <NotificationsCard
                    key={data ? "ready" : "loading"}
                    data={data}
                />
            </Container>
        </>
    );
}
