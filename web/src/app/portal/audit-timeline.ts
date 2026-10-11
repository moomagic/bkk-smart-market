import { ChangeDetectionStrategy, Component, ViewEncapsulation, computed, inject, input, output } from '@angular/core';
import { Button } from 'primeng/button';
import { Timeline } from 'primeng/timeline';
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
  imports: [Button, Timeline],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Unencapsulated so the layout can reach into p-timeline; every selector is scoped under the host tag.
  encapsulation: ViewEncapsulation.None,
  template: `
    @switch (entries().state) {
      @case ('loading') {
        <p class="muted">กำลังโหลด…</p>
      }
      @case ('error') {
        <p class="error">โหลด audit trail ไม่สำเร็จ</p>
      }
      @case ('ready') {
        @if (list().length === 0) {
          <p class="muted">ยังไม่มีบันทึก</p>
        } @else {
          <!-- Entries keep their list semantics (the old markup was an <ol>) -->
          <p-timeline class="panel" [value]="list()" align="left" [pt]="listSemantics">
            <ng-template #opposite let-entry>
              <span class="when mono">{{ dateTime(asEntry(entry).at) }}</span>
            </ng-template>
            <ng-template #content let-entry>
              @let e = asEntry(entry);
              <div class="what">
                <span class="when when-inline mono">{{ dateTime(e.at) }}</span>
                <strong>{{ actionLabel(e.action) }}</strong>
                <span class="muted"> · {{ e.actorName }} ({{ role(e.actorRole) }})</span>
                <div class="action mono">{{ e.action }}</div>
                <ul class="changes">
                  @for (c of e.changes; track c.Entity + c.Id) {
                    <li>
                      <p-button link class="entity mono" [label]="c.Entity + ' …' + c.Id.slice(-8)"
                        [ariaLabel]="'ดูประวัติ ' + c.Entity + ' ' + c.Id" (onClick)="openEntity.emit({ type: c.Entity, id: c.Id })" />
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
            </ng-template>
          </p-timeline>
        }
      }
    }
  `,
  styles: `
    app-audit-timeline .panel { display: block; background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 14px 14px 2px; }
    app-audit-timeline .p-timeline-left .p-timeline-event-opposite { flex: 0 0 170px; padding: 1px 12px 0 0; text-align: left; white-space: nowrap; }
    app-audit-timeline .what { padding-bottom: 12px; }
    app-audit-timeline .when { font-size: 0.82rem; color: var(--muted); }
    app-audit-timeline .when-inline { display: none; }
    app-audit-timeline .action { font-size: 0.75rem; color: var(--muted); }
    app-audit-timeline .changes { margin: 4px 0 0; padding-left: 16px; font-size: 0.85rem; overflow-wrap: anywhere; }
    /* Narrow: one column — the time moves into the entry and the opposite column goes */
    @media (max-width: 640px) {
      app-audit-timeline .p-timeline-left .p-timeline-event-opposite { display: none; }
      app-audit-timeline .when-inline { display: block; }
    }
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
  protected readonly listSemantics = { root: { role: 'list' }, event: { role: 'listitem' } };

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

  /** p-timeline hands entries to its templates untyped; this restores the type for the template checker. */
  protected asEntry(entry: AuditEntry): AuditEntry {
    return entry;
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
