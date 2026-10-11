import { expect, test } from '@playwright/test';
import { CHAI, DESKTOP, DemoApi, PHONE, ROLE_LABEL, openRound, switchRole, vendorTab } from './support';

/** Behaviour that a template rewrite can silently lose: enabled/disabled conditions and the role switcher. */

test.beforeEach(async ({ page, request }) => {
  await new DemoApi(request).reset();
  await page.setViewportSize(DESKTOP);
});

test('the role switcher still shows the chosen role after a reload', async ({ page }) => {
  await page.goto('/vendor');
  await switchRole(page, 'officer');
  await expect(page).toHaveURL(/\/portal$/);

  await page.reload();
  await expect(page.getByRole('combobox', { name: 'บทบาท' })).toHaveText(ROLE_LABEL.officer);
  await expect(page.locator('app-role-switcher')).toHaveAttribute('data-confirmed-user', 'officer-bangphlat');
});

test('running the allocation is blocked until every request has been checked', async ({ page }) => {
  await page.goto('/vendor');
  await switchRole(page, 'officer');
  await openRound(page, 'ปากซอยจรัญฯ 79');

  await expect(page.getByText(/มีคำขอที่ยังไม่ได้ตรวจสิทธิ \d+ คำขอ/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'รันจัดสรร' })).toBeDisabled();

  await page.getByRole('button', { name: 'ตรวจสิทธิทั้งรอบ' }).click();
  await expect(page.getByRole('button', { name: 'รันจัดสรร' })).toBeEnabled();
  await expect(page.getByText(/มีคำขอที่ยังไม่ได้ตรวจสิทธิ/)).toHaveCount(0);
});

test('a reject needs a reason, and past the limit only the executive may reject', async ({
  page,
  request,
}) => {
  const api = new DemoApi(request);
  await api.checkEligibility();
  await api.reject(await api.runAllocation(), 'ทดสอบ: ปฏิเสธครั้งที่ 1');
  await api.reject(await api.runAllocation(), 'ทดสอบ: ปฏิเสธครั้งที่ 2');
  await api.runAllocation();

  await page.goto('/vendor');
  await switchRole(page, 'officer');
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await expect(page.getByText('ปฏิเสธได้อีก 0 ครั้ง')).toBeVisible();
  await page.getByRole('button', { name: 'ปฏิเสธผล' }).click();

  const reason = page.getByLabel('เหตุผลในการปฏิเสธ (ผู้ค้าจะเห็นเหตุผลนี้)');
  await reason.fill('ทดสอบ: ปฏิเสธครั้งที่ 3');
  await expect(
    page.getByText('ครบจำนวนที่เจ้าหน้าที่ปฏิเสธได้แล้ว ต้องให้ผู้บริหารเป็นผู้ปฏิเสธ'),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'ยืนยันการปฏิเสธ' })).toBeDisabled();

  await switchRole(page, 'executive');
  await page.goto('/portal');
  await openRound(page, 'ปากซอยจรัญฯ 79');
  await page.getByRole('button', { name: 'ปฏิเสธผล' }).click();
  await expect(page.getByRole('button', { name: 'ยืนยันการปฏิเสธ' })).toBeDisabled(); // no reason yet
  await page.getByLabel('เหตุผลในการปฏิเสธ (ผู้ค้าจะเห็นเหตุผลนี้)').fill('ทดสอบ: ผู้บริหารปฏิเสธ');
  await expect(page.getByText('ผู้บริหารปฏิเสธเพิ่มได้')).toBeVisible();
  await expect(page.getByRole('button', { name: 'ยืนยันการปฏิเสธ' })).toBeEnabled();
});

