import { Locator, Page, expect, test } from '@playwright/test';
import {
  DESKTOP,
  DemoApi,
  PHONE,
  expectMockBadge,
  expectNoBookingWording,
  expectNoHorizontalScroll,
  openRound,
  switchRole,
} from './support';

/** README › Demo script, steps 1–6, walked the way the presenter does it. Step 7 is `docker compose up` itself. */
test('demo script 1–6', async ({ page, request }) => {
  const api = new DemoApi(request);
  await api.reset();
  const before = await api.occupancy();
  const ref = 'BPL-2569-1025-01';

  await test.step('1 · vendor asks the assistant which documents are needed', async () => {
    await page.setViewportSize(PHONE);
    await page.goto('/vendor');
    await expectMockBadge(page);

    const chat = page.getByRole('region', { name: 'ถามผู้ช่วยเรื่องระเบียบ' });
    await chat.getByRole('button', { name: 'ต้องใช้เอกสารอะไร' }).click();
    const answer = chat.getByRole('listitem').filter({ hasText: 'อ้างอิง:' });
    await expect(answer).toContainText('บัตรประจำตัวประชาชน');
    await expect(answer).toContainText('อ้างอิง: documents.md › เอกสารที่ต้องใช้ในการยื่นคำขอ');
    // Keyword fallback: the suite runs without ANTHROPIC_API_KEY so answers are deterministic.
    await expect(answer).toContainText('ค้นจากหัวข้อระเบียบ');
    await expectNoHorizontalScroll(page);
  });

  await test.step('2 · space matching suggests ปากซอยจรัญฯ 79 and the vendor submits', async () => {
    const match = page.getByRole('region', { name: 'หาพื้นที่ที่เหมาะกับร้าน' });
    await match.getByLabel('ขายอะไร').fill('ข้าวแกง');
    await match.getByLabel('ย่านที่สะดวก').fill('จรัญฯ');
    await match.getByRole('button', { name: 'แนะนำพื้นที่' }).click();

    const first = match.getByRole('listitem').filter({ hasText: '1. ปากซอยจรัญฯ 79' });
    await expect(first).toContainText('90/100');
    await first.getByRole('button', { name: 'ยื่นคำขอที่นี่' }).click();

    await expect(page.getByRole('status')).toContainText('ยื่นคำขอแล้ว · ปากซอยจรัญฯ 79');
    await expect(first).toContainText('ยื่นคำขอแล้ว');
    await expect(currentRequest(page)).toContainText('ยื่นคำขอแล้ว');
    await expectNoBookingWording(page);
    await expectNoHorizontalScroll(page);
  });

  await test.step('3 · officer checks eligibility, runs the allocation (2/1/1) and approves', async () => {
    await page.setViewportSize(DESKTOP);
    await switchRole(page, 'officer');
    await expect(page).toHaveURL(/\/portal$/);
    await expectMockBadge(page);
    await openRound(page, 'ปากซอยจรัญฯ 79');

    await page.getByRole('button', { name: 'ตรวจสิทธิทั้งรอบ' }).click();
    const requests = page.getByRole('region', { name: /คำขอ · ปากซอยจรัญฯ 79/ });
    await expect(statusCell(requests, 'ชัย ด.', 'ไม่ผ่านการตรวจสิทธิ')).toBeVisible();
    await expect(requests.getByRole('row').filter({ hasText: 'ปิยะ น.' })).toContainText('×2');
    await expect(statusCell(requests, 'สมศรี ใ.', 'ผ่านการตรวจสิทธิ')).toBeVisible();

    await page.getByRole('button', { name: 'รันจัดสรร' }).click();
    const run = page.getByRole('region', { name: new RegExp(`ผลจัดสรร · ${ref}`) });
    await expect(run).toContainText('ร่าง รออนุมัติ');
    await expect(run.getByText('Seed (ระบบสุ่ม)')).toBeVisible();
    // The input hash sits in the folded evidence, one click away
    await run.getByText('หลักฐานการสุ่ม').click();
    await expect(run.getByText('Input hash (SHA-256)')).toBeVisible();
    for (const [group, quota] of [
      ['ผู้ค้าเดิม', '2'],
      ['กลุ่มเปราะบาง', '1'],
      ['ผู้ค้าใหม่', '1'],
    ]) {
      await expect(
        run.getByRole('row').filter({ hasText: group }).first().getByRole('cell').nth(1),
      ).toHaveText(quota);
    }
    await expect(run.getByRole('row').filter({ hasText: 'สมศรี ใ.' })).toContainText('B2');

    await run.getByRole('button', { name: 'อนุมัติผล' }).click();
    await expect(run).toContainText('อนุมัติแล้ว');
  });

  await test.step('4 · vendor sees lock B2 with the reason, confirms and checks in', async () => {
    await page.setViewportSize(PHONE);
    await switchRole(page, 'vendor');
    await expect(page).toHaveURL(/\/vendor$/);
    const card = currentRequest(page);
    await expect(card).toContainText('ได้ล็อค B2');
    await expect(card).toContainText(`เลขอ้างอิง ${ref}`);
    await expect(card).toContainText('ยืนยันสิทธิ์ภายใน');

    await card.getByRole('button', { name: 'ยืนยันสิทธิ์', exact: true }).click();
    await expect(page.getByRole('status')).toContainText(
      'ยืนยันสิทธิ์แล้ว · ปากซอยจรัญฯ 79 ล็อค B2',
    );
    await expect(card).toContainText('ยืนยันสิทธิ์แล้ว');

    await card.getByRole('button', { name: 'สแกน QR เช็คอินที่ล็อค B2' }).click();
    await expect(page.getByRole('status')).toContainText('เช็คอินแล้ว · ปากซอยจรัญฯ 79 ล็อค B2');
    await expect(card).toContainText('เช็คอินล่าสุด');
    await expectNoHorizontalScroll(page);
  });

  await test.step('5 · the round audit trail shows every step, personal data masked', async () => {
    await page.setViewportSize(DESKTOP);
    await switchRole(page, 'officer');
    await openRound(page, 'ปากซอยจรัญฯ 79');
    await page.getByRole('button', { name: 'Audit trail ของรอบ' }).click();

    const audit = page.getByRole('region', { name: /Audit trail ·/ });
    for (const action of [
      'ยื่นคำขอ',
      'ตรวจสิทธิทั้งรอบ',
      'รันจัดสรร (ร่าง)',
      'อนุมัติผลจัดสรร',
      'ยืนยันสิทธิ์',
      'เช็คอิน',
    ]) {
      await expect(audit.getByText(action, { exact: true }).first()).toBeVisible();
    }
    await expect(audit).toContainText('***');
    await page.getByRole('button', { name: 'ปิด audit trail' }).click();
    await expect(audit).toHaveCount(0);
  });

  await test.step('6 · the executive dashboard shows occupancy up by one lock', async () => {
    await switchRole(page, 'executive');
    await expect(page).toHaveURL(/\/dashboard$/);
    await expectMockBadge(page);
    const occupancy = page.getByRole('article').filter({ hasText: 'การใช้พื้นที่ (Occupancy)' });
    await expect(occupancy).toContainText(
      `ล็อคที่ผู้ได้สิทธิ์ยืนยันแล้ว ${before.count + 1} จาก ${before.of} ล็อค`,
    );
    await expect(page.getByRole('region', { name: 'ผู้ได้สิทธิ์จริงเทียบโควตา' })).toContainText(
      ref,
    );
  });
});

/** สมศรี ใ.'s request in the current round at ปากซอยจรัญฯ 79 (her previous-round request is listed too). */
function currentRequest(page: Page) {
  return page
    .getByRole('region', { name: 'คำขอของฉัน' })
    .getByRole('listitem')
    .filter({ hasText: 'ปากซอยจรัญฯ 79' })
    .filter({ hasText: 'รอบ 1 พ.ย. 2569' });
}

/** The vendor's status cell, matched exactly ("ผ่าน…" is a substring of "ไม่ผ่าน…"). */
function statusCell(table: Locator, vendorName: string, status: string) {
  return table
    .getByRole('row')
    .filter({ hasText: vendorName })
    .getByRole('cell', { name: status, exact: true });
}
