import type { Component } from 'svelte';

export interface RenderResult {
	head: string;
	body: string;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export declare function renderComponent(component: Component<any>, props?: Record<string, unknown>): Promise<RenderResult>;
