import { Box, Typography } from "@mui/material";
import { useState } from "react";

interface HeroImageProps {
    src: string;
    alt: string;
    size?: "small" | "medium" | "large";
    borderRadius?: number;
    className?: string;
}

const sizeConfig = {
    small: {
        maxWidth: { xs: 280, sm: 320, md: 360 },
        maxHeight: { xs: 120, sm: 140, md: 160 },
        hoverScale: "1.02",
        boxShadow: "0 4px 12px rgba(0,0,0,0.1)",
        hoverBoxShadow: "0 6px 16px rgba(0,0,0,0.15)",
        pulseAnimation: "pulse 3.5s ease-in-out infinite",
        transition: "all 1s cubic-bezier(0.4, 0, 0.2, 1)",
    },
    medium: {
        maxWidth: { xs: 320, sm: 400, md: 450 },
        maxHeight: { xs: 160, sm: 200, md: 230 },
        hoverScale: "1.03",
        boxShadow: "0 6px 18px rgba(0,0,0,0.12)",
        hoverBoxShadow: "0 8px 24px rgba(0,0,0,0.18)",
        pulseAnimation: "pulse 2s ease-in-out infinite",
        transition: "all 0.8s cubic-bezier(0.4, 0, 0.2, 1)",
    },
    large: {
        maxWidth: { xs: 350, sm: 450, md: 500 },
        maxHeight: { xs: 180, sm: 220, md: 250 },
        hoverScale: "1.05",
        boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
        hoverBoxShadow: "0 12px 32px rgba(0,0,0,0.15)",
        pulseAnimation: "pulse 1.5s ease-in-out infinite",
        transition: "all 0.6s cubic-bezier(0.4, 0, 0.2, 1)",
    },
};

export const HeroImage = ({
    src,
    alt,
    size = "medium",
    borderRadius = 2,
    className,
}: HeroImageProps) => {
    const [imageLoaded, setImageLoaded] = useState(false);
    const [isClicked, setIsClicked] = useState(false);
    const config = sizeConfig[size];

    const handleClick = () => {
        setIsClicked(true);
        // Reset the animation after it completes
        setTimeout(() => setIsClicked(false), 300);
    };

    return (
        <Box
            sx={{
                display: "flex",
                justifyContent: "center",
                position: "relative",
                width: "100%",
            }}
            className={className}
        >
            {/* Loading skeleton/placeholder */}
            {!imageLoaded && (
                <Box
                    sx={{
                        width: "100%",
                        maxWidth: config.maxWidth,
                        height: config.maxHeight,
                        borderRadius,
                        bgcolor: "grey.100",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        position: "absolute",
                        animation: config.pulseAnimation,
                        "@keyframes pulse": {
                            "0%": {
                                opacity: 1,
                            },
                            "50%": {
                                opacity: 0.5,
                            },
                            "100%": {
                                opacity: 1,
                            },
                        },
                    }}
                >
                    <Typography
                        variant={size === "small" ? "caption" : "body2"}
                        color="text.secondary"
                        sx={{
                            fontSize:
                                size === "small"
                                    ? "0.8rem"
                                    : size === "large"
                                      ? "0.9rem"
                                      : "0.85rem",
                        }}
                    >
                        Loading...
                    </Typography>
                </Box>
            )}

            <Box
                component="img"
                src={src}
                alt={alt}
                loading="lazy"
                onLoad={() => setImageLoaded(true)}
                onClick={handleClick}
                sx={{
                    width: "100%",
                    maxWidth: config.maxWidth,
                    height: "auto",
                    borderRadius,
                    boxShadow: config.boxShadow,
                    maxHeight: config.maxHeight,
                    objectFit: "contain",
                    opacity: imageLoaded ? 1 : 0,
                    transform: imageLoaded ? "scale(1)" : "scale(0.95)",
                    transition: config.transition,
                    cursor: "pointer",
                    animation: isClicked ? "clickPulse 0.3s ease-out" : "none",
                    "&:hover": {
                        transform: imageLoaded
                            ? `scale(${config.hoverScale})`
                            : "scale(0.95)",
                        boxShadow: config.hoverBoxShadow,
                    },
                    "&:active": {
                        transform: imageLoaded ? "scale(0.98)" : "scale(0.95)",
                    },
                    "@keyframes clickPulse": {
                        "0%": {
                            transform: imageLoaded ? "scale(1)" : "scale(0.95)",
                        },
                        "50%": {
                            transform: imageLoaded
                                ? "scale(1.1)"
                                : "scale(1.05)",
                            filter: "brightness(1.1)",
                        },
                        "100%": {
                            transform: imageLoaded ? "scale(1)" : "scale(0.95)",
                            filter: "brightness(1)",
                        },
                    },
                }}
            />
        </Box>
    );
};
