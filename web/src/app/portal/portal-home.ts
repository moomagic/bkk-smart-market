import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { Api, RoundRequestRow, RoundSummary, problemMessage } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { CATEGORY_LABEL, ROUND_STATUS_LABEL, STATUS_LABEL, formatThaiDate } from '../shared/labels';

type Load<T> = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: T };

@Component({
  selector: 'app-portal-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-home.html',
  styleUrl: './portal-home.scss',
})
export class PortalHome {
  private readonly api = inject(Api);
  protected readonly session = inject(DemoSession);

  protected readonly date = formatThaiDate;
  protected readonly roundStatus = ROUND_STATUS_LABEL;
  protected readonly status = STATUS_LABEL;
  protected readonly category = CATEGORY_LABEL;

  protected readonly isOfficer = computed(() => this.session.role() !== 'vendor');
  private readonly refresh = signal(0);
  protected readonly selectedId = signal<string | null>(null);

  protected readonly rounds = toSignal(
    combineLatest([toObservable(this.isOfficer), toObservable(this.refresh)]).pipe(
      switchMap(([officer]) => (officer ? load(this.api.rounds()) : of({ state: 'ready', data: [] } as Load<RoundSummary[]>))),
    ),
    { initialValue: { state: 'loading' } as Load<RoundSummary[]> },
  );

  protected readonly roundList = computed(() => {
    const r = this.rounds();
    return r.state === 'ready' ? r.data : [];
  });

  protected readonly selected = computed(() => this.roundList().find((r) => r.id === this.selectedId()) ?? null);

  protected readonly requests = toSignal(
    combineLatest([toObservable(this.selectedId), toObservable(this.refresh)]).pipe(
      switchMap(([id]) => (id ? load(this.api.roundRequests(id)) : of(null))),
    ),
    { initialValue: null as Load<RoundRequestRow[]> | null },
  );

  protected readonly requestRows = computed(() => {
    const r = this.requests();
    return r?.state === 'ready' ? r.data : [];
  });

  protected readonly checking = signal(false);
  protected readonly notice = signal<{ kind: 'ok' | 'error'; text: string } | null>(null);

  protected select(round: RoundSummary): void {
    this.notice.set(null);
    this.selectedId.set(round.id);
  }

  protected checkEligibility(round: RoundSummary): void {
    this.checking.set(true);
    this.notice.set(null);
    this.api.checkEligibility(round.id).subscribe({
      next: (rows) => {
        this.checking.set(false);
        const failed = rows.filter((r) => r.status === 'Ineligible').length;
        this.notice.set({ kind: 'ok', text: `ตรวจสิทธิแล้ว ${rows.length} คำขอ · ไม่ผ่าน ${failed}` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.checking.set(false);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }
}

function load<T>(source: Observable<T>) {
  return source.pipe(
    map((data) => ({ state: 'ready', data }) as Load<T>),
    catchError(() => of({ state: 'error' } as Load<T>)),
    startWith({ state: 'loading' } as Load<T>),
  );
}
