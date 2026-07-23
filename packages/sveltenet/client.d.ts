import type { Component } from 'svelte';

export interface MountOptions {
	target: HTMLElement;
	props?: Record<string, unknown>;
	hydrate?: boolean;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export declare function mountComponent(component: Component<any>, options: MountOptions): Record<string, unknown>;

export interface EnhanceOptions<TData = unknown> {
	/** Receives the fresh `data` prop after the handler ran (including modelState on validation failure). */
	onUpdate?: (data: TData) => void;
	/** Called on network/parse failure. Without it the error is rethrown. */
	onError?: (error: unknown) => void;
	/** Reset the form after a successful (redirecting) submit. @default true */
	reset?: boolean;
}

/** Attachment factory: `<form method="post" {@attach enhance({ onUpdate: ... })}>` */
export declare function enhance<TData = unknown>(options?: EnhanceOptions<TData>): (form: HTMLFormElement) => () => void;
