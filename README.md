# nucleics.org

Site for nuclear abundance advocacy. North star: **100 MWh of electricity per person per year**.

## Stack

- Blazor SSR (.NET 11, no WASM, no interactive render modes, no AOT) exported to flat files in `output/`.
- Hosted on Cloudflare Pages as static files; the build runs on Cloudflare via `build.sh`.
- The original single page (`index.html`, `styles.css`, `assets/`) now lives in the Blazor site: markup in `Components/Pages/Home.razor` + `Components/Layout/MainLayout.razor`,
  CSS and images in `src/Nucleics.Web/wwwroot` (referenced with fingerprinted `@Assets[...]`). The exported home page is pixel-identical to the old one (headless Chrome, 1280 and 390 px) and its body markup matches the original token for token. The live site is just that home page + `404.html`, `sitemap.xml`, `robots.txt` and `_headers`: there are no other pages, and no blog is published (see "How to add a post"). The root files were removed; `_headers` stays at the repo root and is copied into `output/`.

## Deploy to Cloudflare Pages

Settings: **Build command** `./build.sh` (exec bit 100755 is committed; `bash build.sh` also works; a *bare* `build.sh` does NOT work: the current directory is not on PATH, so the shell answers `not found`, exit 127) ·
**Build output directory** `output` · production branch `main` · root directory `/`. Then attach custom domain `nucleics.org`.
`build.sh` downloads the pinned .NET SDK at build time (see "Cloudflare Pages (build.sh)" below).

Manual direct upload (after running `./build.sh` locally): `npx wrangler pages deploy output --project-name=nucleics`.

## Mark

The header mark is a seven-shell uranium diagram: gaps encode the 92 electrons (2/8/18/32/21/9/2), amber nucleus, inbound neutron with a quiet trail. Fission as controlled process — never boom.

---

## Blazor SSR build (Kaizen)

