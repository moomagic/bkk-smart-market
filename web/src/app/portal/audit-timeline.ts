import { ChangeDetectionStrategy, Component, ViewEncapsulation, computed, inject, input, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Button } from 'primeng/button';
import { Timeline } from 'primeng/timeline';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { Api, AuditEntry } from '../core/api';
import { formatThaiDateTime } from '../shared/labels';
import { AuditNames, ChangeView, EntryView, NO_NAMES, describeEntry } from './audit-view';

type Load = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: AuditEntry[] };

/**
 * Who did what, when, and what changed — for a round or a single entity, in the officer's words (audit-view.ts).
 * Personal data arrives masked ("***"); ids and the use-case name stay one click away under "รายละเอียดทางเทคนิค".
 */
@Component({
  selector: 'app-audit-timeline',
  imports: [Button, NgTemplateOutlet, Timeline],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Unencapsulated so the layout can reach into p-timeline; every selector is scoped under the host tag.
  encapsulation: ViewEncapsulation.None,
  template: `
    @switch (entries().state) {
      @case ('loading') {
        <p class="muted">กำลังโหลด…</p>
      }
      @case ('error') {
        <p class="error">โหลด audit trail ไม่สำเร็จ — <p-button link label="ลองอีกครั้ง" (onClick)="retry.update((n) => n + 1)" /></p>
      }
      @case ('ready') {
        @if (views().length === 0) {
          <p class="muted">ยังไม่มีบันทึก</p>
        } @else {
          <!-- Entries keep their list semantics (the old markup was an <ol>) -->
          <p-timeline class="panel" [value]="views()" align="left" [pt]="listSemantics">
            <ng-template #opposite let-entry>
              <span class="when mono">{{ dateTime(asView(entry).at) }}</span>
            </ng-template>
            <ng-template #content let-entry>
              @let e = asView(entry);
              <div class="what">
                <span class="when when-inline mono">{{ dateTime(e.at) }}</span>
                <div class="head">
                  <strong>{{ e.action }}</strong>
                  <span class="actor">โดย {{ e.actor }}</span>
                </div>
                @if (e.summary.length > 0) {
                  <ul class="summary">
                    @for (line of e.summary; track $index) {
                      <li>{{ line }}</li>
                    }
                  </ul>
                }
                @if (e.fold) {
                  <details class="fold">
                    <summary>ดูทั้ง {{ e.changes.length }} รายการ</summary>
                    <ng-container *ngTemplateOutlet="changeList; context: { $implicit: e.changes }" />
                  </details>
                } @else {
                  <ng-container *ngTemplateOutlet="changeList; context: { $implicit: e.changes }" />
                }
                <details class="fold technical">
                  <summary>รายละเอียดทางเทคนิค</summary>
                  <ul>
                    @for (line of e.technical; track $index) {
                      <li class="mono">{{ line }}</li>
                    }
                  </ul>
                </details>
              </div>
            </ng-template>
          </p-timeline>
        }
      }
    }

    <ng-template #changeList let-changes>
      <ul class="changes">
        @for (c of asChanges(changes); track c.key) {
          <li>
            <span class="subject">
              <p-button link class="entity" [label]="c.subject" [ariaLabel]="'ดูประวัติ ' + c.subject"
                (onClick)="openEntity.emit({ type: c.entity, id: c.id, label: c.subject })" />
              <span class="state">{{ c.state }}</span>
            </span>
            @if (c.fields.length > 0 || c.more > 0) {
              <span class="fields">
                @for (f of c.fields; track f.label) {
                  <span class="field">
                    <span class="key">{{ f.label }}</span>
                    @if (f.before !== undefined) {
                      <span class="from">{{ f.before }}</span><span aria-hidden="true"> → </span><span class="sr-only"> เป็น </span>
                    }
                    <span class="to">{{ f.after }}</span>
                  </span>
                }
                @if (c.more > 0) {
                  <span class="key">อีก {{ c.more }} ฟิลด์</span>
                }
              </span>
            }
          </li>
        }
      </ul>
    </ng-template>
  `,
  styles: `
    app-audit-timeline .panel { display: block; background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: var(--space-4) var(--space-4) 2px; }
    app-audit-timeline .p-timeline-left .p-timeline-event-opposite { flex: 0 0 150px; padding: 1px var(--space-3) 0 0; text-align: left; white-space: nowrap; }
    app-audit-timeline .what { padding-bottom: var(--space-4); }
    app-audit-timeline .when { font-size: var(--text-small); color: var(--muted); }
    app-audit-timeline .when-inline { display: none; }
    app-audit-timeline .head { display: flex; flex-wrap: wrap; align-items: baseline; column-gap: var(--space-2); }
    app-audit-timeline .actor { font-size: var(--text-small); color: var(--muted); }
    app-audit-timeline .summary { margin: var(--space-1) 0 0; padding: 0; list-style: none; font-size: var(--text-small); }
    app-audit-timeline .changes { margin: var(--space-2) 0 0; padding: 0; list-style: none; font-size: var(--text-small); display: grid; gap: var(--space-2); }
    app-audit-timeline .subject { display: flex; flex-wrap: wrap; align-items: baseline; column-gap: var(--space-2); }
    app-audit-timeline .state { color: var(--muted); }
    app-audit-timeline .fields { display: flex; flex-wrap: wrap; column-gap: var(--space-4); row-gap: 2px; overflow-wrap: anywhere; }
    app-audit-timeline .key { color: var(--muted); margin-right: var(--space-1); }
    app-audit-timeline .from { color: var(--muted); }
    app-audit-timeline .fold { margin-top: var(--space-2); font-size: var(--text-small); }
    app-audit-timeline .fold > summary { cursor: pointer; width: fit-content; color: var(--accent); font-weight: 600; }
    app-audit-timeline .fold > summary::marker { color: var(--muted); }
    app-audit-timeline .technical > summary { color: var(--muted); font-weight: 400; }
    app-audit-timeline .technical ul { margin: var(--space-1) 0 0; padding-left: var(--space-4); color: var(--muted); font-size: var(--text-small); overflow-wrap: anywhere; }
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
  /** Masked names the page already holds, so changes name people instead of ids. */
  readonly names = input<AuditNames>(NO_NAMES);
  /** The officer wants the trail of one entity from a change line; label is how that line named it. */
  readonly openEntity = output<{ type: string; id: string; label: string }>();

  protected readonly dateTime = formatThaiDateTime;
  protected readonly listSemantics = { root: { role: 'list' }, event: { role: 'listitem' } };

  /** Bumped by "ลองอีกครั้ง" after a failed load. */
  protected readonly retry = signal(0);

  private readonly target = computed(() => ({
    type: this.entityType(),
    id: this.entityId(),
    version: this.version(),
    retry: this.retry(),
  }));

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

  protected readonly views = computed(() => {
    const e = this.entries();
    const names = this.names();
    return e.state === 'ready' ? e.data.map((entry) => describeEntry(entry, names)) : [];
  });

  /** p-timeline and the change-list template hand values over untyped; these restore the types for the checker. */
  protected asView(entry: EntryView): EntryView {
    return entry;
  }

  protected asChanges(changes: ChangeView[]): ChangeView[] {
    return changes;
  }
}
