import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { Api, SpaceSummary, StallRequest, problemMessage } from '../core/api';
import { DemoSession } from '../core/demo-session';
import {
  CATEGORY_LABEL,
  RUN_STATUS_LABEL,
  SPACE_TYPE_LABEL,
  STATUS_LABEL,
  formatThaiDate,
  formatThaiDateTime,
} from '../shared/labels';

type Load<T> = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: T };

@Component({
  selector: 'app-vendor-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vendor-home.html',
  styleUrl: './vendor-home.scss',
})
export class VendorHome {
  private readonly api = inject(Api);
  protected readonly session = inject(DemoSession);

  protected readonly typeLabel = SPACE_TYPE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly date = formatThaiDate;
  protected readonly dateTime = formatThaiDateTime;
  protected readonly runStatus = RUN_STATUS_LABEL;

  /** Bumped after a submit so both lists reload. */
  private readonly refresh = signal(0);
  private readonly reload$ = combineLatest([toObservable(this.session.role), toObservable(this.refresh)]);

  protected readonly spaces = toSignal(
    this.reload$.pipe(switchMap(() => loadState(this.api.spaces()))),
    { initialValue: { state: 'loading' } as Load<SpaceSummary[]> },
  );

  protected readonly spaceList = computed(() => {
    const s = this.spaces();
    return s.state === 'ready' ? s.data : [];
  });

  protected readonly myRequests = toSignal(
    this.reload$.pipe(
      switchMap(([role]) => (role === 'vendor' ? loadState(this.api.myStallRequests()) : of(null))),
    ),
    { initialValue: null as Load<StallRequest[]> | null },
  );

  protected readonly requestedRoundIds = computed(() => {
    const mine = this.myRequests();
    return new Set(mine?.state === 'ready' ? mine.data.map((r) => r.roundId) : []);
  });

  protected readonly submitting = signal<string | null>(null);
  protected readonly notice = signal<{ kind: 'ok' | 'error'; text: string } | null>(null);

  protected submit(space: SpaceSummary): void {
    if (!space.openRound) return;
    this.submitting.set(space.id);
    this.notice.set(null);
    this.api.submitStallRequest(space.openRound.id).subscribe({
      next: (req) => {
        this.submitting.set(null);
        this.notice.set({
          kind: 'ok',
          text: `${STATUS_LABEL[req.status]} · ${req.spaceName} · กลุ่ม${CATEGORY_LABEL[req.category]} — ยังไม่ใช่การได้สิทธิ์ ผลจับฉลากประกาศหลังปิดรับคำขอ`,
        });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.submitting.set(null);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }
}

function loadState<T>(source: Observable<T>) {
  return source.pipe(
    map((data) => ({ state: 'ready', data }) as Load<T>),
    catchError(() => of({ state: 'error' } as Load<T>)),
    startWith({ state: 'loading' } as Load<T>),
  );
}
