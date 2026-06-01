import {
    Card,
    CardContent,
    Container,
    MenuItem,
    TextField,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { pageContainerSx } from "../../../theme";
import { useUserSettings } from "../useUserSettings";
import { useUpdateUserSettings } from "../useUpdateUserSettings";

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
        <Container maxWidth="sm" sx={pageContainerSx}>
            <Typography variant="h5" sx={{ mb: { xs: 2, sm: 3 } }}>
                {t("settings.userSettings")}
            </Typography>

            <Card elevation={2}>
                <CardContent>
                    <TextField
                        select
                        fullWidth
                        size="small"
                        label={t("settings.language")}
                        helperText={t("settings.languageHelp")}
                        value={currentLanguage}
                        disabled={isLoading || updateSettings.isPending}
                        onChange={(e) => handleLanguageChange(e.target.value)}
                    >
                        {LANGUAGES.map((lang) => (
                            <MenuItem key={lang.code} value={lang.code}>
                                {lang.label}
                            </MenuItem>
                        ))}
                    </TextField>
                </CardContent>
            </Card>
        </Container>
    );
}
