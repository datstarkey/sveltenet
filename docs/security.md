# Security

## Authorization

Remote functions use ordinary ASP.NET authorization. Put `[Authorize]` on a
`[SvelteRemote]` class to protect every function in it, and `[AllowAnonymous]` on a
method to opt that one back out:

```csharp
[SvelteRemote]
[Authorize(Policy = "admin")]
public class AdminApi
{
    [Query] public IReadOnlyList<AuditEntry> GetAuditLog() => ...;

    [Query]
    [AllowAnonymous]
    public string GetServiceStatus() => "ok";
}
```

Class-level and method-level requirements combine, the same way they do in MVC.
Authentication and authorization services are yours to register as usual:

```csharp
builder.Services.AddAuthentication(...).AddCookie();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("admin", policy => policy.RequireRole("admin"));

app.UseAuthentication();
app.UseAuthorization();
```

### Why it is on the attribute, not the endpoint

`MapSvelteRemote()` maps one endpoint per remote method and projects each method's
requirements onto it, so the standard authorization middleware enforces them. You can
still add blanket requirements — `app.MapSvelteRemote().RequireAuthorization()` — and they
apply on top.

That is not the whole story, though, because HTTP is not the only way a remote function
runs. With `AddJintSSR()`, awaited queries are dispatched **in process** during server
rendering through `ISvelteSsrFetchHandler`, which never passes through routing and
therefore never sees endpoint metadata. So authorization lives on the descriptor, and the
SSR bridge evaluates the identical requirements against `HttpContext.User` before running
anything.

The practical consequence: a protected query cannot leak into server-rendered HTML for an
anonymous visitor, and **the SSR engine you pick does not change who is allowed in**.
(The Node.js and Bun renderers make real loopback HTTP requests, so they were always
subject to the endpoints; Jint is the one that needed this.)

A render with no `HttpContext` — a warm-up render, say — has no user to authorize, so a
protected query is refused rather than run as nobody.

## Cross-site request forgery

### Page forms

Razor Pages validates an antiforgery token on every POST. `enhance()` sends it as the
`RequestVerificationToken` header:

```svelte
const applyUpdate = enhance({ token: () => data.antiforgeryToken, onUpdate: (d) => (data = d) });
```

`AddSvelteNet()` sets `AntiforgeryOptions.HeaderName` when the app has not chosen one —
ASP.NET configures no header name by default and would otherwise reject the post. An
explicit setting of your own is left alone. A form that already renders a hidden
`__RequestVerificationToken` input works without passing `token`.

### Remote functions

`[Command]` and `[Form]` POSTs are protected by two independent checks:

1. **A custom `X-SvelteNet` header.** A cross-origin page cannot set a custom header
   without a CORS preflight your app would have to approve.
2. **Origin.** A request carrying an `Origin` that is not your application's is rejected
   outright. Browsers always attach `Origin` to cross-origin requests, so this holds even
   if a permissive CORS policy (`AllowAnyOrigin()` with `AllowAnyHeader()`) would have let
   the header through. A request with no `Origin` at all is not from a browser and cannot
   be CSRF'd, so it is allowed — that is what keeps server-to-server callers working.

Plain `[Form]` posts made without JavaScript cannot set a custom header, so they are
required to be same-origin by `Origin` or `Referer` instead.

`[Query]` is a GET and must stay side-effect free. Do not mutate state in a query.

## The SSR bundle is not public

`ServerOutput` (`.svelte-net/server`) is deliberately outside `wwwroot`. It contains your
server-rendering bundle, which may embed logic you do not intend to publish. Keep it out
of any static-file root if you change the default.

## Jint renders share module state

Pooled Jint engines keep their module graph — and so any module-level state in the SSR
bundle — alive across renders and across users. Do not hold per-request state at module
scope in a `.svelte` file. The Node.js and Bun renderers start a fresh process per render
and do not share this behaviour. See [SSR renderers](ssr.md).

## Loopback rendering with Node.js and Bun

Those renderers resolve relative `fetch` calls against an address reported by the running
server, never the incoming `Host` header, and forward `Authorization` and `Cookie` so
authenticated queries keep the caller's identity. Set `BaseUrl` explicitly behind a proxy,
and edit `ForwardHeaders` to change what is passed on.