test('an eligibility review can be answered Upheld or Corrected, and the answer needs text', async ({
  page,
  request,
}) => {
  const api = new DemoApi(request);
  await api.checkEligibility();
  await api.approve(await api.runAllocation());
  await api.fileReview(CHAI, 'ทดสอบ: อบรมสุขาภิบาลแล้ว รอใบรับรอง');

  await page.goto('/vendor');
  await switchRole(page, 'officer');
  await openRound(page, 'ปากซอยจรัญฯ 79');
  const reviews = page.getByRole('region', { name: 'คำขอทบทวน' });
  const review = reviews.getByRole('listitem').filter({ hasText: 'ชัย ด.' });
  await review.getByRole('button', { name: 'ตอบคำขอ' }).click();

  // ชัย ด. is Ineligible, so Corrected is allowed; the template disables it for any other status.
  await expect(review.getByRole('radio', { name: 'ยืนตามเดิม' })).toBeChecked();
  await expect(review.getByRole('radio', { name: 'แก้ไข (ตรวจสิทธิผิด)' })).toBeEnabled();
  await expect(review.getByRole('button', { name: 'ส่งคำตอบ' })).toBeDisabled();
  await review.getByLabel('คำตอบถึงผู้ค้า').fill('ทดสอบ: ยืนตามเดิม ยังไม่มีใบรับรอง');
  await expect(review.getByRole('button', { name: 'ส่งคำตอบ' })).toBeEnabled();
});

test('a vendor forfeit asks for a second, deliberate click', async ({ page, request }) => {
  const api = new DemoApi(request);
  await page.goto('/vendor/spaces');
  // สมศรี ใ. submits through the UI, then the officer side is driven through the API.
  await page
    .getByRole('region', { name: 'พื้นที่ค้าขายในเขตบางพลัด' })
    .getByRole('listitem')
    .filter({ hasText: 'ปากซอยจรัญฯ 79' })
    .getByRole('button', { name: 'ยื่นคำขอ' })
    .click();
  await expect(page.getByRole('status')).toContainText('ยื่นคำขอแล้ว');
  await api.checkEligibility();
  await api.approve(await api.runAllocation());
  await page.goto('/vendor');

  const card = page
    .getByRole('region', { name: 'คำขอของฉัน' })
    .getByRole('listitem')
    .filter({ hasText: 'ได้ล็อค B2' });
  await card.getByRole('button', { name: 'สละสิทธิ์' }).click();
  await expect(card).toContainText('สละล็อค B2 ให้ผู้สำรอง?');
  await card.getByRole('button', { name: 'ไม่สละ' }).click();
  await expect(card.getByRole('button', { name: 'ยืนยันสิทธิ์', exact: true })).toBeEnabled();
  await expect(card).not.toContainText('สละล็อค B2 ให้ผู้สำรอง?');
});

test('the vendor app opens on its requests tab, with its own app bar instead of the site header', async ({ page }) => {
  await page.setViewportSize(PHONE);
  await page.goto('/vendor');
  await expect(page).toHaveURL(/\/vendor\/requests$/);
  await expect(page.getByRole('heading', { level: 1, name: 'คำขอของฉัน' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'BKK Smart Market' })).toHaveCount(0);
  const tabs = page.getByRole('navigation', { name: 'เมนูหลัก' });
  await expect(tabs.getByRole('link', { name: 'คำขอ', exact: true })).toHaveAttribute('aria-current', 'page');

  await vendorTab(page, 'หาพื้นที่');
  await expect(page).toHaveURL(/\/vendor\/spaces$/);
  await expect(page.getByRole('heading', { level: 1, name: 'หาพื้นที่ขายของ' })).toBeVisible();
  await expect(tabs.getByRole('link', { name: 'หาพื้นที่', exact: true })).toHaveAttribute('aria-current', 'page');

  // A direct link to a tab works too
  await page.goto('/vendor/assistant');
  await expect(page.getByRole('heading', { level: 1, name: 'ถามระเบียบ' })).toBeVisible();
});

test('the portal keeps the site header', async ({ page }) => {
  await page.goto('/vendor');
  await switchRole(page, 'officer');
  await expect(page.getByRole('link', { name: 'BKK Smart Market' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'เมนูหลัก' })).toHaveCount(0);
});
