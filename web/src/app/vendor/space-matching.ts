import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { Api, PreferredTime, SpaceMatch, SpaceSummary, problemMessage } from '../core/api';

/** Space Matching: rule-based top 3 for the vendor's goods, area and hours — with the reason for every point. */
@Component({
  selector: 'app-space-matching',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="match" aria-labelledby="match-heading">
      <h1 id="match-heading">หาพื้นที่ที่เหมาะกับร้าน</h1>
      <form (submit)="$event.preventDefault(); find()">
        <label>
          ขายอะไร
          <input type="text" maxlength="100" placeholder="เช่น ข้าวแกง" [value]="product()"
            (input)="product.set($any($event.target).value)" />
        </label>
        <label>
          ย่านที่สะดวก
          <input type="text" maxlength="100" placeholder="เช่น จรัญฯ" [value]="area()"
            (input)="area.set($any($event.target).value)" />
        </label>
        <label>
          ช่วงเวลา
          <select (change)="time.set($any($event.target).value || null)">
            <option value="">ไม่ระบุ</option>
            <option value="Morning">เช้า</option>
            <option value="Evening">เย็น</option>
          </select>
        </label>
        <button type="submit" [disabled]="busy() || product().trim().length === 0">
          {{ busy() ? 'กำลังหา…' : 'แนะนำพื้นที่' }}
        </button>
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
                  <h2>{{ i + 1 }}. {{ m.name }}</h2>
                  <span class="score mono">{{ m.score }}/100</span>
                </div>
                <ul class="reasons">
                  @for (r of m.reasons; track r) {
                    <li>{{ r }}</li>
                  }
                </ul>
                @if (requested()(m.spaceId)) {
                  <p class="requested">ยื่นคำขอแล้ว</p>
                } @else if (canSubmit()(m.spaceId); as space) {
                  <button type="button" class="secondary" (click)="submit.emit(space)" [disabled]="submitting()">ยื่นคำขอที่นี่</button>
                }
              </li>
            }
          </ol>
        }
      }
    </section>
  `,
  styles: `
    .match { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 14px; }
    h1 { font-size: 1.1rem; margin: 0 0 10px; }
    form { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 10px; align-items: end; }
    label { display: flex; flex-direction: column; gap: 4px; font-size: 0.85rem; color: var(--muted); }
    label:first-child { grid-column: 1 / -1; }
    input, select { font: inherit; color: var(--ink); padding: 8px 10px; border: 1px solid var(--line); border-radius: 6px; background: var(--surface); min-width: 0; }
    form button { grid-column: 1 / -1; }
    button { font: inherit; font-weight: 600; padding: 8px 14px; color: var(--surface); background: var(--accent); border: 0; border-radius: 6px; cursor: pointer; }
    button.secondary { color: var(--accent); background: var(--surface); border: 1px solid var(--accent); margin-top: 6px; }
    button:disabled { opacity: 0.6; cursor: default; }
    .results { list-style: none; margin: 12px 0 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    .results > li { border-top: 1px solid var(--line); padding-top: 10px; }
    .head { display: flex; justify-content: space-between; align-items: baseline; gap: 8px; }
    h2 { font-size: 1rem; margin: 0; }
    .score { color: var(--accent); font-weight: 600; }
    .reasons { margin: 6px 0 0; padding-left: 18px; font-size: 0.9rem; }
    .requested { margin: 6px 0 0; font-weight: 600; color: var(--accent); }
    .muted { color: var(--muted); }
    .error { color: var(--warn); }
  `,
})
export class SpaceMatching {
  private readonly api = inject(Api);

  /** Given a space id, the space the vendor can submit to now — or null (no open round, already requested, not a vendor). */
  readonly canSubmit = input.required<(spaceId: string) => SpaceSummary | null>();
  /** True when the vendor already has a request in the space's open round. */
  readonly requested = input.required<(spaceId: string) => boolean>();
  /** A submit is in flight (from here or from the list below). */
  readonly submitting = input(false);
  readonly submit = output<SpaceSummary>();

  protected readonly product = signal('');
  protected readonly area = signal('');
  protected readonly time = signal<PreferredTime | null>(null);
  protected readonly busy = signal(false);
  protected readonly results = signal<SpaceMatch[] | null>(null);
  protected readonly error = signal<string | null>(null);

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
