# Kaizen spike notes (branch `kaizen-spike`)

Plan: [bridge#21](https://github.com/kijanawoodard/bridge/issues/21) (body + two comments). Everything below was **run for real** on a Linux box
(Debian 13) unless it is in the "Unverified / not done" list.

## Versions actually used
- **.NET SDK 11.0.100-rc.1.26425.128**, runtime Microsoft.NETCore.App / Microsoft.AspNetCore.App **11.0.0-rc.1.26425.128**
  (installed with `dotnet-install.sh --channel 11.0 --quality preview --install-dir ~/.dotnet`). Real .NET 11 RC1, not a .NET 10 fallback.
- Markdig 1.4.0, YamlDotNet 18.1.0 (pinned after a floating restore). No other NuGet packages.
- `TargetFramework=net11.0`, `PublishAot=false`, no WASM, no interactive render modes. All three libs use `FrameworkReference Microsoft.AspNetCore.App`.

## Layout
```
Nucleics.sln
src/Kaizen.StaticSite   exporter. IStaticRouteSource, ExcludeFromStaticExportAttribute, RouteInventoryBuilder, StaticSiteExporter, LinkScanner, EndpointReport
src/Kaizen.BlogEngine   MarkdownPostService (Markdig + YamlDotNet), IPostService, AddMarkdownContent(...)
src/Kaizen.Seo          SeoHead.razor, JsonLdScript.razor, JsonLd helpers, SeoOptions/AddSeo (Razor class library)
src/Nucleics.Web        Blazor SSR site: App/Routes/MainLayout, Home/About/Learn/BlogIndex/BlogPost/NotFound/Admin(stub), Glue/BlogRouteSource, content/posts/*.md
scripts/                verify-output.py, verify-seo.py, demo-check.sh
docs/                   this file, spike1-endpoints.txt, spike5-check-demo.txt
```
No `ProjectReference` between the three `Kaizen.*` libs (checked with `grep ProjectReference src/*/*.csproj`: only `Nucleics.Web` has them).
Existing root files (`index.html`, `styles.css`, `assets/`, `_headers`, original README content) are untouched; the site's `wwwroot/` has **copies** of `styles.css` and `assets/*`.
`.gitignore`: `bin/ obj/ output/` (+ IDE dirs). No workflow / deploy config exists in the repo and none was added.

## Spike results

### 1. Endpoint list / `ComponentTypeMetadata` filter — PASS (full dump: `docs/spike1-endpoints.txt`, run via `-- routes`)
- 52 endpoints total; **4** carry `ComponentTypeMetadata` (`/`, `/about`, `/learn`, `/not-found` at that moment) — exactly the Razor `@page`s. All 4 are `RouteEndpoint`.
- The other **48 lack it** (verified, not assumed): 46 static-asset endpoints (`assets/*`, `styles*.css`, `_framework/blazor.*.js`, `.map`, `.gz`),
  `/_framework/opaque-redirect` (Blazor framework endpoint) and the `{**path:file}` fallback. So the plan's open question is answered: **no non-page endpoint has the metadata**.
- Metadata types seen on asset endpoints: `StaticAssetDescriptor` (public, `Microsoft.AspNetCore.StaticAssets`), `BuildAssetMetadata`, `ContentEncodingMetadata` (gzip variants). Page endpoints carry `ComponentTypeMetadata`, `RootComponentMetadata`,
  `ConfiguredRenderModesMetadata`, `ResourceAssetCollection`, `ImportMapDefinition`, `RequireAntiforgeryTokenAttribute`, and **all component-class attributes are copied** (our `ExcludeFromStaticExportAttribute` and `[Authorize]` showed up).
- Enumerating `((IEndpointRouteBuilder)app).DataSources` after `app.Build()` + `StartAsync` works (we start first anyway). `RouteEndpoint.RoutePattern.RawText` gives `/blog/{slug}`.

### 2. Export 3-page brochure — PASS
`dotnet run --project src/Nucleics.Web -- export` → `output/index.html`, `about/index.html`, `learn/index.html` (+ `blog/index.html`, `blog/why-nuclear/index.html`), `404.html`, `sitemap.xml`, `robots.txt`, `kaizen-manifest.json`, assets.
Shared header/footer live once in `MainLayout.razor`. Mechanism: Kestrel on `127.0.0.1:0` (random port) in-process, plain `HttpClient` (no auto-redirect), sequential fetch.
`404.html` is captured by requesting a bogus path (must be HTTP 404) through `UseStatusCodePagesWithReExecute("/not-found")` → `NotFound.razor` (marked `[ExcludeFromStaticExport]` so it isn't a page/sitemap entry).
Also verified: works from a clean `git clone` (`dotnet run` without `--no-build`), under `ASPNETCORE_ENVIRONMENT=Development`, and from `dotnet publish` output (`dotnet Nucleics.Web.dll export --output …`).

### 3. Fingerprinted assets materialised — PASS
Asset endpoints are found by `StaticAssetDescriptor` metadata (selector-less ones; `.gz/.br` skipped) and **fetched over HTTP from the running app**, then written to `output/<route>` (both `styles.css` and `styles.w6sn8uuepi.css`, etc. — 18 files).
`@Assets["styles.css"]` renders `styles.w6sn8uuepi.css`; `cmp` confirms the fingerprinted file is byte-identical to the original `styles.css`.
`scripts/verify-output.py output` (plain `python http.server` over `output/`, parses every HTML page incl. 404.html, resolves against `<base href>`, checks href/src/srcset/importmap targets):
`html pages: 6 | references checked: 93 (13 unique URLs) | HTTP 200: 93 | NOT 200: 0 | RESULT: PASS`.
Negative control (output copy with `assets/`, `_framework/`, `styles*.css` deleted): FAIL with 404s for the fingerprinted css/png/svg and importmap targets — the checker does detect missing assets.
(An earlier run, before the blog existed, correctly FAILED on the then-dangling `/blog/` nav link: 53 ok / 5 × 404.)

### 4. Markdown post through BlogEngine + Seo — PASS
`content/posts/why-nuclear.md` (front matter: title, date, updated, description, author, tags, draft) → `/blog/{slug}` (single catch-all page `BlogPost.razor`) → `output/blog/why-nuclear/index.html`, in `sitemap.xml` and on `/blog/`.
`content/posts/draft-unfinished.md` has `draft: true`: not listed, not exported, not in sitemap, live server returns 404 for it. Unknown slug → HTTP 404 (`NavigationManager.NotFound()` in `OnInitialized`).
Head output on the post: `<title>`, meta description, `link rel=canonical https://nucleics.org/blog/why-nuclear/`, `og:*` (type=article), `twitter:*`, `article:published_time/modified_time/tag`, JSON-LD **Organization**, **BlogPosting**, **BreadcrumbList** (all parse as valid JSON).
`scripts/verify-seo.py output`: every exported page has canonical + og:title + og:url + description + JSON-LD; canonical matches path; sitemap == exported pages; robots.txt has the Sitemap line; PASS (5 pages).
Glue: `Glue/BlogRouteSource : IStaticRouteSource` (Template `/blog/{slug}`) in the site feeds slugs; BlogEngine has no idea the exporter exists.

### 5. `check` mode + auth filter — PASS (`docs/spike5-check-demo.txt`, reproducible via `scripts/demo-check.sh`)
- clean: exit 0. Temporary dangling link `/does-not-exist/` on About: **exit 1**, `LINK /about: <a href="/does-not-exist/"> -> /does-not-exist (not generated)`.
  Temporary pages `/broken` (`NotFound()`) and `/boom` (throws): **exit 1**, `HTTP 404` and `HTTP 500`. Reverted → exit 0 again (script restores via `git checkout`/`rm` + trap).
- `[Authorize]` filter: `/admin` stub page with `@attribute [Authorize]` + stub cookie auth. Default: `skip /admin -- requires authorization ([Authorize])`, check passes. With `--include-auth`: `FAIL /admin -> 302`, exit 1 (proves the page is real, protected, and that anonymous export would fail on it).
  Detection = `IAuthorizeData` in endpoint metadata and no `IAllowAnonymous`.
- `check` also: requires the 404 probe to return 404; fetches every asset (200); warns (non-fatal) `STALE` if an existing `output/kaizen-manifest.json` disagrees with the discovered routes (code path written, **not exercised in a demo**).
- `export` fails (exit 1, **writes nothing**) on any non-200 route; dangling links are only reported in export (as in the plan: crawl is report-only), but are fatal in `check` (`FailOnBrokenLinksInCheck`, default true).

## Gotchas / workarounds
- **`dotnet new sln` now makes `.slnx`**; I used `--format sln` (classic). `dotnet sln add … --solution-folder src` **failed for the web project** in RC1 ("Solution folder 'src' already contains a project with the filename 'Kaizen.StaticSite.csproj'" — it seems to walk project references); worked with `--in-root`. So `Nucleics.Web` sits at the solution root while the libs are in the `src` solution folder. Cosmetic.
- **cwd/content root under `dotnet run --project X`** is the project folder, not the invoker's cwd → a relative `output` would land in `src/Nucleics.Web/output`. `AddStaticSite` anchors relative `OutputPath` at the nearest dir containing a `.sln`/`.slnx`/`.git`. Content root (for `content/posts`) = project dir; `content/**` is copied to output/publish via `<Content … CopyToOutputDirectory>`.
- **`UseStatusCodePagesWithReExecute("/not-found")` + `NotFoundPage`**: works for both unmatched URLs and `NavigationManager.NotFound()`; the re-executed page's canonical would be `/not-found/` so `SeoHead` omits canonical/og:url when `NoIndex` (found by the asset/link verifier: 404.html's canonical pointed at an ungenerated URL).
- **`<script type="application/ld+json">` rendered by Blazor is attribute-encoded** (`ld&#x2B;json`). Browsers/Google decode it but naive parsers wouldn't; `JsonLdScript` emits the whole element as `MarkupString` to keep a literal `+`. `JsonLd.Serialize` uses the default encoder, which escapes `<`, `>`, `&` so a post title can't break out of the script element.
- **Razor attribute quoting**: C# with string literals inside a component parameter needs single-quoted attributes (`JsonLdData='new object[] { JsonLd.WebSite("A","B") }'`).
- **`<BasePath />` (new in .NET 11) works** in RC1 (lives in `Microsoft.AspNetCore.Components.Endpoints`; needed `@using`); renders `<base href="/" />`, so fingerprinted relative URLs (`styles.w6sn8uuepi.css`) resolve from `/about/` and `/blog/x/`. `LinkScanner` and `verify-output.py` both honour `<base>`.
- `ImportMap` renders on every page, naming `_framework/blazor.web.js` and `blazor.server.js` (fingerprinted). **No page loads any JS** (no `<script src>`; no enhanced nav), yet those files (≈440 KB + maps, ~4.8 of 4.9 MB of `output/`) get materialised so every importmap target exists. If the site stays script-free, drop `<ImportMap />` and add `_framework/*` to `AssetExcludePatterns` (one line each). Left as-is to demonstrate the mechanism.
- `FocusOnNavigate` removed from Routes (emits `<blazor-focus-on-navigate>` only meaningful with JS).
- Links are written with trailing slashes (`/about/`) to match `route/index.html` hosting without a 308; routes are matched case-sensitively in `check` (static hosts are case-sensitive, Blazor routing is not).
- Blazor routing treats `/about` and `/about/` as the same page, so the exporter fetches the slash-less form.
- Logging: `Microsoft.Hosting.Lifetime` is set to Warning only for export/check/routes commands (appsettings otherwise keeps Info).
- `UseStaticWebAssets()` is called in export mode; static assets worked in Production env under `dotnet run` and from `publish` output.

## Unverified / not done (explicit)
- **Cloudflare Pages**: nothing deployed or even built there; "deploys with an empty build command" and "fingerprinted assets work on CF Pages" are **unverified** (verified only against python `http.server`). Pushing the branch may trigger a CF *preview* deployment of the repo root if the Pages project builds non-production branches — I did not touch or check CF config.
- CF-specific behaviours not tested: `/about` → `/about/` 308 redirect, `_headers`/`_redirects` interplay (the existing `_headers` file is at repo root, **not copied into `output/`** — a deploy of `output/` would lose those security headers; needs a decision).
- Auth: only a stub (cookie scheme, no login page, `[Authorize]` on `/admin`). A real auth setup, `[AllowAnonymous]` precedence on a page, and policy-based `[Authorize(Policy=…)]` pages are untested beyond metadata detection. The `/admin` stub + `AddAuthentication().AddCookie("stub")` in `Program.cs` should be removed before real use.
- Interactive render modes: no warning implemented for interactive-only pages (plan mentions it) — not done, no interactive pages exist.
- Not implemented: `BasePath` sub-path hosting configuration (component is rendered, but `/` is hard-coded in links), query-string pages, QuickGrid, tag pages, `lastmod` in sitemap, `<changefreq>`, per-page sitemap opt-out other than `[ExcludeFromStaticExport]`, Markdown media-folder copying (plan goal), RCL `_content/` and scoped CSS were **not exercised** (no RCL static assets/scoped CSS present — `Kaizen.Seo` has no wwwroot), compression selectors only skipped, not tested with `.br`.
- `StaticAssetsEndpointDataSourceHelper` / `staticwebassets.build.endpoints.json` fallback not needed (the public `StaticAssetDescriptor` route worked) — not implemented.
- Link scanning is regex-based (a/link/img/script/source href/src); `srcset`, CSS `url()`, `<meta>` URLs and `#fragment` existence are not checked by the exporter (`verify-output.py` does cover `srcset`/importmap but not CSS `url()`).
- `check`'s STALE-vs-manifest comparison was not demonstrated.
- OG image: none (only `summary` twitter card) — the repo has no raster brand image in `assets/` (favicons only).
- No unit-test project; verification is via the CLI + scripts. No CI changes (by instruction).
- Performance/scale: sequential fetching; not tested beyond 5 pages.
- Content `html` from markdown is trusted (raw HTML allowed) — fine for a single author, not for user-generated content.
- .NET 11 is RC1; behaviours (BasePath, NotFound, asset metadata) may shift before GA (2026-11-10).

## Open questions for the captain
1. Drop `<ImportMap/>` and the `_framework/*` assets from the export while the site is script-free (saves ~4.8 MB, no JS shipped), or keep for enhanced navigation later?
2. `_headers` (and a future `_redirects`): should the exporter copy a site-level `_headers`/`public/` passthrough into `output/`? (Today it would be lost.)
3. Canonical form: trailing slash (`/about/`, current) vs none — Cloudflare Pages 308s `/about` → `/about/`, so I chose slash.
4. Should dangling links fail `export` too (currently `check` only, per the plan's "report-only")?
5. Cloudflare Pages build image needs the .NET 11 SDK to run the export — acceptable, or commit/pre-build `output/` in CI (GitHub Action) instead?
6. Keep `Nucleics.Web` page set (Home/About/Learn/Blog) as a placeholder for real content? About/Learn copy is placeholder text I wrote.
