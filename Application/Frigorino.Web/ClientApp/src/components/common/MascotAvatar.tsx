import { Box } from "@mui/material";
import { useState } from "react";

interface MascotAvatarProps {
    size?: number;
}

// Small app-icon mascot for the dashboard header. Tapping plays a one-shot grow + wiggle
// easter egg, then auto-reverts (onAnimationEnd clears the flag so it can replay).
export const MascotAvatar = ({ size = 40 }: MascotAvatarProps) => {
    const [playing, setPlaying] = useState(false);

    const handleClick = (e: React.MouseEvent) => {
        // Don't let the tap bubble into the header's long-press (email copy) handler.
        e.stopPropagation();
        if (playing) {
            return;
        }
        navigator.vibrate?.(30);
        setPlaying(true);
    };

    return (
        <Box
            component="img"
            src="/192.png"
            alt="Frigorino"
            onClick={handleClick}
            onAnimationEnd={() => setPlaying(false)}
            data-testid="mascot-avatar"
            sx={{
                width: size,
                height: size,
                objectFit: "contain",
                cursor: "pointer",
                userSelect: "none",
                WebkitUserSelect: "none",
                WebkitTapHighlightColor: "transparent",
                WebkitTouchCallout: "none",
                transformOrigin: "center bottom",
                transition: "transform 0.2s ease",
                animation: playing ? "mascotWiggle 0.6s ease-in-out" : "none",
                "&:hover": {
                    transform: playing ? undefined : "scale(1.08)",
                },
                "@keyframes mascotWiggle": {
                    "0%": { transform: "scale(1) rotate(0deg)" },
                    "20%": { transform: "scale(1.35) rotate(-8deg)" },
                    "40%": { transform: "scale(1.35) rotate(8deg)" },
                    "60%": { transform: "scale(1.3) rotate(-6deg)" },
                    "80%": { transform: "scale(1.25) rotate(4deg)" },
                    "100%": { transform: "scale(1) rotate(0deg)" },
                },
            }}
        />
    );
};
