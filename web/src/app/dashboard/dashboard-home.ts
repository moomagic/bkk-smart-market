import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-dashboard-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>ภาพรวมผู้บริหาร</h1>
    <p class="muted">Occupancy · LeadTime · NewVendors · FairRotation</p>
  `,
  styles: `.muted { color: var(--muted); }`,
})
export class DashboardHome {}
