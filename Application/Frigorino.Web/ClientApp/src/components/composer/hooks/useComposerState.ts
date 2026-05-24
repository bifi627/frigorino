import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { AnyFeature, AnyModifierFeature } from "../types";

type ValuesMap = Record<string, unknown>;
type OpenMap = Record<string, boolean>;

interface InitialDraft {
    text?: string;
    values?: Record<string, unknown>;
}

interface UseComposerStateArgs {
    features: readonly AnyFeature[];
    initialDraft?: InitialDraft;
}

const onlyModifiers = (features: readonly AnyFeature[]): AnyModifierFeature[] =>
    features.filter((f): f is AnyModifierFeature => f.kind === "modifier");

const isValueEmpty = (feature: AnyModifierFeature, value: unknown): boolean => {
    if (feature.isEmpty) {
        return feature.isEmpty(value);
    }
    return value === undefined || value === null || value === "";
};

export function useComposerState({ features, initialDraft }: UseComposerStateArgs) {
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
    const [open, setOpen] = useState<OpenMap>({});
    const inputRef = useRef<HTMLInputElement>(null);

    // Re-seed text + values whenever a new draft object is supplied (e.g. editing a new item).
    const draftRef = useRef<InitialDraft | undefined>(initialDraft);
    useEffect(() => {
        if (draftRef.current === initialDraft) {
            return;
        }
        draftRef.current = initialDraft;
        setText(initialDraft?.text ?? "");
        const nextValues = seedValues();
        setValues(nextValues);
        const nextOpen: OpenMap = {};
        for (const f of modifiers) {
            if (!isValueEmpty(f, nextValues[f.id])) {
                nextOpen[f.id] = true;
            }
        }
        setOpen(nextOpen);
    }, [initialDraft, seedValues, modifiers]);

    const setValue = useCallback((id: string, value: unknown) => {
        setValues((prev) => ({ ...prev, [id]: value }));
    }, []);

    const toggleOpen = useCallback((id: string) => {
        setOpen((prev) => ({ ...prev, [id]: !prev[id] }));
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
        setOpen({});
    }, [modifiers]);

    return {
        text,
        setText,
        values,
        setValue,
        open,
        toggleOpen,
        inputRef,
        focusInput,
        reset,
    };
}
