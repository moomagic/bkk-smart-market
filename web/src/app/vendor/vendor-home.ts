import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Api, SPACE_TYPE_LABEL, SpaceSummary } from '../core/api';

@Component({
  selector: 'app-vendor-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>พื้นที่ค้าขายในเขตบางพลัด</h1>
    <p class="lead">พื้นที่ที่ กทม. อนุญาตแล้ว ยื่นคำขอเพื่อเข้าจับฉลากในแต่ละรอบ</p>

    @switch (state()) {
      @case ('loading') {
        <p class="muted">กำลังโหลด…</p>
      }
      @case ('error') {
        <p class="error">โหลดรายการพื้นที่ไม่สำเร็จ ลองใหม่อีกครั้ง</p>
      }
      @default {
        <ul class="spaces">
          @for (space of spaces(); track space.id) {
            <li class="space">
              <div class="head">
                <h2>{{ space.name }}</h2>
                <span class="type">{{ typeLabel[space.type] }}</span>
              </div>
              <dl>
                <div><dt>ล็อค</dt><dd class="mono">{{ space.lockCount }}</dd></div>
                <div><dt>สินค้า</dt><dd>{{ space.productTypes.join(' · ') }}</dd></div>
                <div><dt>เวลา</dt><dd>{{ space.openHours }}</dd></div>
              </dl>
            </li>
          } @empty {
            <li class="muted">ยังไม่มีพื้นที่</li>
          }
        </ul>
      }
    }
  `,
  styles: `
    .lead { color: var(--muted); margin-top: 0; }
    .spaces { list-style: none; padding: 0; margin: 16px 0 0; display: grid; gap: 12px; }
    .space { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 14px 16px; }
    .head { display: flex; justify-content: space-between; align-items: baseline; gap: 8px; }
    h2 { font-size: 1.05rem; margin: 0; }
    .type { color: var(--accent); background: var(--accent-soft); border-radius: 4px; padding: 1px 8px; font-size: 0.75rem; white-space: nowrap; }
    dl { margin: 10px 0 0; display: grid; gap: 4px; }
    dl div { display: grid; grid-template-columns: 56px 1fr; gap: 8px; }
    dt { color: var(--muted); font-size: 0.85rem; }
    dd { margin: 0; }
    .muted { color: var(--muted); }
    .error { color: var(--warn); }
  `,
})
export class VendorHome {
  protected readonly typeLabel = SPACE_TYPE_LABEL;
  protected readonly spaces = signal<SpaceSummary[]>([]);
  protected readonly state = signal<'loading' | 'ready' | 'error'>('loading');

  constructor() {
    inject(Api)
      .spaces()
      .subscribe({
        next: (s) => {
          this.spaces.set(s);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }
}
