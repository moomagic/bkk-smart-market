import { Page, expect, test } from '@playwright/test';
import { DESKTOP, DemoApi, openRound, switchRole } from './support';

/** The portal's smaller promises from the final audit (#57): errors, limits, keyboard, motion and type. */

test.beforeEach(async ({ page, request }) => {
  await new DemoApi(request).reset();
  await page.setViewportSize(DESKTOP);
});

async function asOfficer(page: Page): Promise<void> {
  await page.goto('/vendor');
  await switchRole(page, 'officer');
}

test('a failed load is announced, and the requests tab does not claim 0 requests', async ({ page }) => {
  await page.route(/\/api\/rounds\/[^/]+\/stall-requests$/, (route) => route.fulfill({ status: 500, body: '{}' }));
  await asOfficer(page);
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await expect(page.getByRole('alert').filter({ hasText: 'โหลดคำขอไม่สำเร็จ' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'คำขอ', exact: true })).toBeVisible();
});

test('past the reject limit the bar says so in warn, and the dialog says why confirm is off', async ({
  page,
  request,
}) => {
  const api = new DemoApi(request);
  await api.checkEligibility();
  await api.reject(await api.runAllocation(), 'ทดสอบ 1');
  await api.reject(await api.runAllocation(), 'ทดสอบ 2');
  await api.runAllocation();

  await asOfficer(page);
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await expect(page.getByText('ปฏิเสธได้อีก 0 ครั้ง')).toHaveClass(/warn-text/);
  await page.getByRole('button', { name: 'ปฏิเสธผล' }).click();
  await expect(page.getByRole('button', { name: 'ยืนยันการปฏิเสธ' })).toHaveAccessibleDescription(
    /ครบจำนวนที่เจ้าหน้าที่ปฏิเสธได้แล้ว/,
  );
});

test('Esc closes only the dialog on top, not the audit panel behind it', async ({ page, request }) => {
  const api = new DemoApi(request);
  await api.checkEligibility();
  await api.runAllocation();

  await asOfficer(page);
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await page.getByRole('button', { name: 'ประวัติการดำเนินการ', exact: true }).click();
  const audit = page.getByRole('region', { name: /ประวัติการดำเนินการ ·/ });
  await expect(audit).toBeVisible();
  await page.getByRole('button', { name: 'อนุมัติผล', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'อนุมัติผลจัดสรร' });
  await expect(dialog).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(audit).toBeVisible();
});

test('the audit panel puts each time above its entry instead of in a squeezed column', async ({
  page,
  request,
}) => {
  await new DemoApi(request).checkEligibility();
  await asOfficer(page);
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await page.getByRole('button', { name: 'ประวัติการดำเนินการ', exact: true }).click();
  const audit = page.getByRole('region', { name: /ประวัติการดำเนินการ ·/ });
  await expect(audit.locator('.when-inline').first()).toBeVisible();
  await expect(audit.locator('.p-timeline-event-opposite').first()).toBeHidden();
});

test('disabled controls say why: the run tab before a draw, the check with no requests', async ({ page }) => {
  await asOfficer(page);
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await expect(page.getByRole('tab', { name: 'ผลจัดสรร' })).toHaveAccessibleDescription(/ยังไม่ได้รันจัดสรร/);
  await openRound(page, 'ตลาดนัดเขตบางพลัด');
  await expect(page.getByRole('button', { name: 'ตรวจสิทธิทั้งรอบ' })).toBeDisabled();
});

test('under reduced motion the tab bar does not slide; dialog titles use the heading face', async ({
  page,
  request,
}) => {
  const api = new DemoApi(request);
  await api.checkEligibility();
  await api.runAllocation();
  await page.emulateMedia({ reducedMotion: 'reduce' });

  await asOfficer(page);
  await openRound(page, 'ปากซอยจรัญฯ 79');
  const inkBar = page.locator('.p-tablist-active-bar');
  await expect(inkBar).toHaveCSS('transition-duration', '0s');
  await page.getByRole('button', { name: 'อนุมัติผล', exact: true }).click();
  await expect(page.locator('.p-dialog-title')).toHaveCSS('font-family', /Bai Jamjuree/);
});

test('the demo tools button is named by what it shows', async ({ page }) => {
  await asOfficer(page);
  await expect(page.getByRole('button', { name: /^เวลา demo .+ · เครื่องมือ demo$/ })).toBeVisible();
});
