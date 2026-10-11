import { APIRequestContext, Page, expect } from '@playwright/test';

/** Seeded ids (Infrastructure/Seed) — fixed, never random. */
export const CHARAN_79_ROUND = '50000000-0000-0000-0000-000000000001';
export const SOMSRI = '40000000-0000-0000-0000-000000000001';
export const CHAI = '40000000-0000-0000-0000-000000000011';

const OFFICER = { 'X-Demo-Role': 'officer', 'X-Demo-User': 'officer-bangphlat' };
const EXECUTIVE = { 'X-Demo-Role': 'executive', 'X-Demo-User': 'executive-bkk' };
const vendor = (id: string) => ({ 'X-Demo-Role': 'vendor', 'X-Demo-User': id });

/** Vendor screens are judged on a phone; portal and dashboard on a desktop. */
export const PHONE = { width: 390, height: 844 };
export const DESKTOP = { width: 1280, height: 800 };

/** Calls the API directly to put the demo in a known state; the UI is what the tests check. */
export class DemoApi {
  constructor(private readonly request: APIRequestContext) {}

  /** Restores seed data and Demo:Now (Development only). */
  async reset(): Promise<void> {
    await this.post('/api/demo/reset', OFFICER);
  }

  async checkEligibility(roundId = CHARAN_79_ROUND): Promise<void> {
    await this.post(`/api/rounds/${roundId}/eligibility`, OFFICER);
  }

  async runAllocation(roundId = CHARAN_79_ROUND): Promise<string> {
    return (await this.post<{ id: string }>(`/api/rounds/${roundId}/allocation-runs`, OFFICER)).id;
  }

  async reject(runId: string, reason: string): Promise<void> {
    await this.post(`/api/allocation-runs/${runId}/reject`, OFFICER, { reason });
  }

  async approve(runId: string): Promise<void> {
    await this.post(`/api/allocation-runs/${runId}/approve`, OFFICER);
  }

  async submit(vendorId: string, roundId = CHARAN_79_ROUND): Promise<void> {
    await this.post('/api/stall-requests', vendor(vendorId), { roundId });
  }

  async fileReview(vendorId: string, reason: string, roundId = CHARAN_79_ROUND): Promise<void> {
    const res = await this.request.get('/api/stall-requests/me', { headers: vendor(vendorId) });
    expect(res.ok()).toBeTruthy();
    const request = ((await res.json()) as { id: string; roundId: string }[]).find(
      (r) => r.roundId === roundId,
    );
    expect(request, `no request of ${vendorId} in round ${roundId}`).toBeDefined();
    await this.post(`/api/stall-requests/${request!.id}/reviews`, vendor(vendorId), { reason });
  }

  /** Confirmed locks ÷ all locks, as the dashboard counts them. */
  async occupancy(): Promise<{ count: number; of: number }> {
    const res = await this.request.get('/api/dashboard/kpis', { headers: EXECUTIVE });
    expect(res.ok()).toBeTruthy();
    return ((await res.json()) as { occupancy: { count: number; of: number } }).occupancy;
  }

  private async post<T = unknown>(
    url: string,
    headers: Record<string, string>,
    data?: unknown,
  ): Promise<T> {
    const res = await this.request.post(url, { headers, data });
    expect(res.ok(), `${url} → ${res.status()} ${await res.text()}`).toBeTruthy();
    return (await res.json()) as T;
  }
}

/** Switches role with the top-right switcher, which also navigates to that role's home. */
export async function switchRole(
  page: Page,
  role: 'vendor' | 'officer' | 'executive',
): Promise<void> {
  await page.getByRole('combobox', { name: 'บทบาท' }).click();
  await page.getByRole('option', { name: ROLE_LABEL[role], exact: true }).click();
  await expect(page.locator('app-role-switcher')).toHaveAttribute('data-confirmed-role', role);
}

/** What the switcher shows for each role. */
export const ROLE_LABEL = {
  vendor: 'ผู้ค้า',
  officer: 'เจ้าหน้าที่เขต',
  executive: 'ผู้บริหาร',
} as const;

/** Guardrail 5: every screen says the data is mock. */
export async function expectMockBadge(page: Page): Promise<void> {
  await expect(page.getByRole('note').filter({ hasText: 'ข้อมูลจำลอง' })).toBeVisible();
}

/** Guardrail 4: a submitted request is never presented as a booking ("จอง…" in any form). */
export async function expectNoBookingWording(page: Page): Promise<void> {
  await expect(page.locator('body')).not.toContainText('จอง');
}

export async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow, 'page scrolls horizontally').toBeLessThanOrEqual(0);
}

/** Opens a round in the portal (selection is not kept across navigation). Default: the current round. */
export async function openRound(
  page: Page,
  spaceName: string,
  startsOn = '1 พ.ย. 2569',
): Promise<void> {
  const rounds = page.getByRole('region', { name: 'รอบ', exact: true });
  const item = rounds.getByRole('listitem').filter({ hasText: spaceName }).filter({ hasText: startsOn });
  // Approved rounds are folded under their own heading
  if (!(await item.isVisible())) await rounds.getByText(/รอบที่อนุมัติผลแล้ว/).click();
  await item.getByRole('button').click();
  await expect(page.getByRole('heading', { level: 2, name: new RegExp(spaceName) })).toBeVisible();
}
