// Regenerates the docs/screenshots/ gallery from a *seeded* instance (see scripts/seed-demo.sh).
// Logs in as the demo admin once, discovers the demo league/club/season/session ids from the
// API, then drives each view at the gallery's fixed 1400x910 viewport (device-scale 1) and
// writes the PNGs. Light + dark variants are produced where the gallery keeps both.
//
// Usage (via scripts/capture-screenshots.sh, which sets cwd so playwright-core resolves):
//   node scripts/capture-screenshots.mjs                 # every image
//   node scripts/capture-screenshots.mjs players profile # only the named images
//
// Env: BASE_URL (default http://localhost:5080), ADMIN_EMAIL, ADMIN_PASSWORD,
//      CHROME_BIN (browser executable; defaults to a system chromium/chrome).
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { existsSync } from 'node:fs';
import { createRequire } from 'node:module';

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const ROOT = join(SCRIPT_DIR, '..');
// playwright-core lives in ClientApp/node_modules; resolve it from there regardless of cwd.
const require = createRequire(join(ROOT, 'ClientApp', 'package.json'));
const { chromium } = require('playwright-core');

const BASE = process.env.BASE_URL ?? 'http://localhost:5080';
const EMAIL = process.env.ADMIN_EMAIL ?? 'admin@smash-dates.test';
const PASSWORD = process.env.ADMIN_PASSWORD ?? 'correct-horse-battery';
const OUT_DIR = join(ROOT, 'docs', 'screenshots');
const VIEWPORT = { width: 1400, height: 910 };

const only = new Set(process.argv.slice(2));
const wanted = (name) => only.size === 0 || only.has(name) || only.has(name.replace(/-dark$/, ''));

function findBrowser() {
  const candidates = [
    process.env.CHROME_BIN,
    '/usr/bin/chromium',
    '/usr/bin/chromium-browser',
    '/usr/bin/google-chrome',
    '/opt/google/chrome/chrome',
  ].filter(Boolean);
  const found = candidates.find((p) => existsSync(p));
  if (!found) {
    throw new Error(
      `No browser found. Set CHROME_BIN to a Chromium/Chrome executable. Tried: ${candidates.join(', ')}`,
    );
  }
  return found;
}

// Each shot: { name, path(ids) => urlPath, dark?, before?(page, ids) }.
// `before` runs after navigation, to open a modal / expand an in-page panel before the shot.
const SHOTS = [
  { name: 'leagues', path: () => '/leagues' },
  { name: 'league-detail', path: (id) => `/leagues/${id.league}` },
  { name: 'dark-mode', dark: true, path: (id) => `/leagues/${id.league}` },
  {
    name: 'season-setup',
    path: (id) => `/leagues/${id.league}?tab=seasons`,
    before: (page, id) => expandSeasonPanel(page, id.draftSeasonName, 'Weeks'),
  },
  {
    name: 'fixtures',
    path: (id) => `/leagues/${id.league}?tab=seasons`,
    before: (page, id) => expandSeasonPanel(page, id.scheduledSeasonName, 'Fixtures'),
  },
  {
    name: 'match-actions',
    path: (id) => `/leagues/${id.league}?tab=seasons`,
    before: (page, id) => expandSeasonPanel(page, id.scheduledSeasonName, 'Fixtures'),
  },
  {
    name: 'standings',
    path: (id) => `/leagues/${id.league}?tab=seasons`,
    before: (page, id) => expandSeasonPanel(page, id.scheduledSeasonName, 'Table'),
  },
  { name: 'club-detail', path: (id) => `/clubs/${id.club}` },
  { name: 'players', path: (id) => `/clubs/${id.club}?tab=players` },
  {
    name: 'csv-import',
    path: (id) => `/clubs/${id.club}?tab=players`,
    before: async (page) => {
      await page.getByRole('button', { name: 'Import CSV' }).click();
      await page.getByRole('dialog').waitFor({ timeout: 5000 });
    },
  },
  { name: 'pegboard-sessions', path: (id) => `/clubs/${id.club}?tab=sessions` },
  { name: 'pegboard-sessions-dark', dark: true, path: (id) => `/clubs/${id.club}?tab=sessions` },
  { name: 'pegboard-board', path: (id) => `/clubs/${id.club}/pegboard/${id.session}` },
  {
    name: 'pegboard-board-dark',
    dark: true,
    path: (id) => `/clubs/${id.club}/pegboard/${id.session}`,
  },
  { name: 'profile', path: () => '/profile' },
  { name: 'public-standings', path: (id) => `/public/leagues/${id.league}` },
  { name: 'public-standings-dark', dark: true, path: (id) => `/public/leagues/${id.league}` },
];

