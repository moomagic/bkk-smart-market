import { expect, test } from '@playwright/test';
import {
  CHARAN_79_ROUND,
  DESKTOP,
  DemoApi,
  PHONE,
  SOMSRI,
  expectMockBadge,
  expectNoBookingWording,
  openRound,
} from './support';

test.beforeEach(async ({ request }) => {
  await new DemoApi(request).reset();
});

/** Guardrails 4 and 5 on every area, for the role and device that uses it. */
for (const { path, role, device, loaded } of [
  {
    path: '/vendor',
    role: 'vendor',
    device: { viewport: PHONE, isMobile: true, hasTouch: true },
    loaded: 'ปากซอยจรัญฯ 79',
  },
  { path: '/portal', role: 'officer', device: { viewport: DESKTOP }, loaded: 'ปากซอยจรัญฯ 79' },
  {
    path: '/dashboard',
    role: 'executive',
    device: { viewport: DESKTOP },
    loaded: 'การใช้พื้นที่ (Occupancy)',
  },
] as const) {
  test.describe(path, () => {
    test.use(device);

    test(`shows the mock-data badge and never says "booked"`, async ({ page, request }) => {
      // A submitted request is on screen for the vendor, so the status wording is actually checked.
      if (role === 'vendor') await new DemoApi(request).submit(SOMSRI, CHARAN_79_ROUND);
      await page.addInitScript((r) => localStorage.setItem('bkk-smart-market.role', r), role);
      await page.goto(path);
      // Wait for API data, not just the static heading — a negative text check passes on a loading screen.
      await expect(page.getByText(loaded).first()).toBeVisible();
      if (role === 'vendor') await expect(page.getByText('ยื่นคำขอแล้ว').first()).toBeVisible();
      await expectMockBadge(page);
      await expectNoBookingWording(page);
    });
  });
}

test('vendor names appear only in short form (สมศรี ใ.), never with the full surname', async ({
  page,
  request,
}) => {
  await new DemoApi(request).submit(SOMSRI, CHARAN_79_ROUND);
  await page.setViewportSize(DESKTOP);
  await page.addInitScript(() => localStorage.setItem('bkk-smart-market.role', 'officer'));
  await page.goto('/portal');
  await openRound(page, 'ปากซอยจรัญฯ 79');

  const requests = page.getByRole('region', { name: /คำขอ · ปากซอยจรัญฯ 79/ });
  await expect(requests).toContainText('สมศรี ใ.');
  await expect(requests).toContainText('ชัย ด.');
  // Full names from seed data must not leak to the screen.
  await expect(page.locator('body')).not.toContainText('สมศรี ใจดี');
  await expect(page.locator('body')).not.toContainText('ชัย ดวงดี');
});
