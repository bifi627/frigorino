import { Language, Translate } from "@mui/icons-material";
import {
    Button,
    IconButton,
    Menu,
    MenuItem,
    ListItemIcon,
    ListItemText,
    Tooltip,
} from "@mui/material";
import React, { useState } from "react";
import { useTranslation } from "react-i18next";

interface LanguageSwitcherProps {
    variant?: "button" | "icon";
    size?: "small" | "medium" | "large";
}

const languages = [
    { code: "en", name: "English", flag: "🇺🇸" },
    { code: "de", name: "Deutsch", flag: "🇩🇪" },
];

export const LanguageSwitcher: React.FC<LanguageSwitcherProps> = ({
    variant = "icon",
    size = "medium",
}) => {
    const { i18n } = useTranslation();
    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const open = Boolean(anchorEl);

    const handleClick = (event: React.MouseEvent<HTMLElement>) => {
        setAnchorEl(event.currentTarget);
    };

    const handleClose = () => {
        setAnchorEl(null);
    };

    const handleLanguageChange = (languageCode: string) => {
        i18n.changeLanguage(languageCode);
        handleClose();
    };

    const currentLanguage =
        languages.find((lang) => lang.code === i18n.language) || languages[0];

    if (variant === "button") {
        return (
            <>
                <Button
                    onClick={handleClick}
                    startIcon={<Translate />}
                    size={size}
                    sx={{ minWidth: 120 }}
                >
                    {currentLanguage.flag} {currentLanguage.name}
                </Button>
                <Menu
                    anchorEl={anchorEl}
                    open={open}
                    onClose={handleClose}
                    anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
                    transformOrigin={{ vertical: "top", horizontal: "left" }}
                >
                    {languages.map((language) => (
                        <MenuItem
                            key={language.code}
                            onClick={() => handleLanguageChange(language.code)}
                            selected={language.code === i18n.language}
                        >
                            <ListItemIcon sx={{ minWidth: "auto", mr: 1 }}>
                                <span style={{ fontSize: "1.2em" }}>
                                    {language.flag}
                                </span>
                            </ListItemIcon>
                            <ListItemText primary={language.name} />
                        </MenuItem>
                    ))}
                </Menu>
            </>
        );
    }

    return (
        <>
            <Tooltip title={`Current: ${currentLanguage.name}`}>
                <IconButton onClick={handleClick} size={size} color="inherit">
                    <Language />
                </IconButton>
            </Tooltip>
            <Menu
                anchorEl={anchorEl}
                open={open}
                onClose={handleClose}
                anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
                transformOrigin={{ vertical: "top", horizontal: "right" }}
            >
                {languages.map((language) => (
                    <MenuItem
                        key={language.code}
                        onClick={() => handleLanguageChange(language.code)}
                        selected={language.code === i18n.language}
                    >
                        <ListItemIcon sx={{ minWidth: "auto", mr: 1 }}>
                            <span style={{ fontSize: "1.2em" }}>
                                {language.flag}
                            </span>
                        </ListItemIcon>
                        <ListItemText primary={language.name} />
                    </MenuItem>
                ))}
            </Menu>
        </>
    );
};
