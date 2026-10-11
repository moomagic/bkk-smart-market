import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { StatusTag } from '../shared/status-tag';
import { Api, PreferredTime, SpaceMatch, SpaceSummary, problemMessage } from '../core/api';
import { VendorStore } from './vendor-store';

/** Space Matching: rule-based top 3 for the vendor's goods, area and hours — with the reason for every point. */
@Component({
  selector: 'app-space-matching',
  imports: [FormsModule, Button, InputText, Select, StatusTag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="match-heading">
      <h2 id="match-heading">หาพื้นที่ที่เหมาะกับร้าน</h2>
      <!-- ngNoForm: FormsModule is here only for the select; the form keeps its plain submit -->
      <form ngNoForm (submit)="$event.preventDefault(); find()">
        <label>
          ขายอะไร
          <input pInputText type="text" maxlength="100" placeholder="เช่น ข้าวแกง" [value]="product()"
            (input)="product.set($any($event.target).value)" />
        </label>
        <label>
          ย่านที่สะดวก
          <input pInputText type="text" maxlength="100" placeholder="เช่น จรัญฯ" [value]="area()"
            (input)="area.set($any($event.target).value)" />
        </label>
        <div class="field">
          <span id="match-time-label">ช่วงเวลา</span>
          <!-- "ไม่ระบุ" is no preference (null), as before; the panel goes on body so it stays inside a 390px screen -->
          <p-select ariaLabelledBy="match-time-label" appendTo="body" fluid placeholder="ไม่ระบุ" [options]="times"
            optionLabel="label" optionValue="value" [ngModel]="time()" (ngModelChange)="time.set($event)" />
        </div>
        <!-- Disabled until there is something to match on; the hint says so instead of leaving a dead button -->
        <p-button class="find" type="submit" fluid [label]="busy() ? 'กำลังหา…' : 'แนะนำพื้นที่'"
          [disabled]="busy() || product().trim().length === 0" />
        @if (product().trim().length === 0) {
          <p class="find-hint">กรอกสิ่งที่ขายก่อน แล้วระบบจะแนะนำพื้นที่ 3 อันดับพร้อมเหตุผล</p>
        }
      </form>

      @if (error(); as e) {
        <p class="error" role="status">{{ e }}</p>
      }
      @if (results(); as list) {
        @if (list.length === 0) {
          <p class="muted">ไม่พบพื้นที่ที่รับสินค้านี้ กรุณาติดต่อสำนักงานเขตบางพลัด</p>
        } @else {
          <ol class="results">
            @for (m of list; track m.spaceId; let i = $index) {
              <li>
                <div class="head">
                  <h3>{{ i + 1 }}. {{ m.name }}</h3>
                  <span class="score mono">{{ m.score }}/100</span>
                </div>
                <ul class="reasons">
                  @for (r of m.reasons; track r) {
                    <li>{{ r }}</li>
                  }
                </ul>
                @if (requestStatus()(m.spaceId); as status) {
                  <p class="requested"><app-status-tag [request]="status" /></p>
                } @else if (canSubmit()(m.spaceId); as space) {
                  <!-- The best match carries the page's one filled button; the others stay outlined -->
                  <p-button class="submit-here" [variant]="i === 0 ? undefined : 'outlined'" label="ยื่นคำขอที่นี่"
                    (onClick)="submit(space)" [disabled]="submitting()" />
                }
              </li>
            }
          </ol>
        }
      }
    </section>
  `,
  styles: `
    h2 { font-size: var(--text-h3); margin: 0 0 var(--space-3); }
    .find-hint { grid-column: 1 / -1; margin: 0; font-size: var(--text-small); color: var(--muted); }
    form { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-2) var(--space-3); align-items: end; }
    label, .field { display: flex; flex-direction: column; gap: var(--space-1); font-size: var(--text-small); color: var(--muted); min-width: 0; }
    label:first-child { grid-column: 1 / -1; }
    .find { grid-column: 1 / -1; }
    .submit-here { display: inline-block; margin-top: var(--space-2); }
    .results { list-style: none; margin: var(--space-3) 0 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-3); }
    .results > li { border-top: 1px solid var(--line); padding-top: var(--space-3); }
    .head { display: flex; justify-content: space-between; align-items: baseline; gap: var(--space-2); }
    h3 { font-size: var(--text-body); margin: 0; }
    .score { color: var(--accent); font-weight: 600; }
    .reasons { margin: var(--space-2) 0 0; padding-left: var(--space-4); font-size: var(--text-small); }
    .requested { margin: var(--space-2) 0 0; }
  `,
})
export class SpaceMatching {
  private readonly api = inject(Api);

  private readonly store = inject(VendorStore);
  private readonly router = inject(Router);

  /** Given a space id, the space the vendor can submit to now — or null (no open round, already requested, not a vendor). */
  protected readonly canSubmit = this.store.submittable;
  /** The vendor's request status in the space's open round, or null when they have none there. */
  protected readonly requestStatus = this.store.requestStatus;
  /** A submit is in flight (from here or from the list below). */
  protected readonly submitting = computed(() => this.store.submitting() !== null);

  protected readonly product = signal('');
  protected readonly area = signal('');
  protected readonly time = signal<PreferredTime | null>(null);
  protected readonly times: { label: string; value: PreferredTime | null }[] = [
    { label: 'ไม่ระบุ', value: null },
    { label: 'เช้า', value: 'Morning' },
    { label: 'เย็น', value: 'Evening' },
  ];
  protected readonly busy = signal(false);
  protected readonly results = signal<SpaceMatch[] | null>(null);
  protected readonly error = signal<string | null>(null);

  /** A filed request opens on its own screen, as from the list of spaces. */
  protected async submit(space: SpaceSummary): Promise<void> {
    const req = await this.store.submit(space);
    if (req) await this.router.navigate(['/vendor/requests', req.id]);
  }

  protected find(): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.matchSpaces(this.product().trim(), this.area().trim(), this.time()).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.results.set(r);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemMessage(err));
      },
    });
  }
}
