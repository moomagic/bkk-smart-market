import { Injectable, computed, signal } from '@angular/core';

export type DemoRole = 'vendor' | 'officer' | 'executive';

interface RoleInfo {
  label: string;
  user: string;
  home: string;
}

/** Mock identities. The vendor is สมศรี ใ. (fixed VendorId from seed data). */
export const DEMO_ROLES: Record<DemoRole, RoleInfo> = {
  vendor: { label: 'ผู้ค้า', user: '40000000-0000-0000-0000-000000000001', home: '/vendor' },
  officer: { label: 'เจ้าหน้าที่เขต', user: 'officer-bangphlat', home: '/portal' },
  executive: { label: 'ผู้บริหาร', user: 'executive-bkk', home: '/dashboard' },
};

const STORAGE_KEY = 'bkk-smart-market.role';

/** Which demo role the browser is acting as. Sent to the API as X-Demo-Role / X-Demo-User. */
@Injectable({ providedIn: 'root' })
export class DemoSession {
  readonly role = signal<DemoRole>(readStoredRole());
  readonly user = computed(() => DEMO_ROLES[this.role()].user);
  readonly info = computed(() => DEMO_ROLES[this.role()]);

  switchTo(role: DemoRole): void {
    this.role.set(role);
    try {
      localStorage.setItem(STORAGE_KEY, role);
    } catch {
      // storage unavailable (private mode) — role just won't persist
    }
  }
}

function readStoredRole(): DemoRole {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored && stored in DEMO_ROLES) return stored as DemoRole;
  } catch {
    // ignore
  }
  return 'vendor';
}
