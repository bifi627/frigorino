import type { ReactNode } from "react";

/** State handed to a modifier feature's render functions. */
export interface FeatureSlot<V> {
    value: V;
    setValue: (value: V) => void;
    open: boolean;
    toggleOpen: () => void;
    disabled: boolean;
}

/** A feature that augments a text completion with a typed value under its id. */
export interface ModifierFeature<Id extends string, V> {
    kind: "modifier";
    id: Id;
    initial: V;
    /** Optional emptiness test; used to decide whether a value renders a chip. */
    isEmpty?: (value: V) => boolean;
    renderToggle?: (slot: FeatureSlot<V>) => ReactNode;
    renderPanel?: (slot: FeatureSlot<V>) => ReactNode;
    /** Optional chip shown above the field when the value is non-empty; tapping it opens the panel. */
    renderChip?: (slot: FeatureSlot<V>) => ReactNode;
}

/** Context handed to an action feature's trigger. */
export interface ActionContext<P> {
    complete: (payload: P) => void;
    disabled: boolean;
}

/** A feature that IS its own completion (e.g. pick a document). */
export interface ActionFeature<Id extends string, P> {
    kind: "action";
    id: Id;
    renderTrigger: (ctx: ActionContext<P>) => ReactNode;
}

/* eslint-disable @typescript-eslint/no-explicit-any -- type-level only: `any` lets a
   heterogeneous features array hold differently-typed features. The real value/payload
   types are recovered by inference in Completion below, so consumers never see `any`. */
export type AnyModifierFeature = ModifierFeature<string, any>;
export type AnyActionFeature = ActionFeature<string, any>;
/* eslint-enable @typescript-eslint/no-explicit-any */

export type AnyFeature = AnyModifierFeature | AnyActionFeature;

/** Map of each modifier feature's id -> its value type. */
export type ModifierValues<F extends readonly AnyFeature[]> = {
    [M in Extract<F[number], AnyModifierFeature> as M["id"]]: M extends ModifierFeature<
        string,
        infer V
    >
        ? V
        : never;
};

/** The text-send completion: text + mode + all modifier values. */
export type TextCompletion<F extends readonly AnyFeature[]> = {
    kind: "text";
    mode: "create" | "edit";
    text: string;
} & ModifierValues<F>;

/** One completion variant per action feature. */
export type ActionCompletion<A extends AnyActionFeature> =
    A extends ActionFeature<infer Id, infer P> ? { kind: Id } & P : never;

/** Full discriminated-union completion for a features tuple. */
export type Completion<F extends readonly AnyFeature[]> =
    | TextCompletion<F>
    | ActionCompletion<Extract<F[number], AnyActionFeature>>;

export interface Suggestion {
    id: string | number;
    label: string;
    secondaryLabel?: string;
    badge?: ReactNode;
}

export interface SuggestionsConfig {
    getItems: (query: string) => Suggestion[];
    minChars?: number;
    onSelect?: (suggestion: Suggestion) => void;
}

export interface DuplicateResult {
    /** Already-localized message shown inline under the field. */
    message: string;
    /** When true, send is disabled and the text completion is prevented. */
    block?: boolean;
    /** When set, hitting send fires this instead of completing (e.g. "uncheck existing"). */
    onResolve?: () => void;
}

export interface DuplicateConfig {
    check: (text: string) => DuplicateResult | null;
}

export interface EditingConfig {
    active: boolean;
    onCancel: () => void;
    label?: string;
}

export interface ComposerProps<F extends readonly AnyFeature[]> {
    features?: F;
    onComplete: (completion: Completion<F>) => void;
    placeholder?: string;
    disabled?: boolean;
    editing?: EditingConfig;
    initialDraft?: { text?: string; values?: Partial<ModifierValues<F>> };
    suggestions?: SuggestionsConfig;
    duplicate?: DuplicateConfig;
}
