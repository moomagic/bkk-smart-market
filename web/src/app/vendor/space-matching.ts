import { ChangeDetectionStrategy, Component, Injector, afterNextRender, computed, inject, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { StatusTag } from '../shared/status-tag';
import { SpaceFacts } from './space-facts';
import { PreferredTime, SpaceSummary } from '../core/api';
import { VendorStore } from './vendor-store';

/** Space Matching: rule-based top 3 for the vendor's goods, area and hours — with the reason for every point. */
@Component({
  selector: 'app-space-matching',
  imports: [FormsModule, Button, InputText, Select, SpaceFacts, StatusTag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="match-heading">
      <!-- The app bar already says what this screen is for; the heading only names the region for assistive tech -->
      <h2 id="match-heading" class="sr-only">หาพื้นที่ที่เหมาะกับร้าน</h2>
      <!-- ngNoForm: FormsModule is here only for the select; the form keeps its plain submit -->
      <form ngNoForm (submit)="$event.preventDefault(); find()">
        <label>
          สินค้าที่ขาย
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
        <!-- Disabled until there is something to match on: the goods are the form's one required field, and first -->
        <p-button class="find" type="submit" fluid [label]="busy() ? 'กำลังหา…' : 'แนะนำพื้นที่'"
          [disabled]="busy() || product().trim().length === 0" />
      </form>

      @if (error(); as e) {
        <p class="error" [class.appear]="e !== errorOnArrival" role="status">{{ e }}</p>
      }
      @if (results(); as list) {
        @if (list.length === 0) {
          <p class="muted">ไม่พบพื้นที่ที่รับสินค้านี้ กรุณาติดต่อสำนักงานเขตบางพลัด</p>
        } @else {
          <ol class="results">
            @for (m of list; track m; let i = $index) {
              @let status = requestStatus()(m.spaceId);
              <!-- Matches the vendor just asked for come in one after another (tracked by object, so every search is new rows);
                   ones already found (back on the tab) just stand -->
              <li [class.arrive]="fresh()" [style.--i]="i">
                <!-- The same row as the full list: name and status, the space's facts, then why it matched -->
                <div class="head">
                  <h3>{{ i + 1 }}. {{ m.name }}</h3>
                  @if (status) {
                    <app-status-tag [request]="status" />
                  }
                </div>
                @if (spaceById().get(m.spaceId); as space) {
                  <app-space-facts [space]="space" [showCloses]="!closesAt()" />
                }
                <div class="why">
                  <p class="score">คะแนน <span class="mono">{{ m.score }}/100</span></p>
                  <ul class="reasons">
                    @for (r of m.reasons; track r) {
                      <li>{{ r }}</li>
                    }
                  </ul>
                </div>
                @if (!status && canSubmit()(m.spaceId); as space) {
                  <!-- The best match carries the page's one filled button; the others stay outlined -->
                  <p-button class="submit-here" [variant]="i === 0 ? undefined : 'outlined'" label="ยื่นคำขอที่นี่"
                    (onClick)="submit(space)" [disabled]="submitting()" />
                }
              </li>
            }
          </ol>
        }
        <!-- The matches stand in for the full list on a phone; this brings the list back -->
        <p-button class="show-all" link label="ดูพื้นที่ทั้งหมด" (onClick)="showAll()" />
      }
    </section>
  `,
  styles: `
    form { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); align-items: end; }
    label, .field { display: flex; flex-direction: column; gap: var(--space-1); font-size: var(--text-small); font-weight: 600; color: var(--muted); min-width: 0; }
    label:first-child { grid-column: 1 / -1; }
    .find { grid-column: 1 / -1; }
    .submit-here { display: block; margin-top: var(--space-3); }
    /* The matches read as the spaces list they stand in for: one bordered panel, rows divided */
    .results { list-style: none; margin: var(--space-4) 0 0; padding: 0; background: var(--surface); border: 1px solid var(--line); border-radius: 8px; }
    .results > li { padding: var(--space-4); }
    .results > li + li { border-top: 1px solid var(--line); }
    .head { display: flex; justify-content: space-between; align-items: baseline; gap: var(--space-2); }
    .head > app-status-tag { flex-shrink: 0; white-space: nowrap; }
    h3 { font-size: var(--text-h3); margin: 0; }
    /* Why it matched: under the facts, in the small size */
    .why { margin-top: var(--space-3); font-size: var(--text-small); }
    .score { margin: 0; color: var(--muted); }
    .score .mono { color: var(--accent); font-weight: 600; }
    .reasons { margin: var(--space-1) 0 0; padding-left: var(--space-4); }
    .reasons li + li { margin-top: 2px; }
    /* A link on its own line, still a finger-sized target */
    .show-all { display: block; margin-top: var(--space-2); }
    .show-all ::ng-deep .p-button { min-height: 44px; }
  `,
})
export class SpaceMatching {
  private readonly store = inject(VendorStore);
  /** Given a space id, the space the vendor can submit to now — or null (no open round, already requested, not a vendor). */
  protected readonly canSubmit = this.store.submittable;
  /** The vendor's request status in the space's open round, or null when they have none there. */
  protected readonly requestStatus = this.store.requestStatus;
  /** The listed space behind a match, for its facts (type, locks, hours, round). */
  protected readonly spaceById = computed(() => new Map(this.store.spaceList().map((s) => [s.id, s])));
  protected readonly closesAt = this.store.closesAt;
  /** A submit is in flight (from here or from the list below). */
  protected readonly submitting = computed(() => this.store.submitting() !== null);

  protected readonly product = this.store.match.product;
  protected readonly area = this.store.match.area;
  protected readonly time = this.store.match.time;
  protected readonly times: { label: string; value: PreferredTime | null }[] = [
    { label: 'ไม่ระบุ', value: null },
    { label: 'เช้า', value: 'Morning' },
    { label: 'เย็น', value: 'Evening' },
  ];
  protected readonly busy = this.store.match.busy;
  protected readonly results = this.store.match.results;
  protected readonly error = this.store.match.error;
  private readonly injector = inject(Injector);
  /** The matches already on screen when the tab opened: they are not news, so only a newer search animates in. */
  private readonly shownOnArrival = untracked(this.results);
  protected readonly fresh = computed(() => this.results() !== this.shownOnArrival);
  protected readonly errorOnArrival = untracked(this.error);

  protected submit(space: SpaceSummary): void {
    this.store.submit(space);
  }

  protected find(): void {
    this.store.findMatches();
  }

  /** Back to the full list, and focus to its heading: the button pressed is gone with the matches. */
  protected showAll(): void {
    this.store.clearMatches();
    afterNextRender(() => document.getElementById('spaces-heading')?.focus(), { injector: this.injector });
  }
}
