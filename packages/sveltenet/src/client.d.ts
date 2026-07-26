import type { Component } from 'svelte';

export interface MountOptions {
	target: HTMLElement;
	props?: Record<string, unknown>;
	hydrate?: boolean;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export declare function mountComponent(component: Component<any>, options: MountOptions): Record<string, unknown>;

/** RFC 9457 validation problem with the ASP.NET `errors` member. */
export interface ValidationProblem {
	title: string;
	status: number;
	errors: Record<string, string[]>;
}

export interface EnhanceOptions<TData = unknown> {
	/** Receives the fresh `data` prop after the handler ran (its `problem` field carries validation errors). */
	onUpdate?: (data: TData) => void;
	/** Called on network/parse failure. Without it the error is rethrown. */
	onError?: (error: unknown) => void;
	/** Reset the form after a successful (redirecting) submit. @default true */
	reset?: boolean;
	/**
	 * Antiforgery token, sent as the RequestVerificationToken header. Every page's data
	 * prop carries one as `antiforgeryToken`. Pass a function when the token comes from
	 * reactive state — it is read at submit time, so it cannot go stale after an update.
	 * Omit it only when the form already renders a hidden `__RequestVerificationToken`
	 * input, which is picked up automatically.
	 */
	token?: string | (() => string | undefined);
}

/** Attachment factory: `<form method="post" {@attach enhance({ onUpdate: ... })}>` */
export declare function enhance<TData = unknown>(options?: EnhanceOptions<TData>): (form: HTMLFormElement) => () => void;
