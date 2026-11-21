// Street View reference shots of the Nainital fire station (Mallital) for building it in the game - reference only,
// never put this imagery into the game. Runs Brave with a throw-away profile (not the owner's), so his open browser
// and tabs are not touched.
// usage: node streetview.js <outDir> "<lat,lon>" "<heading,heading,...>" [pitch] [fov] [tag]
//    or: node streetview.js <outDir> search "<query>" [tag]
const path = require('path');
const fs = require('fs');
const { chromium } = require('C:/Users/Ayush Singth/.claude/skills/google-flow-automation/node_modules/playwright');

const BRAVE = 'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe';
const outDir = process.argv[2];
const mode = process.argv[3];
fs.mkdirSync(outDir, { recursive: true });

async function dismiss(page) {
  for (const t of ['Accept all', 'Reject all', 'I agree', 'Got it', 'No thanks']) {
    const b = page.getByRole('button', { name: t });
    if (await b.count().catch(() => 0)) { await b.first().click().catch(() => {}); await page.waitForTimeout(800); }
  }
}

(async () => {
  const profile = path.join(process.env.TEMP || 'C:/Temp', 'nainita_sv_profile');
  const ctx = await chromium.launchPersistentContext(profile, {
    executablePath: BRAVE, headless: false, viewport: { width: 1600, height: 900 },
    args: ['--window-position=-2400,0', '--lang=en-US', '--ignore-gpu-blocklist'],
  });
  const page = ctx.pages()[0] || await ctx.newPage();
  try {
    if (mode === 'search') {
      const q = process.argv[4], tag = process.argv[5] || 'search';
      await page.goto('https://www.google.com/maps/search/' + encodeURIComponent(q) + '?hl=en', { waitUntil: 'domcontentloaded', timeout: 60000 });
      await page.waitForTimeout(6000);
      await dismiss(page);
      await page.waitForTimeout(4000);
      await page.screenshot({ path: path.join(outDir, `${tag}.png`) });
      console.log('url', page.url());
      fs.writeFileSync(path.join(outDir, `${tag}.txt`), page.url() + '\n' + (await page.innerText('body').catch(() => '')).slice(0, 4000));
    } else {
      const ll = mode, heads = (process.argv[4] || '0').split(',').map(Number);
      const pitch = process.argv[5] || '5', fov = process.argv[6] || '80', tag = process.argv[7] || 'sv';
      for (const h of heads) {
        const url = `https://www.google.com/maps/@?api=1&map_action=pano&viewpoint=${ll}&heading=${h}&pitch=${pitch}&fov=${fov}&hl=en`;
        await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
        await page.waitForTimeout(5000);
        await dismiss(page);
        await page.waitForTimeout(6000);
        const f = path.join(outDir, `${tag}_h${h}.png`);
        await page.screenshot({ path: f });
        console.log(f, page.url());
      }
    }
  } catch (e) { console.log('ERR', e.message); }
  await ctx.close();
})();