// Expand an in-page panel under the Seasons tab. Each label is unique to one season's status
// in the demo (only the Draft season shows "Weeks"; only the scheduled one shows
// "Fixtures"/"Table"), so a page-level click by exact name targets the right row.
async function expandSeasonPanel(page, seasonName, buttonLabel) {
  const btn = page.getByRole('button', { name: buttonLabel, exact: true }).first();
  await btn.scrollIntoViewIfNeeded();
  await btn.click();
  await page.waitForTimeout(500);
  // The toggle's label flips to "Close" on expand, so don't re-target buttonLabel — scroll the
  // season row (and the panel rendered right after it) into view via the season name instead.
  if (seasonName) {
    await page
      .getByText(seasonName, { exact: false })
      .first()
      .scrollIntoViewIfNeeded()
      .catch(() => {});
  }
}

async function discoverIds(api) {
  const leagues = await (await api.get(`${BASE}/api/leagues`)).json();
  const league = leagues[0].id;
  const clubs = await (await api.get(`${BASE}/api/clubs`)).json();
  const club = (clubs.find((c) => c.shortCode === 'RIV') ?? clubs[0]).id;
  const seasons = await (await api.get(`${BASE}/api/leagues/${league}/seasons`)).json();
  const draft = seasons.find((s) => s.status === 'Draft');
  const scheduled = seasons.find((s) => ['Active', 'Proposed', 'Closed'].includes(s.status));
  const sessions = await (await api.get(`${BASE}/api/clubs/${club}/pegboard/sessions`)).json();
  const open = sessions.find((s) => s.status === 'Open') ?? sessions[0];
  return {
    league,
    club,
    draftSeasonName: draft?.name,
    scheduledSeasonName: scheduled?.name,
    session: open?.id,
  };
}

const browser = await chromium.launch({ executablePath: findBrowser(), headless: true });
let failures = 0;
try {
  const ctx = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  const page = await ctx.newPage();

  // Log in once; the auth cookie then covers both API discovery and the browser navigations.
  // (The /api/auth/login endpoint is rate-limited, so capture runs log in a single time.)
  await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
  await page.fill('input[type="email"], input[formcontrolname="email"]', EMAIL);
  await page.fill('input[type="password"], input[formcontrolname="password"]', PASSWORD);
  await page.getByRole('button', { name: /sign in|log in/i }).click();
  await page.waitForURL((u) => !u.pathname.endsWith('/login'), { timeout: 15000 });

  const ids = await discoverIds(ctx.request);

  for (const shot of SHOTS.filter((s) => wanted(s.name))) {
    try {
      await page.evaluate((t) => localStorage.setItem('theme', t), shot.dark ? 'dark' : 'light');
      // Prefer 'networkidle' so async tab/data loads finish before we shoot; fall back to a
      // plain load if it never settles (e.g. the pegboard board holds an SSE stream open).
      await page
        .goto(`${BASE}${shot.path(ids)}`, { waitUntil: 'networkidle', timeout: 12000 })
        .catch(() => page.goto(`${BASE}${shot.path(ids)}`, { waitUntil: 'load' }));
      if (shot.before) await shot.before(page, ids);
      await page.waitForTimeout(600);
      const file = join(OUT_DIR, `${shot.name}.png`);
      await page.screenshot({ path: file });
      console.log(`✓ ${shot.name}.png`);
    } catch (err) {
      failures++;
      console.error(`✗ ${shot.name}: ${err.message.split('\n')[0]}`);
      if (process.env.CAPTURE_DEBUG) {
        await page.screenshot({ path: `/tmp/fail-${shot.name}.png` }).catch(() => {});
        const btns = await page
          .locator('button')
          .evaluateAll((e) => e.map((x) => x.textContent.trim()).filter(Boolean))
          .catch(() => []);
        console.error(`   url=${page.url()} btns=${JSON.stringify(btns)}`);
      }
    }
  }
} finally {
  await browser.close();
}
process.exit(failures ? 1 : 0);
