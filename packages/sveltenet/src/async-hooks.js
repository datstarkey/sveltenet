// Stand-in for node:async_hooks inside the .NET SSR engine (no Node builtins).
// Each Jint engine renders one tree at a time, so a persistent store — set by
// run()/enterWith() and never popped — provides the async-context continuity
// Svelte's async SSR needs without real async-hooks support.
export class AsyncLocalStorage {
	/** @type {unknown} */
	#store;

	getStore() {
		return this.#store;
	}

	/**
	 * @template T
	 * @param {unknown} store
	 * @param {(...args: any[]) => T} fn
	 * @param {any[]} args
	 * @returns {T}
	 */
	run(store, fn, ...args) {
		this.#store = store;
		return fn(...args);
	}

	/** @param {unknown} store */
	enterWith(store) {
		this.#store = store;
	}

	/**
	 * @template T
	 * @param {(...args: any[]) => T} fn
	 * @param {any[]} args
	 * @returns {T}
	 */
	exit(fn, ...args) {
		this.#store = undefined;
		return fn(...args);
	}

	disable() {
		this.#store = undefined;
	}
}

export default { AsyncLocalStorage };
