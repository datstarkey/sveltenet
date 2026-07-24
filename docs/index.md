---
layout: home

hero:
  name: SvelteNet
  text: Svelte 5 islands for ASP.NET
  tagline: Typed props from C#, SvelteKit-style forms and remote functions, with optional Jint, Node.js, Bun, or custom SSR.
  actions:
    - theme: brand
      text: Get started
      link: /getting-started
    - theme: alt
      text: Remote functions
      link: /remote-functions
    - theme: alt
      text: GitHub
      link: https://github.com/datstarkey/sveltenet

features:
  - title: Typed props
    details: "[SvelteProp] page models and [SvelteComponent] view models generate TypeScript interfaces automatically — C# is the source of truth."
  - title: SvelteKit-style forms
    details: Razor Pages form posts progressively enhanced with enhance() — no reload, fresh JSON props, model-state validation included.
  - title: Remote functions
    details: "[Query], [Command], and [Form] methods on C# services become a typed client — queries await in components, commands refresh them, forms spread onto <form>."
  - title: SSR on your terms
    details: Stay client-only, run the server bundle in-process with Jint, use Node.js or Bun from PATH, or register a custom renderer.
  - title: One Vite build
    details: A single vite build produces the client and SSR bundles via the Vite Environments API, with HMR in dev.
  - title: Source-generated dispatch
    details: SvelteNet.Generators compiles remote-function dispatchers at build time — no reflection in the request path.
---
