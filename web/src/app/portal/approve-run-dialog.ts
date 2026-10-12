import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { AllocationRun, Category, OutcomeKind } from '../core/api';
import { CATEGORY_LABEL } from '../shared/labels';

/**
 * The deliberate step before an allocation run is approved. Approval publishes the result to every applicant and
 * cannot be rejected afterwards (only a Draft can be), so the dialog restates what is being approved: the reference
 * and seed, winners against each group's quota, the waitlist, and who will see it.
 */
@Component({
  selector: 'app-approve-run-dialog',
  imports: [Button, Dialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-dialog [visible]="visible()" (visibleChange)="$event || cancel.emit()" (onHide)="closed.emit()" [modal]="true" [draggable]="false" closeAriaLabel="ปิด"
      [style]="{ width: 'min(32rem, calc(100vw - 32px))' }" header="อนุมัติผลจัดสรร">
      @let run = this.run();
      <p>อนุมัติผล <strong class="mono">{{ run.referenceNo }}</strong> · เลขตั้งต้นการสุ่ม <span class="mono">{{ run.seed }}</span></p>
      <table>
        <caption class="sr-only">ผู้ได้สิทธิ์เทียบโควตา</caption>
        <thead>
          <tr>
            <th scope="col">กลุ่ม</th>
            <th scope="col" class="num">ได้สิทธิ์ / โควตา</th>
            <th scope="col" class="num">สำรอง</th>
          </tr>
        </thead>
        <tbody>
          @for (q of run.quota; track q.category) {
            <tr>
              <td>{{ category[q.category] }}</td>
              <!-- Above or below quota only through rollover; marked so the officer sees it before approving -->
              <td class="num mono" [class.warn-text]="count(q.category, 'Won') !== q.base">{{ count(q.category, 'Won') }}/{{ q.base }}</td>
              <td class="num mono">{{ count(q.category, 'Waitlisted') }}</td>
            </tr>
          }
        </tbody>
      </table>
      <p>
        ได้สิทธิ์ {{ count(null, 'Won') }} คน · สำรอง {{ count(null, 'Waitlisted') }} คน ·
        ไม่ได้สิทธิ์ {{ count(null, 'NotSelected') }} คน
      </p>
      <p class="warn-text consequence">
        ผู้ค้าที่ผ่านการตรวจสิทธิ {{ run.outcomes.length }} คนจะเห็นผลนี้ทันที และผู้ได้สิทธิ์ต้องยืนยันสิทธิ์ภายในกำหนด ·
        อนุมัติแล้วปฏิเสธหรือจับฉลากใหม่ไม่ได้
      </p>
      <ng-template #footer>
        <p-button text label="กลับไปตรวจผล" (onClick)="cancel.emit()" [disabled]="deciding()" />
        <p-button [label]="deciding() ? 'กำลังบันทึก…' : 'ยืนยันอนุมัติผล'" (onClick)="confirm.emit()" [disabled]="deciding()" />
      </ng-template>
    </p-dialog>
  `,
  styles: `
    p { margin: 0 0 var(--space-3); }
    table { width: 100%; border-collapse: collapse; margin-bottom: var(--space-3); }
    /* Same density as the portal's tables (preset datatable: 8px × 12px) */
    th, td { text-align: left; padding: var(--space-2) var(--space-3); border-bottom: 1px solid var(--line); }
    th.num, td.num { text-align: right; }
    th { color: var(--muted); font-size: var(--text-small); font-weight: 600; }
    .consequence { margin-bottom: 0; }
  `,
})
export class ApproveRunDialog {
  readonly run = input.required<AllocationRun>();
  readonly visible = input(false);
  readonly deciding = input(false);
  readonly cancel = output<void>();
  readonly confirm = output<void>();
  /** Fired once the dialog has gone, however it closed: the page returns focus from here. */
  readonly closed = output<void>();

  protected readonly category = CATEGORY_LABEL;

  /** Outcomes of one kind, in one quota group or (category null) across the run. */
  protected count(category: Category | null, kind: OutcomeKind): number {
    return this.run().outcomes.filter((o) => o.kind === kind && (category === null || o.category === category)).length;
  }
}
