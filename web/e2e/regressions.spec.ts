import { expect, test } from '@playwright/test';
import { CHAI, DESKTOP, DemoApi, PHONE, ROLE_LABEL, SOMSRI, openRound, switchRole, vendorTab } from './support';

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

  // The card in the list only leads to the request; every action is on the request's own screen
  const card = page
    .getByRole('region', { name: 'คำขอของฉัน' })
    .getByRole('listitem')
    .filter({ hasText: 'ได้ล็อค B2' });
  await expect(card.getByRole('button')).toHaveCount(0);
  await card.getByRole('link').click();

  const detail = page.getByRole('region', { name: 'รายละเอียดคำขอ' });
  await page.getByRole('button', { name: 'สละสิทธิ์' }).click();
  await expect(page.getByText('สละล็อค B2 ให้ผู้สำรอง?')).toBeVisible();
  await page.getByRole('button', { name: 'ไม่สละ' }).click();
  await expect(page.getByRole('button', { name: 'ยืนยันสิทธิ์', exact: true })).toBeEnabled();
  await expect(page.getByText('สละล็อค B2 ให้ผู้สำรอง?')).toHaveCount(0);
  await expect(detail).toContainText('ได้ล็อค B2');
});

test('← on a request goes back where the vendor came from, or to the requests tab from a direct link', async ({
  page,
}) => {
  await page.setViewportSize(PHONE);
  const back = page.getByRole('button', { name: 'ย้อนกลับ' });
  const previousRound = page
    .getByRole('region', { name: 'คำขอของฉัน' })
    .getByRole('listitem')
    .filter({ hasText: 'รอบ 1 พ.ค. 2569' });

  // From the requests tab: the detail hides the tabs, ← returns
  await page.goto('/vendor');
  await previousRound.getByRole('link').click();
  await expect(page.getByRole('region', { name: 'รายละเอียดคำขอ' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'เมนูหลัก' })).toHaveCount(0);
  const detailUrl = page.url();
  await back.click();
  await expect(page).toHaveURL(/\/vendor\/requests$/);

  // A submit from the spaces tab opens the new request; ← goes back to the spaces tab
  await vendorTab(page, 'หาพื้นที่');
  await page
    .getByRole('region', { name: 'พื้นที่ค้าขายในเขตบางพลัด' })
    .getByRole('listitem')
    .filter({ hasText: 'ปากซอยจรัญฯ 79' })
    .getByRole('button', { name: 'ยื่นคำขอ' })
    .click();
  await expect(page.getByRole('region', { name: 'รายละเอียดคำขอ' })).toContainText('ยื่นคำขอแล้ว');
  await expect(page.getByRole('status')).toContainText('ยื่นคำขอแล้ว · ปากซอยจรัญฯ 79');
  await back.click();
  await expect(page).toHaveURL(/\/vendor\/spaces$/);
  // A notice belongs to the screen it was made on
  await expect(page.getByRole('status')).toHaveText('');

  // Opened straight from a link there is nothing to go back to inside the app: ← lands on the requests tab
  await page.goto(detailUrl);
  await expect(page.getByRole('region', { name: 'รายละเอียดคำขอ' })).toBeVisible();
  await back.click();
  await expect(page).toHaveURL(/\/vendor\/requests$/);
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

test('the requests tab greets the vendor and leads to the spaces tab while a round is open and unapplied', async ({
  page,
}) => {
  await page.setViewportSize(PHONE);
  await page.goto('/vendor');
  await expect(page.getByText('สวัสดี สมศรี ใ.')).toBeVisible();
  // สมศรี has only her previous round's request: nothing to do there, the open round is the next step
  await expect(page.getByTestId('todo')).toContainText('รับคำขอรอบใหม่ถึง 31 ต.ค. 2569');
  await page.getByRole('link', { name: 'หาพื้นที่ขายของ' }).click();
  await expect(page).toHaveURL(/\/vendor\/spaces$/);
});

test('a vendor with no request at all gets an empty state that leads to the spaces tab', async ({ page }) => {
  await page.setViewportSize(PHONE);
  await page.route('**/api/stall-requests/me', (route) => route.fulfill({ json: [] }));
  await page.goto('/vendor');
  await expect(page.getByRole('region', { name: 'คำขอของฉัน' })).toContainText('ยังไม่มีคำขอ');
  await page.getByRole('link', { name: 'หาพื้นที่ขายของ' }).click();
  await expect(page).toHaveURL(/\/vendor\/spaces$/);
});

test('the requests tab carries a dot only while a won lock waits to be confirmed', async ({ page, request }) => {
  const api = new DemoApi(request);
  await page.setViewportSize(PHONE);
  const tab = page.getByRole('navigation', { name: 'เมนูหลัก' }).getByRole('link', { name: 'คำขอ', exact: true });

  await api.submit(SOMSRI);
  await page.goto('/vendor/spaces');
  await expect(page.getByText('ตลาดนัดเขตบางพลัด').first()).toBeVisible();
  await expect(tab).toHaveAccessibleDescription('');

  await api.checkEligibility();
  await api.approve(await api.runAllocation());
  await page.reload();
  // Seen from another tab too
  await expect(tab).toHaveAccessibleDescription('มีคำขอที่รอยืนยันสิทธิ์');

  await vendorTab(page, 'คำขอ');
  await page.getByRole('listitem').filter({ hasText: 'ได้ล็อค B2' }).getByRole('link').click();
  await page.getByRole('button', { name: 'ยืนยันสิทธิ์', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('ยืนยันสิทธิ์แล้ว');
  await page.getByRole('button', { name: 'ย้อนกลับ' }).click();
  await expect(tab).toHaveAccessibleDescription('');
  // Holding B2 for this period, nothing else is to do: no other open round may be applied to (ADR-0003)
  await expect(page.getByText('สวัสดี สมศรี ใ.')).toBeVisible();
  await expect(page.getByTestId('todo')).toHaveCount(0);
});

test('the spaces tab shows matches in place of the list, keeps them through a submit, and returns to the list', async ({
  page,
}) => {
  await page.setViewportSize(PHONE);
  await page.goto('/vendor/spaces');
  const all = page.getByRole('region', { name: 'พื้นที่ค้าขายในเขตบางพลัด' });
  await expect(all).toContainText('ตลาดนัดเขตบางพลัด');

  const match = page.getByRole('region', { name: 'หาพื้นที่ที่เหมาะกับร้าน' });
  await match.getByLabel('ขายอะไร').fill('ข้าวแกง');
  await match.getByLabel('ย่านที่สะดวก').fill('จรัญฯ');
  await match.getByRole('button', { name: 'แนะนำพื้นที่' }).click();
  const first = match.getByRole('listitem').filter({ hasText: '1. ปากซอยจรัญฯ 79' });
  await expect(first).toContainText('90/100');
  // On a phone the matches stand in for the list rather than stacking above it
  await expect(all).toHaveCount(0);

  await first.getByRole('button', { name: 'ยื่นคำขอที่นี่' }).click();
  await expect(page.getByRole('region', { name: 'รายละเอียดคำขอ' })).toContainText('ยื่นคำขอแล้ว');
  await page.getByRole('button', { name: 'ย้อนกลับ' }).click();
  // Back on the tab, the search is where it was, with the space now marked as applied
  await expect(match.getByLabel('ขายอะไร')).toHaveValue('ข้าวแกง');
  await expect(first).toContainText('ยื่นคำขอแล้ว');
  await expect(first.getByRole('button', { name: 'ยื่นคำขอที่นี่' })).toHaveCount(0);

  await page.getByRole('button', { name: 'ดูพื้นที่ทั้งหมด' }).click();
  await expect(all).toContainText('ตลาดนัดเขตบางพลัด');
  // The button pressed is gone with the matches; focus lands on the list it brought back
  await expect(page.getByRole('heading', { name: 'พื้นที่ค้าขายในเขตบางพลัด' })).toBeFocused();
  await expect(all.getByRole('listitem').filter({ hasText: 'ปากซอยจรัญฯ 79' })).toContainText('ยื่นคำขอแล้ว');
  await expect(match.getByRole('listitem')).toHaveCount(0);
});

test('a search with no match keeps the full list in view', async ({ page }) => {
  await page.setViewportSize(PHONE);
  await page.route('**/api/spaces/match', (route) => route.fulfill({ json: [] }));
  await page.goto('/vendor/spaces');
  const match = page.getByRole('region', { name: 'หาพื้นที่ที่เหมาะกับร้าน' });
  await match.getByLabel('ขายอะไร').fill('ของที่ไม่มีที่ไหนรับ');
  await match.getByRole('button', { name: 'แนะนำพื้นที่' }).click();
  await expect(match).toContainText('ไม่พบพื้นที่ที่รับสินค้านี้');
  await expect(page.getByRole('region', { name: 'พื้นที่ค้าขายในเขตบางพลัด' })).toContainText('ตลาดนัดเขตบางพลัด');
});

test('the assistant is a full-screen chat that keeps its history across tabs', async ({ page }) => {
  await page.setViewportSize(PHONE);
  await page.goto('/vendor/assistant');
  const chat = page.getByRole('region', { name: 'ถามผู้ช่วยเรื่องระเบียบ' });
  const input = chat.getByRole('textbox', { name: 'คำถาม' });
  const tabs = page.getByRole('navigation', { name: 'เมนูหลัก' });

  // The question box sits right above the tabs, clear of them
  const box = (await input.boundingBox())!;
  const nav = (await tabs.boundingBox())!;
  expect(box.y + box.height).toBeLessThanOrEqual(nav.y);
  expect(nav.y - (box.y + box.height)).toBeLessThan(40);

  // A suggestion does not wipe what the vendor had started typing
  await input.fill('ร่างคำถาม');
  await chat.getByRole('button', { name: 'ต้องใช้เอกสารอะไร' }).click();
  await expect(chat.getByRole('listitem').filter({ hasText: 'อ้างอิง:' })).toContainText('documents.md');
  await expect(input).toHaveValue('ร่างคำถาม');
  // Suggestions are for an empty chat only
  await expect(chat.getByRole('button', { name: 'จัดสรรล็อคยังไง' })).toHaveCount(0);

  for (const q of ['จัดสรรล็อคยังไง', 'ต้องยืนยันสิทธิ์ภายในกี่วัน']) {
    await input.fill(q);
    await chat.getByRole('button', { name: 'ถาม', exact: true }).click();
    await expect(chat.getByRole('listitem').filter({ hasText: 'อ้างอิง:' })).toHaveCount(
      q === 'จัดสรรล็อคยังไง' ? 2 : 3,
    );
  }
  const newest = chat.getByRole('listitem').filter({ hasText: 'อ้างอิง:' }).last();
  await expect(newest).toBeInViewport();

  await vendorTab(page, 'คำขอ');
  await vendorTab(page, 'ถามระเบียบ');
  await expect(chat.getByRole('listitem').filter({ hasText: 'ต้องใช้เอกสารอะไร' })).toHaveCount(1);
  // Back on the tab, the conversation opens at its newest answer
  await expect(newest).toBeInViewport();
});
