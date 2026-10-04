# nucleics.org

Site for nuclear abundance advocacy. North star: **100 MWh of electricity per person per year**.

## Stack

- Blazor SSR (.NET 11, no WASM, no interactive render modes, no AOT) exported to flat files in `output/`.
- Hosted on Cloudflare Pages as static files; the build runs on Cloudflare via `build.sh`.
- `index.html`, `styles.css` and `assets/` at the repo root are the original hand-written single page. They are kept for reference and are **no longer deployed**
  (the site's copies live in `src/Nucleics.Web/wwwroot`).

## Deploy to Cloudflare Pages

Settings: **Build command** `./build.sh` (or `build.sh`; the executable bit is committed; `bash build.sh` also works) ·
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
Nucleics.sln
src/
  Kaizen.StaticSite/   exporter: endpoint inventory, export, check, sitemap/robots/404, asset materialisation
  Kaizen.BlogEngine/   Markdig + YAML front matter post service (IPostService)
  Kaizen.Seo/          <SeoHead/> (title/description/canonical/OG/Twitter) + JSON-LD helpers
  Nucleics.Web/        the site: layout, pages, content/posts/*.md, glue (BlogRouteSource)
scripts/               verify-output.py, verify-seo.py, demo-check.sh
```

The three `Kaizen.*` libraries have **no references to each other**. Only `Nucleics.Web` references all three and
wires them together (`Glue/BlogRouteSource.cs` adapts the blog's `IPostService` to the exporter's `IStaticRouteSource`).

### Build / run / export / check

Requires the .NET 11 SDK (`dotnet-install.sh --channel 11.0 --quality preview`; tested with 11.0.100-rc.1).

```bash
dotnet build Nucleics.sln                          # compile everything
dotnet run --project src/Nucleics.Web              # normal live server (exporter inactive)
dotnet run --project src/Nucleics.Web -- export    # write ./output  (alias: --static-export)
dotnet run --project src/Nucleics.Web -- check     # CI gate: no files written, exit 1 on any problem
dotnet run --project src/Nucleics.Web -- routes    # print every endpoint + the ComponentTypeMetadata filter result

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
   - `[Authorize]` pages are excluded automatically (the export runs anonymous).
   - A parameterised route (`@page "/team/{id}"`) is **not exported** (loud warning) until you register an
     `IStaticRouteSource` whose `Template` is `"/team/{id}"` and which yields `{ ["id"] = "…" }` per page (see `Glue/BlogRouteSource.cs`).

### How to add a post

1. Create `src/Nucleics.Web/content/posts/my-post.md`:
   ```markdown
   ---
   title: My post
   date: 2026-10-05
   description: One-sentence summary (meta description, OG, JSON-LD).
   tags: [nuclear]
   draft: false
   ---
   Markdown body…
   ```
   The file name is the slug (`/blog/my-post/`); override with `slug:`. `draft: true` hides it from the blog and the export.
2. `dotnet run --project src/Nucleics.Web -- export` → `output/blog/my-post/index.html`, listed in `sitemap.xml` and on `/blog/`.
   The page is the single catch-all `Components/Pages/BlogPost.razor` (`@page "/blog/{slug}"`); an unknown slug returns 404.

### Cloudflare Pages (build.sh)

Cloudflare Pages settings: **Build command** `./build.sh` or `build.sh` (mode 100755 is committed; `bash build.sh` is equivalent) · **Build output directory** `output` · root directory `/`.
`build.sh` (POSIX `sh`, `set -e`) downloads `dotnet-install.sh`, installs the exact pinned SDK (`--version 11.0.100-rc.1.26425.128`, not a channel)
into `./dotnet`, and runs only `./dotnet/dotnet run --project src/Nucleics.Web -c Release -- export`.
The build image needs `curl`, `tar`/`gzip` and a shell; nothing else (no global dotnet, no Node, no libicu: `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`).
`global.json` pins the same SDK (`rollForward: latestFeature`, prerelease allowed) so `dotnet` anywhere in the repo resolves to it; bump it together with `DOTNET_VERSION` in `build.sh` (e.g. to `11.0.100` at GA).
`./dotnet` and `dotnet-install.sh` are git-ignored. `_headers` (repo root) and an optional `_redirects` are copied into `output/`;
the exporter warns on stderr (never fails) beyond Cloudflare's limits: `_headers` 100 rules / 2,000-char lines; `_redirects` 2,000 static + 100 dynamic (splat `*` or `:placeholder`) / 1,000-char lines / statics should precede dynamics.
