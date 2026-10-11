import { Page, expect, test } from '@playwright/test';
import { DESKTOP, DemoApi, PHONE, SOMSRI, openRound } from './support';

/**
 * Locks the look after the PrimeNG migration (ADR-0006): the vendor app at 390px (and its phone frame at 1280px),
 * portal and dashboard at 1280px.
 * States come from the demo seed (the first allocation run uses it), so they are the same every run; only
 * clock-derived times are masked, as the demo clock keeps ticking from Demo:Now.
 * Run with `npm run e2e:visual`; after an intended visual change add `-- --update-snapshots`.
 * Baselines are per platform (…-visual-darwin.png) and need the Google Fonts the app loads; another OS
 * records its own on first run, which is why this is a separate project from `npm run e2e`.
 */

const shot = { fullPage: true, animations: 'disabled' as const };

/** Fonts loaded, and no hover state left on whatever was clicked last. */
async function settle(page: Page): Promise<void> {
  await page.mouse.move(0, 0);
  await page.evaluate(async () => {
    await document.fonts.ready;
  });
}

async function as(page: Page, role: 'vendor' | 'officer' | 'executive'): Promise<void> {
  await page.addInitScript((r) => localStorage.setItem('bkk-smart-market.role', r), role);
}

test.beforeEach(async ({ request }) => {
  await new DemoApi(request).reset();
});

test('vendor requests tab at 390px', async ({ page }) => {
  await page.setViewportSize(PHONE);
  await as(page, 'vendor');
  await page.goto('/vendor');
  await expect(page.getByText('สวัสดี สมศรี ใ.')).toBeVisible();
  await expect(page.getByTestId('todo')).toContainText('รับคำขอรอบใหม่ถึง');
  await expect(page.getByRole('region', { name: 'คำขอของฉัน' })).toContainText('ปากซอยจรัญฯ 79');
  await settle(page);
  await expect(page).toHaveScreenshot('vendor-requests.png', shot);
});

test('vendor spaces tab with matches at 390px', async ({ page }) => {
  await page.setViewportSize(PHONE);
  await as(page, 'vendor');
  await page.goto('/vendor/spaces');
  const match = page.getByRole('region', { name: 'หาพื้นที่ที่เหมาะกับร้าน' });
  await match.getByLabel('สินค้าที่ขาย').fill('ข้าวแกง');
  await match.getByLabel('ย่านที่สะดวก').fill('จรัญฯ');
  await match.getByRole('button', { name: 'แนะนำพื้นที่' }).click();
  await expect(match.getByRole('listitem').filter({ hasText: '1. ปากซอยจรัญฯ 79' })).toContainText('90/100');
  await settle(page);
  await expect(page).toHaveScreenshot('vendor-spaces-matches.png', shot);
});

test('vendor request with lock B2, asked before forfeit, at 390px', async ({ page, request }) => {
  const api = new DemoApi(request);
  await api.submit(SOMSRI);
  await api.checkEligibility();
  await api.approve(await api.runAllocation());
  await page.setViewportSize(PHONE);
  await as(page, 'vendor');
  await page.goto('/vendor');
  await page.getByRole('listitem').filter({ hasText: 'ได้ล็อค B2' }).getByRole('link').click();
  await page.getByRole('button', { name: 'สละสิทธิ์', exact: true }).click();
  await expect(page.getByText('สละล็อค B2 ให้ผู้สำรอง?')).toBeVisible();
  await settle(page);
  // The confirm-by deadline is approval time + 3 days on the ticking demo clock
  await expect(page).toHaveScreenshot('vendor-won-forfeit.png', {
    ...shot,
    mask: [page.locator('.deadline')],
  });
});

test('vendor app in its phone frame at 1280px', async ({ page }) => {
  await page.setViewportSize(DESKTOP);
  await as(page, 'vendor');
  await page.goto('/vendor');
  await expect(page.getByTestId('todo')).toContainText('รับคำขอรอบใหม่ถึง');
  await settle(page);
  await expect(page).toHaveScreenshot('vendor-frame.png', shot);
});

test('portal draft run at 1280px', async ({ page, request }) => {
  const api = new DemoApi(request);
  await api.submit(SOMSRI);
  await api.checkEligibility();
  await api.runAllocation();
  await page.setViewportSize(DESKTOP);
  await as(page, 'officer');
  await page.goto('/portal');
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await expect(page.getByText('BPL-2569-1025-01').first()).toBeVisible();
  await settle(page);
  // Only the run time ticks; reference no, seed (the demo seed), ran-by and input hash stay in the picture
  const ranAt = page.locator('.run-meta > div').filter({ hasText: 'รันเมื่อ' }).locator('dd');
  await expect(page).toHaveScreenshot('portal-draft-run.png', { ...shot, mask: [ranAt] });
});

test('dashboard at 1280px', async ({ page }) => {
  await page.setViewportSize(DESKTOP);
  await as(page, 'executive');
  await page.goto('/dashboard');
  await expect(page.locator('p-chart canvas')).toHaveCount(3);
  await settle(page);
  await expect(page).toHaveScreenshot('dashboard.png', shot);
});
