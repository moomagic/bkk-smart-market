import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, signal, untracked, viewChild } from '@angular/core';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Textarea } from 'primeng/textarea';
import { AllocationRun } from '../core/api';

/**
 * Rejecting a draft run, as deliberately as approving one: the reason goes to every applicant, and a rejected run is
 * drawn again with a new seed. The dialog states both, asks for the reason, and says how many rejects the round has
 * left (past the limit only an executive may reject — ADR-0002; the server decides).
 */
@Component({
  selector: 'app-reject-run-dialog',
  imports: [Button, Dialog, Textarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-dialog [visible]="visible()" (visibleChange)="$event || cancel.emit()" (onShow)="opened()" (onHide)="closed.emit()"
      [modal]="true" [draggable]="false" closeAriaLabel="ปิด" styleClass="portal-dialog" [focusOnShow]="false"
      [style]="{ width: 'min(32rem, calc(100vw - 32px))' }" header="ปฏิเสธผลจัดสรร">
      @let run = this.run();
      <div class="body">
        <p>ปฏิเสธผล <strong class="mono">{{ run.referenceNo }}</strong> · จับฉลากใหม่ด้วยเลขตั้งต้นการสุ่มใหม่</p>
        <label for="reject-reason">เหตุผลในการปฏิเสธ (ผู้ค้าจะเห็นเหตุผลนี้)</label>
        <textarea #reason pTextarea id="reject-reason" rows="3" maxlength="500" [value]="text()"
          (input)="text.set($any($event.target).value)"></textarea>
        <p class="muted note" id="reject-note">
          @if (rejectsLeft() > 0) {
            ปฏิเสธได้อีก {{ rejectsLeft() }} ครั้งในรอบนี้ · เกินนั้นต้องให้ผู้บริหารเป็นผู้ปฏิเสธ
          } @else if (isExecutive()) {
            ครบจำนวนที่เจ้าหน้าที่ปฏิเสธได้แล้ว · ผู้บริหารปฏิเสธเพิ่มได้ และนับรวมในรอบนี้
          } @else {
            ครบจำนวนที่เจ้าหน้าที่ปฏิเสธได้แล้ว ต้องให้ผู้บริหารเป็นผู้ปฏิเสธ
          }
        </p>
      </div>
      <ng-template #footer>
        <p-button text label="กลับไปตรวจผล" (onClick)="cancel.emit()" [disabled]="deciding()" />
        <p-button severity="danger" variant="outlined" (onClick)="submit()" [pt]="{ root: { 'aria-describedby': 'reject-note' } }"
          [label]="deciding() ? 'กำลังบันทึก…' : 'ยืนยันการปฏิเสธ'" [disabled]="!canSubmit()" />
      </ng-template>
    </p-dialog>
  `,
  styles: `
    .body { display: flex; flex-direction: column; gap: var(--space-2); }
    p { margin: 0; }
    .note { font-size: var(--text-small); }
  `,
})
export class RejectRunDialog {
  readonly run = input.required<AllocationRun>();
  readonly visible = input(false);
  readonly deciding = input(false);
  readonly rejectsLeft = input.required<number>();
  readonly isExecutive = input(false);
  readonly cancel = output<void>();
  /** The reason, trimmed and not empty. */
  readonly confirm = output<string>();
  /** Fired once the dialog has gone, however it closed: the page returns focus from here. */
  readonly closed = output<void>();

  protected readonly text = signal('');
  private readonly reason = viewChild<ElementRef<HTMLTextAreaElement>>('reason');

  /** Mirrors the server's guards so the button only offers what it will accept; the server still decides. */
  protected canSubmit(): boolean {
    return !this.deciding() && this.text().trim().length > 0 && (this.rejectsLeft() > 0 || this.isExecutive());
  }

  constructor() {
    // Each opening starts from an empty reason, cleared as it opens (not once its animation ends, which would wipe
    // anything typed meanwhile)
    effect(() => {
      if (this.visible()) untracked(() => this.text.set(''));
    });
  }

  /** The cursor goes into the reason once the dialog is shown. */
  protected opened(): void {
    this.reason()?.nativeElement.focus();
  }

  protected submit(): void {
    if (this.canSubmit()) this.confirm.emit(this.text().trim());
  }
}
