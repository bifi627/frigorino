import { useCallback, useRef } from "react";

interface UseLongPressOptions {
    onLongPress: () => void;
    delay?: number;
    shouldPreventDefault?: boolean;
}

interface UseLongPressReturn {
    onTouchStart: () => void;
    onTouchEnd: () => void;
    onTouchMove: () => void;
    onClick: (e: React.MouseEvent) => void;
}

/**
 * Custom hook for handling long press interactions on mobile devices
 * @param options Configuration options for the long press behavior
 * @returns Event handlers for touch and click events
 */
export const useLongPress = ({
    onLongPress,
    delay = 500,
    shouldPreventDefault = true,
}: UseLongPressOptions): UseLongPressReturn => {
    const longPressTimer = useRef<NodeJS.Timeout | null>(null);
    const longPressTriggered = useRef(false);

    const clearTimer = useCallback(() => {
        if (longPressTimer.current) {
            clearTimeout(longPressTimer.current);
            longPressTimer.current = null;
        }
    }, []);

    const handleTouchStart = useCallback(() => {
        longPressTriggered.current = false;
        longPressTimer.current = setTimeout(() => {
            longPressTriggered.current = true;
            onLongPress();
        }, delay);
    }, [onLongPress, delay]);

    const handleTouchEnd = useCallback(() => {
        // Add a small delay before clearing to ensure toast has time to display
        if (longPressTriggered.current) {
            // If long press was triggered, delay clearing to let toast show
            setTimeout(clearTimer, 100);
        } else {
            clearTimer();
        }
    }, [clearTimer]);

    const handleTouchMove = useCallback(() => {
        // Cancel long press if user moves finger (but only if not already triggered)
        if (!longPressTriggered.current) {
            clearTimer();
        }
    }, [clearTimer]);

    const handleClick = useCallback(
        (e: React.MouseEvent) => {
            // Prevent default click behavior if long press was triggered
            if (longPressTriggered.current && shouldPreventDefault) {
                e.preventDefault();
                e.stopPropagation();
            }
        },
        [shouldPreventDefault],
    );

    return {
        onTouchStart: handleTouchStart,
        onTouchEnd: handleTouchEnd,
        onTouchMove: handleTouchMove,
        onClick: handleClick,
    };
};
