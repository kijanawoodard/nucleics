# Kaizen.BlogEngine: ideas

Parked ideas for the blog engine library. Nothing here is committed work.

## Content freshness signals (from kijanawoodard.com Search Console review, 2026-10-04)

**Problem.** Old posts sit in Search Console as "Crawled - currently not indexed". Refreshing a post helps only if Google can tell it changed, and only if the date is honest. Google ignores date bumps without a real content change, and it ignores `lastmod` values from sites that update them blindly (for example to the build date).

**Idea.** Treat one author-controlled date, `updated`, as the single source of truth and emit it everywhere Google reads freshness:

1. **Sitemap `<lastmod>`**: per-post `lastmod` from `updated`, falling back to `date`. Pages with no meaningful date get no `lastmod` (never the build date).
2. **Visible line on the post**: "Updated: <date>" next to the published date, only when `updated` is set.
3. **Structured data**: JSON-LD `Article` with `datePublished` and `dateModified`.

**Where it fits.**
- `Post` already carries `Updated` (nullable), so the engine side is mostly done. The work is exposing it.
- The sitemap is written by Kaizen.StaticSite (`StaticSiteExporter`). It needs an optional per-route last-modified hook so Kaizen.StaticSite stays independent of Kaizen.BlogEngine. The blog engine supplies dates through that hook.
- JSON-LD likely belongs in Kaizen.Seo, which already owns head metadata.

**Rules to keep it honest.**
- `updated` is set by hand in front matter, only when content actually changed.
- Optionally, `check` mode warns when a post's file changed in git but `updated` did not (and the reverse).

**Open questions.**
- Should `updated` default from git history, or stay manual only? Manual is simpler and harder to abuse.
- Is a visible "Updated" line wanted on every theme, or opt-in?

Origin: the kijanawoodard.com blog (BlazorStatic based) has 46 posts from 2013 to 2025 and no `lastmod`, canonical, or JSON-LD today.
