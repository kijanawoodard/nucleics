# Nucleics: end-state IA and roadmap

Draft from 2026-10-04. Tone: never boom, dry, evidence first.

## End-state IA

- **Home** keeps the 100 MWh metric and points into the three sections.
- **Learn** (hub): 100 MWh metric page, energy density, how reactors work, land use, fuel comparisons (hard on coal, soft on gas, fair to solar and wind), waste series, SMR series (including Valar).
- **Argue** (hub): TMI dose vs Denver, coal radioactivity, "why not just solar" (rooftop barriers, agrivoltaics), myths and FAQ, quote bank. Every claim links to sources.
- **Build** (hub): policy, plants, people who ship; get involved; mailing list or contact; shop later.
- **Blog**: chronological feed at `/blog/slug`. Tags decide which hub lists a post.
- **About**: follows the key-facts layout from bridge#25, plus a short "how we handle evidence" page.

## Web pages vs blog posts

- **Web pages** are evergreen and maintained in place: reference, explainers, About, FAQ, get involved. Clean top-level URLs such as `/learn/energy-density/`.
- **Blog posts** are dated and not rewritten beyond fixes: series episodes, reactions, quick takes.
- Test: if you would want it read unchanged in three years, it is a web page. If it is tied to a moment, it is a post.
- Series (waste, SMR) run as blog episodes with an evergreen landing page such as `/series/waste/`.
- Hubs show evergreen pages first, then the latest posts with the hub's tags. Posts link to the evergreen page for the underlying claim.

## Layouts and authoring

- Blog posts: markdown, post layout (single column, date, tags).
- Plain web pages (About, FAQ, evidence standards): markdown with `layout: page`, wider and plainer, no byline.
- Showpiece pages (home, 100 MWh explainer, Build): Razor pages built from shared components (metric block, cards, charts).
- Start with all web pages as Razor if easier; URLs do not change when moving plain ones to markdown later.

## Dates on every page

- Every web page and post shows a quiet "Published ... · Updated ..." line. It does not need to be prominent.
- `updated` is set by hand only when the content really changed, never by a build timestamp. With no `updated`, show only the published date.
- The same values feed sitemap `lastmod` and JSON-LD `datePublished` / `dateModified`.
- Build it in the shared Kaizen pieces so other sites get it too. See `docs/kaizen-blogengine-ideas.md`.

## Roadmap

### Phase 1: Foundation
- Publish "why nuclear" (currently `draft: true`).
- Write the 100 MWh metric page (the keystone other pieces link back to).
- Turn Learn, Argue and Build into real hub pages; add Blog and About to the nav.
- Add the `layout` field and the page template.
- Add published/updated dates to pages and posts, plus sitemap lastmod and JSON-LD.
- About page with the xkcd 1162 origin story (bridge#25).

### Phase 2: Waste series (bridge#6)
4 to 6 episodes plus a series landing page.

### Phase 3: Argue starter pack
TMI dose (bridge#19), coal radioactivity (bridge#10), rooftop solar and agrivoltaics (bridge#8). Tie to merch slogans (bridge#17).

### Phase 4: SMR series (bridge#14)
Valar deep-dives and landscape explainers.

### Phase 5: Build and shop
Get-involved page, newsletter, merch once the logo is locked.

Priority order is by dependency, then ready research, then search interest.
