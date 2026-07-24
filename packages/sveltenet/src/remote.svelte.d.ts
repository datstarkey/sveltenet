export interface RemoteQuery<T> extends PromiseLike<T> {
	/** Latest resolved value, or undefined while loading / after an error. */
	readonly current: T | undefined;
	readonly loading: boolean;
	readonly error: unknown;
	/** Re-fetches from the server and resolves with the new value. */
	refresh(): Promise<T>;
	/** Sets the value locally (e.g. from a command's return value). */
	set(value: T): void;
	finally(onfinally?: (() => void) | null): Promise<T>;
}

/** Cacheable, refreshable server read — `getPosts() === getPosts()` per argument set. */
export declare function query<T, A extends unknown[] = []>(
	path: string,
	mapArgs?: (...args: A) => Record<string, unknown>,
): (...args: A) => RemoteQuery<T>;

export interface RemoteCommand<T> extends Promise<T> {
	/** Refreshes the given queries after the command succeeds, resolving with the command's result. */
	updates(...queries: RemoteQuery<unknown>[]): Promise<T>;
}

/** Server write callable from anywhere (not during render). */
export declare function command<T, A extends unknown[] = []>(
	path: string,
	mapArgs?: (...args: A) => Record<string, unknown>,
): (...args: A) => RemoteCommand<T>;

export interface RemoteIssue {
	message: string;
}

export interface RemoteField<T> {
	/** Attributes for an input of the given type: name, type, value/checked, aria-invalid. */
	as(type: string, value?: T | string): Record<string, unknown>;
	issues(): RemoteIssue[] | undefined;
	value(): T | undefined;
	set(value: T): void;
}

export type RemoteFormFields<F> = { readonly [K in keyof F]-?: RemoteField<F[K]> } & {
	value(): Partial<F>;
	set(values: Partial<F>): void;
	allIssues(): RemoteIssue[] | undefined;
};

export interface RemoteFormSubmission<T, F> {
	readonly fields: RemoteFormFields<F>;
	readonly element: HTMLFormElement;
	readonly pending: number;
	readonly result: T | undefined;
	/** Submits directly; resolves with the result (or true for void), or false when invalid. */
	submit(): Promise<T | boolean>;
}

export interface RemoteFormAttributes {
	readonly method: 'POST';
	readonly action: string;
	[key: symbol]: Attachment<HTMLFormElement> | undefined;
}

export interface RemoteForm<T, F = Record<string, unknown>> extends RemoteFormAttributes {
	readonly fields: RemoteFormFields<F>;
	readonly pending: number;
	readonly result: T | undefined;
	/** Validates on the server (binding only) without running the handler. */
	validate(): Promise<void>;
	/** An isolated instance for repeated forms in a list. */
	for(id: string | number): RemoteForm<T, F>;
	/** Custom submit flow; the returned object is spread onto the <form> instead. */
	enhance(callback: (form: RemoteFormSubmission<T, F>) => void | Promise<void>): RemoteFormAttributes;
}

/** Form handler bound to a [Form] C# method — spread onto a <form>: `<form {...createPost}>`. */
export declare function form<T, F = Record<string, unknown>>(path: string): RemoteForm<T, F>;

/** Refreshes every query created on this page. */
export declare function refreshAll(): Promise<unknown[]>;

/**
 * Makes a boundary's pending snippet client-only so SSR awaits and renders the
 * real content: `<svelte:boundary pending={clientPending(loading)}>`. A static
 * `{#snippet pending()}` would make the compiler drop the children from the
 * server bundle entirely.
 */
export declare function clientPending<T>(snippet: T): T | undefined;
import type { Attachment } from 'svelte/attachments';