A **Blazor SSR → flat files** build on **.NET 11** (no WASM, no interactive render modes, no AOT).
Design brief: [bridge#21](https://github.com/kijanawoodard/bridge/issues/21). Findings: [docs/kaizen-spike-notes.md](docs/kaizen-spike-notes.md).

```
Nucleics.slnx
src/
  Kaizen.StaticSite/   exporter: endpoint inventory, export, check, sitemap/robots/404, asset materialisation
  Kaizen.BlogEngine/   Markdig + YamlDotNet post service, YAML front matter (IPostService)
  Kaizen.Seo/          <SeoHead/> (title/description/canonical/OG/Twitter) + JSON-LD helpers
  Nucleics.Web/        the site: layout, pages, content/posts/*.md, glue (BlogRouteSource)
tests/                 Kaizen.StaticSite.Tests, Kaizen.BlogEngine.Tests (xunit; not used by build.sh)
Directory.Packages.props  central package versions (no Version on any PackageReference)
scripts/               verify-output.py, verify-seo.py, demo-check.sh
```

The three `Kaizen.*` libraries have **no references to each other**. Only `Nucleics.Web` references all three and
wires them together (`Glue/BlogRouteSource.cs` adapts the blog's `IPostService` to the exporter's `IStaticRouteSource`).

### Build / run / export / check

Requires the .NET 11 SDK (`dotnet-install.sh --channel 11.0 --quality preview`; tested with 11.0.100-rc.1).

```bash
dotnet build Nucleics.slnx                          # compile everything
dotnet run --project src/Nucleics.Web              # normal live server on http://localhost:5263 (launchSettings, Development; exporter inactive)
dotnet run --project src/Nucleics.Web -- export    # write ./output  (alias: --static-export)
dotnet run --project src/Nucleics.Web -- check     # CI gate: no files written, exit 1 on any problem
dotnet run --project src/Nucleics.Web -- routes    # print every endpoint + the ComponentTypeMetadata filter result
dotnet test Nucleics.slnx                          # unit tests: exporter (route filter, [Authorize] skip, header limits) + blog engine (front matter, posts)

python3 scripts/verify-output.py output            # serve output/ with python http.server, assert every referenced URL is HTTP 200
python3 scripts/verify-seo.py output               # canonical / og / JSON-LD / sitemap / robots assertions
```

`export` boots the real app on a random loopback port, fetches each route with `HttpClient`, and writes
`output/<route>/index.html`, `404.html`, `sitemap.xml`, `robots.txt`, `kaizen-manifest.json` and every static asset
(fingerprinted `@Assets[...]` files, RCL `_content/*`) plus `_headers` / `_redirects` copied as-is from the repo root when present. `output/` is git-ignored.
The Blazor framework scripts (`_framework/*`) and the `<ImportMap/>` that names them are **not** exported by default: no page ships JavaScript
(output ≈ 170 KB instead of 4.9 MB). `export --keep-framework` brings both back (needed once you add interactivity / enhanced navigation).
`export` exits non-zero (and deletes any stale `output/`) on a non-200 route, an unhandled exception, an empty route inventory, a missing 404 probe,
or a page that references a dropped asset. Dangling internal links fail **`check` only**; `export` just reports them.
`check` fails (exit 1) on any non-200 route, a missing 404 page, any unreachable asset, and any internal link
(`a/link/img/script`, canonical included) that points at something not generated. Add `--output <dir>` to change the folder.

### How to add a route

1. Add `src/Nucleics.Web/Components/Pages/Pricing.razor` with `@page "/pricing"` and a `<SeoHead Title="…" Description="…" />`.
2. Link to it with a trailing slash: `<a href="/pricing/">` (exports as `pricing/index.html`; Cloudflare Pages serves `/pricing/`).
3. `dotnet run --project src/Nucleics.Web -- check` — the page is picked up automatically (it is a Razor component endpoint).
   - Opt out: `@attribute [ExcludeFromStaticExport("why")]`.
   - `[Authorize]` pages are excluded automatically (the export runs anonymous; `[AllowAnonymous]` wins). `--include-auth` disables that, for diagnostics.
   - A parameterised route (`@page "/team/{id}"`) is **not exported** (loud warning) until you register an
     `IStaticRouteSource` whose `Template` is `"/team/{id}"` and which yields `{ ["id"] = "…" }` per page (see `Glue/BlogRouteSource.cs`).

### How to add a post

The blog engine, `/blog` and `/blog/{slug}` are in place, but **nothing is published**: the only sample post (`content/posts/why-nuclear.md`) and `draft-unfinished.md` are `draft: true`,
the nav does not link to the blog, and the blog index is switched off (`BlogIndexGate`, an `IStaticPageGate`) while there is no published post, so production ships no `/blog` and no placeholder content.
See it working without touching the repo: `scripts/demo-blog.sh` (publishes a copy of the sample into a temp folder via `--Blog:ContentPath=…` and exports to a temp dir).

1. Create `src/Nucleics.Web/content/posts/my-post.md`:
   ```markdown
   ---
   title: My post
   date: 2026-10-05
   description: One-sentence summary (meta description, OG, JSON-LD).
   tags: [nuclear]
   draft: false
   ---
   (front matter is standard YAML read by YamlDotNet; allowed keys: title, date, updated, description, author, slug, tags, draft — see below)
   Markdown body…
   ```
   The file name is the slug (`/blog/my-post/`); override with `slug:`. `draft: true` hides it from the blog and the export.
   **Publish the first post:** delete the `draft: true` line from `why-nuclear.md` (or add a new post). The blog index appears in the export and sitemap automatically,
   but the nav has no Blog link by design: add `<a href="@Home/blog/">Blog</a>`-style link to `MainLayout.razor` (a plain `/blog/` is fine) when you want it, and review the sample's copy first.
2. `dotnet run --project src/Nucleics.Web -- export` → `output/blog/my-post/index.html`, listed in `sitemap.xml` and on `/blog/`.
   The page is the single catch-all `Components/Pages/BlogPost.razor` (`@page "/blog/{slug}"`); an unknown slug returns 404.

Front matter (YamlDotNet, strict): `title` and `date` are required; `date`/`updated` are an ISO date (`2026-10-01`) or date-time (`2026-10-01T09:30:00Z`, `2026-10-01 09:30:00`);
`tags` is an inline `[a, b]` or a dash list; text values may be plain, `'single'` or `"double"` quoted, or multi-line (`|` literal, `>` folded); `draft: true|false`; `#` comments are fine.
**Anything else is an error, never skipped**: an unknown key (a typo like `dratf:`), malformed YAML, a wrong type (`draft: maybe`, `tags: single`), a duplicate key or a missing/empty block throws
`<file>, line N: invalid front matter: …` (e.g. `…/bad.md, line 4: invalid front matter: unknown key 'dratf' (allowed, lowercase camelCase: title, date, …)`), which fails `export` and `check` with exit 1 (and the live site on first use).
Drafts are validated too. Demo: `scripts/demo-malformed-post.sh` (evidence in `docs/malformed-post-demo.txt`).

### Cloudflare Pages (build.sh)

Cloudflare Pages settings: **Build command** `./build.sh` (mode 100755 is committed; `bash build.sh` is equivalent; not bare `build.sh`) · **Build output directory** `output` · root directory `/`.
`build.sh` (POSIX `sh`, `set -e`) downloads `dotnet-install.sh`, installs the exact pinned SDK (`--version 11.0.100-rc.1.26425.128`, not a channel)
into `./dotnet`, and runs only `./dotnet/dotnet run --project src/Nucleics.Web -c Release -- export`.
The build image needs `curl`, `tar`/`gzip` and a shell; nothing else (no global dotnet, no Node, no libicu: `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`).
`global.json` pins the same SDK (`rollForward: latestFeature`, prerelease allowed) so `dotnet` anywhere in the repo resolves to it; bump it together with `DOTNET_VERSION` in `build.sh` (e.g. to `11.0.100` at GA).
`./dotnet` and `dotnet-install.sh` are git-ignored. `_headers` (repo root) and an optional `_redirects` are copied into `output/`;
the exporter warns on stderr (never fails) beyond Cloudflare's limits: `_headers` 100 rules / 2,000-char lines; `_redirects` 2,000 static + 100 dynamic (splat `*` or `:placeholder`) / 1,000-char lines / statics should precede dynamics.
