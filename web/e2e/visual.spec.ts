import { Page, expect, test } from '@playwright/test';
import { DESKTOP, DemoApi, PHONE, SOMSRI, openRound } from './support';

/**
 * Locks the look after the PrimeNG migration (ADR-0006): vendor at 390px, portal and dashboard at 1280px.
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

test('vendor at 390px', async ({ page }) => {
  await page.setViewportSize(PHONE);
  await as(page, 'vendor');
  await page.goto('/vendor');
  await expect(page.getByText('ตลาดนัดเขตบางพลัด').first()).toBeVisible();
  await expect(page.getByRole('heading', { name: 'คำขอของฉัน' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'บทบาท' })).toBeVisible();
  await settle(page);
  await expect(page).toHaveScreenshot('vendor-start.png', shot);
});

test('vendor with lock B2 at 390px', async ({ page, request }) => {
  const api = new DemoApi(request);
  await api.submit(SOMSRI);
  await api.checkEligibility();
  await api.approve(await api.runAllocation());
  await page.setViewportSize(PHONE);
  await as(page, 'vendor');
  await page.goto('/vendor');
  const card = page.getByRole('listitem').filter({ hasText: 'ได้ล็อค B2' });
  await card.getByRole('button', { name: 'สละสิทธิ์', exact: true }).click();
  await expect(card).toContainText('สละล็อค B2 ให้ผู้สำรอง?');
  await expect(page.getByRole('combobox', { name: 'บทบาท' })).toBeVisible();
  await settle(page);
  // The confirm-by deadline is approval time + 3 days on the ticking demo clock
  await expect(page).toHaveScreenshot('vendor-won-forfeit.png', {
    ...shot,
    mask: [card.locator('.deadline')],
  });
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
