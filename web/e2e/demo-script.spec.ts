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
  vendorTab,
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
    await expect(page).toHaveURL(/\/vendor\/requests$/);
    await expectMockBadge(page);

    await vendorTab(page, 'ถามระเบียบ');
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
    await vendorTab(page, 'หาพื้นที่');
    const match = page.getByRole('region', { name: 'หาพื้นที่ที่เหมาะกับร้าน' });
    await match.getByLabel('สินค้าที่ขาย').fill('ข้าวแกง');
    await match.getByLabel('ย่านที่สะดวก').fill('จรัญฯ');
    await match.getByRole('button', { name: 'แนะนำพื้นที่' }).click();

    const first = match.getByRole('listitem').filter({ hasText: '1. ปากซอยจรัญฯ 79' });
    await expect(first).toContainText('90/100');
    await first.getByRole('button', { name: 'ยื่นคำขอที่นี่' }).click();

    await expect(page.getByRole('status')).toContainText('ยื่นคำขอแล้ว · ปากซอยจรัญฯ 79');
    // The vendor lands on the request just filed: what it is now and what happens next
    await expect(page).toHaveURL(/\/vendor\/requests\/[0-9a-f-]+$/);
    const detail = requestDetail(page);
    await expect(detail).toContainText('ยื่นคำขอแล้ว');
    await expect(detail).toContainText('รอเขตตรวจสิทธิ');
    // The request's screen has ← in place of the tabs
    await page.getByRole('button', { name: 'ย้อนกลับ' }).click();
    await vendorTab(page, 'คำขอ');
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
    // Once drawn, the run tab opens with its decision bar; running or checking again is no longer on screen
    const run = page.getByRole('region', { name: new RegExp(`ผลจัดสรร · ${ref}`) });
    await expect(run.getByRole('group', { name: 'ตัดสินผลจัดสรร' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'รันจัดสรร' })).toHaveCount(0);
    await expect(run).toContainText('ร่าง รออนุมัติ');
    await expect(run.getByText('เลขตั้งต้นการสุ่ม (seed)')).toBeVisible();
    // The input hash sits in the folded evidence, one click away
    await run.getByText('หลักฐานการสุ่ม').click();
    await expect(run.getByText('ค่าแฮชข้อมูลนำเข้า (SHA-256)')).toBeVisible();
    // Replay verification: the draw again from the stored seed, rules and draw record gives the same result
    await run.getByRole('button', { name: 'ตรวจซ้ำด้วย seed เดิม' }).click();
    await expect(run.getByRole('status').filter({ hasText: 'ผลตรงกัน' })).toHaveText('ผลตรงกันทุกราย · ค่าแฮชข้อมูลตรงกัน');
    // Each group of the result carries its quota and seats won, in quota order
    const groups = run.getByRole('columnheader').filter({ hasText: 'โควตา' });
    await expect(groups).toHaveText([
      /ผู้ค้าเดิม · โควตา 2 · ได้สิทธิ์ 2/,
      /กลุ่มเปราะบาง · โควตา 1 · ได้สิทธิ์ 1/,
      /ผู้ค้าใหม่ · โควตา 1 · ได้สิทธิ์ 1/,
    ]);
    await expect(run.getByRole('row').filter({ hasText: 'สมศรี ใ.' })).toContainText('B2');

    // Approval publishes the result, so a dialog restates it before the officer commits
    await run.getByRole('button', { name: 'อนุมัติผล' }).click();
    const confirm = page.getByRole('dialog', { name: 'อนุมัติผลจัดสรร' });
    await expect(confirm).toContainText(ref);
    await expect(confirm).toContainText('ผู้ค้าที่ผ่านการตรวจสิทธิ 10 คนจะเห็นผลนี้ทันที');
    await confirm.getByRole('button', { name: 'ยืนยันอนุมัติผล' }).click();
    await expect(confirm).toBeHidden();
    await expect(run).toContainText('อนุมัติแล้ว');
  });

  await test.step('4 · vendor sees lock B2 with the reason, confirms and checks in', async () => {
    await page.setViewportSize(PHONE);
    await switchRole(page, 'vendor');
    await expect(page).toHaveURL(/\/vendor\/requests$/);
    // The requests tab says first what the vendor has to do, and the tab carries a dot while it waits
    await expect(page.getByText('สวัสดี สมศรี ใ.')).toBeVisible();
    await expect(page.getByTestId('todo')).toContainText('ยืนยันสิทธิ์ล็อค B2 ภายใน');
    await expect(requestsTab(page)).toHaveAccessibleDescription('มีคำขอที่รอยืนยันสิทธิ์');
    const card = currentRequest(page);
    await expect(card).toContainText('ได้ล็อค B2');
    await card.getByRole('link').click();

    const detail = requestDetail(page);
    await expect(detail).toContainText('ได้ล็อค B2');
    await expect(detail.getByRole('definition').filter({ hasText: ref })).toBeVisible();
    await expect(detail).toContainText('ยืนยันสิทธิ์ภายใน');

    await page.getByRole('button', { name: 'ยืนยันสิทธิ์', exact: true }).click();
    await expect(page.getByRole('status')).toContainText(
      'ยืนยันสิทธิ์แล้ว · ปากซอยจรัญฯ 79 ล็อค B2',
    );
    await expect(detail).toContainText('ยืนยันสิทธิ์แล้ว');

    await page.getByRole('button', { name: 'สแกน QR เช็คอินที่ล็อค B2' }).click();
    await expect(page.getByRole('status')).toContainText('เช็คอินแล้ว · ปากซอยจรัญฯ 79 ล็อค B2');
    await expect(detail).toContainText('เช็คอินล่าสุด');
    await expectNoHorizontalScroll(page);
  });

  await test.step('5 · the round audit trail shows every step, personal data masked', async () => {
    await page.setViewportSize(DESKTOP);
    await switchRole(page, 'officer');
    await openRound(page, 'ปากซอยจรัญฯ 79');
    await page.getByRole('button', { name: 'ประวัติการดำเนินการ' }).click();

    const audit = page.getByRole('region', { name: /ประวัติการดำเนินการ ·/ });
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
    await page.getByRole('button', { name: 'ปิดประวัติการดำเนินการ' }).click();
    await expect(audit).toHaveCount(0);
  });

  await test.step('6 · the executive dashboard shows occupancy up by one lock', async () => {
    await switchRole(page, 'executive');
    await expect(page).toHaveURL(/\/dashboard$/);
    await expectMockBadge(page);
    const occupancy = page.getByRole('article').filter({ hasText: 'การใช้พื้นที่' });
    await expect(occupancy).toContainText(
      `ผู้ได้สิทธิ์ยืนยันแล้ว ${before.count + 1} จาก ${before.of} ล็อค`,
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

/** The requests tab in the vendor app's bottom navigation. */
function requestsTab(page: Page) {
  return page.getByRole('navigation', { name: 'เมนูหลัก' }).getByRole('link', { name: 'คำขอ', exact: true });
}

/** The open request's own screen in the vendor app. */
function requestDetail(page: Page) {
  return page.getByRole('region', { name: 'รายละเอียดคำขอ' });
}

/** The vendor's status cell, matched exactly ("ผ่าน…" is a substring of "ไม่ผ่าน…"). */
function statusCell(table: Locator, vendorName: string, status: string) {
  return table
    .getByRole('row')
    .filter({ hasText: vendorName })
    .getByRole('cell', { name: status, exact: true });
}
