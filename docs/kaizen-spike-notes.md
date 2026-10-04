# Kaizen spike notes (written on the kaizen-spike branch, 2026-10)

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
src/Nucleics.Web        Blazor SSR site: App/Routes/MainLayout, Home/About/Learn/BlogIndex/BlogPost/NotFound, Glue/BlogRouteSource, content/posts/*.md
tests/Kaizen.StaticSite.Tests   xunit tests for the exporter (references only Kaizen.StaticSite)
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
- `[Authorize]` filter: during this spike an `/admin` stub page + stub cookie auth proved it end-to-end (skipped by default; `FAIL /admin -> 302` with `--include-auth`). **That stub page and the auth wiring were removed before merge** (2026-10-04); the site has no auth-gated page now. The filter is covered by unit tests only (see below).
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
- (Superseded 2026-10-04: now dropped by default, see follow-up.) `ImportMap` rendered on every page, naming `_framework/blazor.web.js` and `blazor.server.js` (fingerprinted). **No page loads any JS** (no `<script src>`; no enhanced nav), yet those files (≈440 KB + maps, ~4.8 of 4.9 MB of `output/`) get materialised so every importmap target exists. If the site stays script-free, drop `<ImportMap />` and add `_framework/*` to `AssetExcludePatterns` (one line each). Left as-is to demonstrate the mechanism.
- `FocusOnNavigate` removed from Routes (emits `<blazor-focus-on-navigate>` only meaningful with JS).
- Links are written with trailing slashes (`/about/`) to match `route/index.html` hosting without a 308; routes are matched case-sensitively in `check` (static hosts are case-sensitive, Blazor routing is not).
- Blazor routing treats `/about` and `/about/` as the same page, so the exporter fetches the slash-less form.
- Logging: `Microsoft.Hosting.Lifetime` is set to Warning only for export/check/routes commands (appsettings otherwise keeps Info).
- `UseStaticWebAssets()` is called in export mode; static assets worked in Production env under `dotnet run` and from `publish` output.

## Unverified / not done (explicit)
- **Cloudflare Pages** (still true): nothing deployed or even built there; "deploys with an empty build command" and "fingerprinted assets work on CF Pages" are **unverified** (verified only against python `http.server`). Pushing the branch may trigger a CF *preview* deployment of the repo root if the Pages project builds non-production branches — I did not touch or check CF config.
- CF-specific behaviours not tested: `/about` → `/about/` 308 redirect, `_headers`/`_redirects` interplay ((resolved 2026-10-04: `_headers` is now copied into `output/`)).
- Auth: the `[Authorize]` skip is covered **only by unit tests** (`tests/Kaizen.StaticSite.Tests`: `[Authorize]`, `[Authorize(Policy=…)]`, `[Authorize]`+`[AllowAnonymous]` precedence, `ExcludeAuthorizedPages=false`, plus opt-out attribute, parameterised-route expansion/warning and the `_headers`/`_redirects` limit warnings; 10 tests, `dotnet test tests/Kaizen.StaticSite.Tests`). **End-to-end with real authentication is unverified** (no login flow, no real policy, no real redirect/challenge behaviour beyond the earlier throwaway stub).
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

## Follow-up 2026-10-04: Cloudflare Pages via `build.sh`
Captain decisions applied: Pages builds with `bash build.sh` (output dir `output`), `_framework/*` + ImportMap dropped by default (`--keep-framework` overrides), trailing-slash canonicals kept, dangling links fail `check` only.
- **build.sh**: installs the exact SDK with `dotnet-install.sh --version 11.0.100-rc.1.26425.128 --install-dir ./dotnet` (verified: reports that version), skips the download if `./dotnet` already has it, then uses only `./dotnet/dotnet` (`DOTNET_ROOT` set, `DOTNET_MULTILEVEL_LOOKUP=0`, telemetry off, `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`). Uses `curl -f` so an HTTP error fails the build. After the export it asserts `output/{index.html,404.html,sitemap.xml,robots.txt}` are non-empty.
- **global.json** pin (`11.0.100-rc.1.26425.128`, `latestFeature`, `allowPrerelease`): helpful because the repo then refuses to silently build with another preview SDK and matches `build.sh`; mostly harmless, but anyone without that SDK (or a newer feature band) gets a clear error rather than a different compiler. Two places to bump at GA: `global.json` and `build.sh`.
- **`_headers` / `_redirects`** are copied byte-for-byte from the repo root (where the existing `_headers` lives; `cmp` identical) into the output root. They are *not* put in `wwwroot` (it would publish them as ordinary assets). No `_redirects` example was added (no real redirects to invent). Limit warnings (stderr, exit stays 0) verified with temp oversize files: `_headers` 102 rules + a 2,110-char line; `_redirects` 2,002 static + 101 dynamic + a 1,115-char line + statics after a dynamic. Limits come from Cloudflare's docs (read 2026-10-04): headers 100 rules / 2,000 chars per line; redirects 2,000 static + 100 dynamic / 1,000 chars per declaration. (Your brief said 1,000-char lines generally; Cloudflare documents 2,000 for `_headers`, so that is what I used.) The parser is a heuristic (URL line = unindented non-comment line; dynamic = `*` or `:letter` in the source field).
- **Exit codes** (all proven via `sh build.sh`): temp 404 page → exit 1, no output/ left; temp `IStaticRouteSource` that throws → `[kaizen] FAILED with an unhandled exception`, exit 1; compile error → exit 1; reverted → exit 0. New hard failures: empty route inventory; a page referencing an excluded asset (ImportMap forced on while `_framework` dropped → 10 errors, exit 1); stale `output/` is deleted on a failed export. `--output .` (or any dir containing the repo root/cwd) is refused, since export wipes its output dir.
- **Framework drop**: output 4.9 MB → 168 KB; `verify-output.py` 81/81 URLs return 200 with the default, 93/93 with `--keep-framework`. The link scanner now also reads `<script type="importmap">` targets, so a stray reference to a dropped file fails the export. `FrameworkAssetPatterns` (`_framework/*`) is the list that is dropped when `KeepFramework` is false.

## Follow-up 2026-10-04 (b): merge prep
- `/admin` stub page and the stub cookie auth/authorization wiring removed from `Nucleics.Web`; the `[Authorize]`-skip logic and `--include-auth` stay in the exporter. `scripts/demo-check.sh` and `docs/spike5-check-demo.txt` no longer mention the stub.
- New `tests/Kaizen.StaticSite.Tests` (xunit 2.9, Microsoft.NET.Test.Sdk, references only `Kaizen.StaticSite`), added to `Nucleics.sln`. `build.sh` runs `dotnet run --project src/Nucleics.Web`, which builds only the site and its three libraries, so tests are neither built nor needed on Cloudflare.
- `build.sh` is committed as mode 100755; `./build.sh` runs directly.

## Open questions for the captain
1. ~~Drop ImportMap/_framework~~ — decided: dropped by default, `--keep-framework` to override.
2. ~~`_headers` passthrough~~ — done (repo-root `_headers`/`_redirects` copied as-is).
3. ~~Canonical form~~ — decided: keep trailing slash.
4. ~~Dangling links~~ — decided: `check` only.
5. ~~CF build image~~ — decided: Pages runs `build.sh`, which installs the SDK.
6. Keep `Nucleics.Web` page set (Home/About/Learn/Blog) as a placeholder for real content? About/Learn copy is placeholder text I wrote.
