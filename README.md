# nucleics.org

Single-page static site for nuclear abundance advocacy.
North star: **100 MWh of electricity per person per year**.

## Stack

- Plain `index.html` + `styles.css`
- No build step, no framework
- Logo SVGs and favicons in `assets/`

## Local preview

Open `index.html` in a browser, or:

```bash
npx --yes serve .
```

## Deploy to Cloudflare Pages

### Option A — connect GitHub

1. [Cloudflare Dashboard](https://dash.cloudflare.com) → Workers & Pages → Create → Pages → Connect to Git
2. Select `kijanawoodard/nucleics`
3. Build settings: **Framework preset** None · **Build command** empty · **Output directory** `/` (or leave blank)
4. Deploy, then attach custom domain `nucleics.org`

### Option B — direct upload (Wrangler)

```bash
npx wrangler pages deploy . --project-name=nucleics
```

Then add the `nucleics.org` custom domain in the Pages project settings.

## Mark

The header mark is a seven-shell uranium diagram: gaps encode the 92 electrons (2/8/18/32/21/9/2), amber nucleus, inbound neutron with a quiet trail. Fission as controlled process — never boom.
