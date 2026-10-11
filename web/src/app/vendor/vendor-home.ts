import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { Card } from 'primeng/card';
import { Checkbox } from 'primeng/checkbox';
import { Message } from 'primeng/message';
import { Tag } from 'primeng/tag';
import { Textarea } from 'primeng/textarea';
import { Observable, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { Api, SpaceSummary, StallRequest, StallRequestStatus, lockQr, problemMessage } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { AssistantChat } from './assistant-chat';
import { SpaceMatching } from './space-matching';
import { StatusTag } from '../shared/status-tag';
import {
  CATEGORY_LABEL,
  REVIEW_OUTCOME_LABEL,
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
  imports: [FormsModule, Button, Card, Checkbox, Message, Tag, Textarea, AssistantChat, SpaceMatching, StatusTag],
  templateUrl: './vendor-home.html',
  styleUrl: './vendor-home.scss',
})
export class VendorHome {
  private readonly api = inject(Api);
  protected readonly session = inject(DemoSession);

  protected readonly typeLabel = SPACE_TYPE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly date = formatThaiDate;
  protected readonly dateTime = formatThaiDateTime;
  protected readonly runStatus = RUN_STATUS_LABEL;
  protected readonly reviewOutcome = REVIEW_OUTCOME_LABEL;
  /** Request id whose review form is open, and the reason typed so far. */
  protected readonly reviewing = signal<string | null>(null);
  protected readonly reviewReason = signal('');

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

  /** The vendor's request status in each round they applied to. */
  protected readonly requestStatusByRound = computed(() => {
    const mine = this.myRequests();
    return new Map(mine?.state === 'ready' ? mine.data.map((r) => [r.roundId, r.status]) : []);
  });

  /** A matched space the vendor can submit to right now, for the "ยื่นคำขอที่นี่" button. */
  protected readonly submittable = computed(() => {
    const spaces = this.spaceList();
    const requested = this.requestStatusByRound();
    const isVendor = this.session.role() === 'vendor';
    return (spaceId: string): SpaceSummary | null => {
      const space = spaces.find((s) => s.id === spaceId);
      return isVendor && space?.openRound && !requested.has(space.openRound.id) ? space : null;
    };
  });

  /** The status of the vendor's request in a space's open round, or null when they have none there. */
  protected readonly requestStatus = computed(() => {
    const spaces = this.spaceList();
    const requested = this.requestStatusByRound();
    return (spaceId: string): StallRequestStatus | null => {
      const round = spaces.find((s) => s.id === spaceId)?.openRound;
      return (round && requested.get(round.id)) ?? null;
    };
  });

  protected readonly submitting = signal<string | null>(null);
  /** Request id with a confirm/forfeit in flight. */
  protected readonly acting = signal<string | null>(null);
  /** Request id whose forfeit is waiting for a second, deliberate click. */
  protected readonly confirmingForfeit = signal<string | null>(null);
  /** Demo toggle: pretend the phone is outside the space when scanning. */
  protected readonly outsideGeofence = signal(false);
  protected readonly notice = signal<{ kind: 'ok' | 'warn' | 'error'; text: string } | null>(null);

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

  /** Retry after a failed load. */
  protected reload(): void {
    this.refresh.update((n) => n + 1);
  }

  protected confirm(req: StallRequest): void {
    this.act(req, this.api.confirmStallRequest(req.id), (r) => `ยืนยันสิทธิ์แล้ว · ${r.spaceName} ล็อค ${r.lockCode}`);
  }

  protected forfeit(req: StallRequest): void {
    this.act(req, this.api.forfeitStallRequest(req.id), (r) => `สละสิทธิ์แล้ว · ${r.spaceName} — ล็อคส่งต่อให้ผู้สำรองตามลำดับ`);
  }

  /** Simulates scanning the QR posted at the vendor's own lock. */
  protected checkIn(req: StallRequest): void {
    if (!req.lockCode) return;
    this.acting.set(req.id);
    this.notice.set(null);
    this.api.checkIn(req.id, lockQr(req.spaceId, req.lockCode), !this.outsideGeofence()).subscribe({
      next: (c) => {
        this.acting.set(null);
        this.notice.set({
          kind: c.withinGeofence ? 'ok' : 'warn',
          text: c.withinGeofence
            ? `เช็คอินแล้ว · ${c.spaceName} ล็อค ${c.lockCode} · ${formatThaiDateTime(c.at)}`
            : `เช็คอินแล้ว แต่ตำแหน่งอยู่นอกพื้นที่ — บันทึกไว้ให้เจ้าหน้าที่ตรวจสอบ`,
        });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.acting.set(null);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  /** Only an Ineligible result can be reviewed (draw outcomes cannot), once per request; the server checks the window. */
  protected canReview(req: StallRequest): boolean {
    return req.status === 'Ineligible' && !req.review;
  }

  protected fileReview(req: StallRequest): void {
    const reason = this.reviewReason().trim();
    if (!reason) return;
    this.acting.set(req.id);
    this.notice.set(null);
    this.api.fileReview(req.id, reason).subscribe({
      next: () => {
        this.acting.set(null);
        this.reviewing.set(null);
        this.reviewReason.set('');
        this.notice.set({ kind: 'ok', text: `ยื่นขอทบทวนผลแล้ว · ${req.spaceName} — รอเขตตอบ` });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.acting.set(null);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
      },
    });
  }

  private act(req: StallRequest, call: Observable<StallRequest>, message: (r: StallRequest) => string): void {
    this.acting.set(req.id);
    this.notice.set(null);
    call.subscribe({
      next: (r) => {
        this.acting.set(null);
        this.confirmingForfeit.set(null);
        this.notice.set({ kind: 'ok', text: message(r) });
        this.refresh.update((n) => n + 1);
      },
      error: (err) => {
        this.acting.set(null);
        this.confirmingForfeit.set(null);
        this.notice.set({ kind: 'error', text: problemMessage(err) });
        this.refresh.update((n) => n + 1);
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
