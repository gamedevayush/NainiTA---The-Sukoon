// Street View reference shots along the route (reference only, never put into the game). Throw-away Brave profile,
// window off-screen, so the owner's own browser is not touched. For each point: the map with the Street View layer,
// a click on the blue coverage line nearest the point (the Google car's pano, not a user photo sphere), then the
// left and right views of that pano.
// usage: node sv.js <route.json> <outDir> [workers] [onlyIdx,...]
const path = require('path');
const fs = require('fs');
const { execFileSync } = require('child_process');
const { chromium } = require('C:/Users/Lenovo/.claude/skills/google-flow-automation/node_modules/playwright');

const BRAVE = 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe';
const PY = 'D:/NainiTA---The-Sukoon/Tools/.venv/Scripts/python.exe';
const FIND = path.join(__dirname, 'findline.py');
const route = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const outDir = process.argv[3];
const workers = +(process.argv[4] || 4);
const only = process.argv[5] ? new Set(process.argv[5].split(',').map(Number)) : null;
fs.mkdirSync(outDir, { recursive: true });
const metaFile = path.join(outDir, 'sv_meta.json');
const meta = fs.existsSync(metaFile) ? JSON.parse(fs.readFileSync(metaFile, 'utf8')) : {};
const save = () => fs.writeFileSync(metaFile, JSON.stringify(meta, null, 1));

const jobs = [];
route.samples.forEach((s, i) => { if (!only || only.has(i)) jobs.push({ i, s }); });

function parse(u) {
  const m = u.match(/@(-?\d+\.\d+),(-?\d+\.\d+),([\d.]+)a,/);
  if (!m) return null;
  return { lat: +m[1], lng: +m[2], alt: +m[3], pano: (u.match(/!1s([^!]+)!2e(\d+)/) || [])[1] || '', type: (u.match(/!1s[^!]+!2e(\d+)/) || [])[1] || '' };
}

async function openPano(page, j, tmp) {
  await page.goto(`https://www.google.com/maps/@${j.s.lat},${j.s.lng},19z/data=!5m1!1e5?hl=en`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.waitForTimeout(6500);
  await page.screenshot({ path: tmp, type: 'png' });
  const cands = execFileSync(PY, ['-I', FIND, tmp]).toString().trim().split(/\r?\n/).filter(Boolean).map(l => l.split(',').map(Number));
  for (const [x, y, d] of cands) {
    await page.mouse.click(x, y);
    await page.waitForTimeout(5500);
    const p = parse(page.url());
    if (p && p.type === '0') return { ...p, clickPx: d };
    // a photo sphere or nothing: back to the map and try the next blue pixel
    await page.goto(`https://www.google.com/maps/@${j.s.lat},${j.s.lng},19z/data=!5m1!1e5?hl=en`, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForTimeout(5000);
  }
  return null;
}

(async () => {
  // one browser window per worker (tabs behind another tab, or windows off-screen, get throttled and render late)
  const ctxs = [];
  for (let w = 0; w < workers; w++) {
    ctxs.push(await chromium.launchPersistentContext(path.join(outDir, '..', `sv_profile${w ? '_' + w : ''}`), {
      executablePath: BRAVE, headless: false, viewport: { width: 1280, height: 720 },
      args: [`--window-position=${-2600 + w * 40},${w * 40}`, '--lang=en-US', '--ignore-gpu-blocklist',
             '--disable-background-timer-throttling', '--disable-renderer-backgrounding', '--disable-backgrounding-occluded-windows'],
    }));
  }
  let next = 0, done = 0;
  async function worker(page, wi) {
    const tmp = path.join(outDir, `_map_${wi}.png`);
    while (next < jobs.length) {
      const j = jobs[next++];
      const tag = String(j.i).padStart(3, '0');
      if (meta[j.i] && (meta[j.i].none || fs.existsSync(path.join(outDir, `real_${tag}_R.jpg`)))) { done++; continue; }
      try {
        const p = await openPano(page, j, tmp);
        if (!p) { meta[j.i] = { none: true }; fs.copyFileSync(tmp, path.join(outDir, `nopano_${tag}.png`)); }
        else {
          for (const [side, d] of [['L', -90], ['R', 90]]) {
            const h = ((j.s.yaw + d) % 360 + 360) % 360;
            await page.goto(`https://www.google.com/maps/@?api=1&map_action=pano&pano=${p.pano}&heading=${h.toFixed(1)}&pitch=0&fov=90&hl=en`, { waitUntil: 'domcontentloaded', timeout: 60000 });
            await page.waitForTimeout(7000);
            await page.screenshot({ path: path.join(outDir, `real_${tag}_${side}.jpg`), type: 'jpeg', quality: 85 });
          }
          const date = (await page.locator('text=/Image capture:/').first().innerText().catch(() => '')).trim();
          meta[j.i] = { ...p, date };
        }
        done++;
        console.log(`${done}/${jobs.length} #${j.i} ${p ? `${p.lat},${p.lng} ${p.pano} ${meta[j.i].date}` : 'NO CAR PANO'}`);
      } catch (e) { console.log('ERR', j.i, e.message.slice(0, 160)); }
      save();
    }
  }
  const pages = [];
  for (const c of ctxs) pages.push(c.pages()[0] || await c.newPage());
  await Promise.all(pages.map(worker));
  save();
  for (const c of ctxs) await c.close();
})();
