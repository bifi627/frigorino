import { useCallback, useMemo, useRef, useState } from "react";
import type { AnyFeature, AnyModifierFeature } from "../types";

type ValuesMap = Record<string, unknown>;

interface InitialDraft {
    text?: string;
    values?: Record<string, unknown>;
}

interface UseComposerStateArgs {
    features: readonly AnyFeature[];
    initialDraft?: InitialDraft;
    initialOpenId?: string;
}

const onlyModifiers = (features: readonly AnyFeature[]): AnyModifierFeature[] =>
    features.filter((f): f is AnyModifierFeature => f.kind === "modifier");

export const isModifierValueEmpty = (
    feature: AnyModifierFeature,
    value: unknown,
): boolean => {
    if (feature.isEmpty) {
        return feature.isEmpty(value);
    }
    return value === undefined || value === null || value === "";
};

export function useComposerState({
    features,
    initialDraft,
    initialOpenId,
}: UseComposerStateArgs) {
    const modifiers = useMemo(() => onlyModifiers(features), [features]);

    const seedValues = useCallback((): ValuesMap => {
        const map: ValuesMap = {};
        for (const f of modifiers) {
            const seeded = initialDraft?.values?.[f.id];
            map[f.id] = seeded !== undefined ? seeded : f.initial;
        }
        return map;
    }, [modifiers, initialDraft]);

    const [text, setText] = useState<string>(() => initialDraft?.text ?? "");
    const [values, setValues] = useState<ValuesMap>(seedValues);
    const [openId, setOpenId] = useState<string | null>(initialOpenId ?? null);
    const inputRef = useRef<HTMLInputElement>(null);

    const setValue = useCallback((id: string, value: unknown) => {
        setValues((prev) => ({ ...prev, [id]: value }));
    }, []);

    const toggleOpen = useCallback((id: string) => {
        setOpenId((prev) => (prev === id ? null : id));
    }, []);

    const focusInput = useCallback(() => {
        inputRef.current?.focus();
    }, []);

    const reset = useCallback(() => {
        setText("");
        const cleared: ValuesMap = {};
        for (const f of modifiers) {
            cleared[f.id] = f.initial;
        }
        setValues(cleared);
        setOpenId(null);
    }, [modifiers]);

    return {
        text,
        setText,
        values,
        setValue,
        openId,
        toggleOpen,
        inputRef,
        focusInput,
        reset,
    };
}
