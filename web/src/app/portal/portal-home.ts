import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-portal-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>พอร์ทัลเจ้าหน้าที่เขต</h1>
    <p class="muted">รอบ · ตรวจสิทธิ · รันจัดสรร · อนุมัติ · คำขอทบทวน · audit trail</p>
  `,
  styles: `.muted { color: var(--muted); }`,
})
export class PortalHome {}
