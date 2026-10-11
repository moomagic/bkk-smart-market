import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { Api, AuditChange, AuditEntry } from '../core/api';
import { AUDIT_ACTION_LABEL, ROLE_LABEL, formatThaiDateTime } from '../shared/labels';

type Load = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: AuditEntry[] };

/** Longer values (e.g. a run's result JSON) are left off the timeline; the stored entry keeps them in full. */
const MAX_SHOWN_VALUE_LENGTH = 60;
const MAX_SHOWN_FIELDS = 6;
const STATE_LABEL: Record<AuditChange['State'], string> = { Added: 'สร้าง', Modified: 'แก้ไข', Deleted: 'ลบ' };

/** Who did what, when, and what changed — for a round or a single entity. Personal data arrives masked ("***"). */
@Component({
  selector: 'app-audit-timeline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (entries().state) {
      @case ('loading') {
        <p class="muted">กำลังโหลด…</p>
      }
      @case ('error') {
        <p class="error">โหลด audit trail ไม่สำเร็จ</p>
      }
      @case ('ready') {
        <ol class="timeline">
          @for (e of list(); track e.id) {
            <li>
              <div class="when mono">{{ dateTime(e.at) }}</div>
              <div class="what">
                <strong>{{ actionLabel(e.action) }}</strong>
                <span class="muted"> · {{ e.actorName }} ({{ role(e.actorRole) }})</span>
                <div class="action mono">{{ e.action }}</div>
                <ul class="changes">
                  @for (c of e.changes; track c.Entity + c.Id) {
                    <li>
                      <button type="button" class="entity mono" (click)="openEntity.emit({ type: c.Entity, id: c.Id })"
                        [attr.aria-label]="'ดูประวัติ ' + c.Entity + ' ' + c.Id">
                        {{ c.Entity }} …{{ c.Id.slice(-8) }}
                      </button>
                      · {{ stateLabel[c.State] }}
                      @if (fields(c); as f) {
                        @if (f.length > 0) {
                          <span class="muted"> — {{ f.join(' · ') }}</span>
                        }
                      }
                    </li>
                  }
                </ul>
              </div>
            </li>
          } @empty {
            <li class="muted">ยังไม่มีบันทึก</li>
          }
        </ol>
      }
    }
  `,
  styles: `
    .timeline { list-style: none; margin: 0; padding: 0; background: var(--surface); border: 1px solid var(--line); border-radius: 8px; }
    .timeline > li { display: grid; grid-template-columns: 150px 1fr; gap: 12px; padding: 10px 14px; border-bottom: 1px solid var(--line); }
    .timeline > li:last-child { border-bottom: 0; }
    .when { font-size: 0.82rem; color: var(--muted); }
    .action { font-size: 0.75rem; color: var(--muted); }
    .changes { margin: 4px 0 0; padding-left: 16px; font-size: 0.85rem; overflow-wrap: anywhere; }
    .entity { font: inherit; padding: 0; border: 0; background: none; color: var(--accent); cursor: pointer; }
    @media (max-width: 640px) { .timeline > li { grid-template-columns: 1fr; gap: 2px; } }
  `,
})
export class AuditTimeline {
  private readonly api = inject(Api);

  readonly entityType = input.required<string>();
  readonly entityId = input.required<string>();
  /** Bump to reload after a command. */
  readonly version = input(0);
  /** The officer wants the trail of one entity from a change line. */
  readonly openEntity = output<{ type: string; id: string }>();

  protected readonly dateTime = formatThaiDateTime;
  protected readonly stateLabel = STATE_LABEL;

  private readonly target = computed(() => ({ type: this.entityType(), id: this.entityId(), version: this.version() }));

  protected readonly entries = toSignal(
    toObservable(this.target).pipe(
      switchMap(({ type, id }) =>
        this.api.auditTrail(type, id).pipe(
          map((data) => ({ state: 'ready', data }) as Load),
          catchError(() => of({ state: 'error' } as Load)),
          startWith({ state: 'loading' } as Load),
        ),
      ),
    ),
    { initialValue: { state: 'loading' } as Load },
  );

  protected list(): AuditEntry[] {
    const e = this.entries();
    return e.state === 'ready' ? e.data : [];
  }

  protected actionLabel(action: string): string {
    return AUDIT_ACTION_LABEL[action] ?? action;
  }

  protected role(role: string): string {
    return ROLE_LABEL[role] ?? role;
  }

  /** "Status: Eligible → Won" for edits; "Status: Submitted" for creations; long values (JSON results) left out. */
  protected fields(c: AuditChange): string[] {
    const keys = Object.keys(c.After ?? c.Before ?? {});
    const shown = keys
      .map((k) => {
        const before = c.Before?.[k];
        const after = c.After?.[k];
        if ([before, after].some((v) => String(v ?? '').length > MAX_SHOWN_VALUE_LENGTH)) return null;
        return c.State === 'Modified' ? `${k}: ${show(before)} → ${show(after)}` : `${k}: ${show(after ?? before)}`;
      })
      .filter((s): s is string => s !== null);
    return shown.length > MAX_SHOWN_FIELDS
      ? [...shown.slice(0, MAX_SHOWN_FIELDS), `+${shown.length - MAX_SHOWN_FIELDS} รายการ`]
      : shown;
  }
}

function show(v: unknown): string {
  return v === null || v === undefined ? '–' : String(v);
}
