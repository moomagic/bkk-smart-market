import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export type SpaceType = 'Relaxation' | 'DistrictMarket' | 'TrialArea';
export type Category = 'Legacy' | 'Vulnerable' | 'New';
export type StallRequestStatus =
  | 'Submitted'
  | 'Eligible'
  | 'Ineligible'
  | 'Won'
  | 'Waitlisted'
  | 'NotSelected'
  | 'Confirmed'
  | 'Forfeited';

export interface OpenRound {
  id: string;
  startsOn: string;
  endsOn: string;
  applyClosesAt: string;
}

export interface SpaceSummary {
  id: string;
  name: string;
  type: SpaceType;
  lockCount: number;
  productTypes: string[];
  openHours: string;
  openRound: OpenRound | null;
}

export interface StallRequest {
  id: string;
  roundId: string;
  spaceId: string;
  spaceName: string;
  roundStartsOn: string;
  roundEndsOn: string;
  submittedAt: string;
  category: Category;
  status: StallRequestStatus;
  ineligibleReason: string | null;
  waitlistPosition: number | null;
  /** Outcome fields come from the approved run only. */
  lockCode: string | null;
  resultReason: string | null;
  referenceNo: string | null;
  confirmBy: string | null;
  /** Every run of the round, newest first. */
  runs: RunReference[];
  lastCheckInAt: string | null;
}

export interface CheckIn {
  id: string;
  stallRequestId: string;
  spaceName: string;
  lockCode: string;
  at: string;
  withinGeofence: boolean;
}

/** What the (simulated) QR posted at a lock encodes — mirrors CheckInQr on the API. */
export function lockQr(spaceId: string, lockCode: string): string {
  return `bkk-smart-market:lock:${spaceId}:${lockCode}`;
}

export interface RunReference {
  referenceNo: string;
  status: AllocationRunStatus;
  ranAt: string;
  rejectReason: string | null;
}

export type RoundStatus = 'Open' | 'Closed' | 'Drawn' | 'Approved';

export interface RoundSummary {
  id: string;
  spaceId: string;
  spaceName: string;
  startsOn: string;
  endsOn: string;
  applyClosesAt: string;
  status: RoundStatus;
  requestCount: number;
  eligibleCount: number;
  ineligibleCount: number;
}

export interface RoundRequestRow {
  requestId: string;
  vendorId: string;
  vendorName: string;
  category: Category;
  status: StallRequestStatus;
  ineligibleReason: string | null;
  carryOverWeight: number;
}

export type AllocationRunStatus = 'Draft' | 'Approved' | 'Rejected';
export type OutcomeKind = 'Won' | 'Waitlisted' | 'NotSelected' | 'Forfeited';

export interface QuotaLine {
  category: Category;
  /** Seats the quota rule reserves for the group. */
  base: number;
  /** Seats filled after rollover. */
  allocated: number;
}

export interface RunOutcome {
  vendorId: string;
  vendorName: string;
  category: Category;
  weight: number;
  kind: OutcomeKind;
  lockCode: string | null;
  waitlistPosition: number | null;
  viaRollover: boolean;
  reason: string;
}

export interface AllocationRun {
  id: string;
  roundId: string;
  spaceName: string;
  referenceNo: string;
  seed: number;
  inputHash: string;
  status: AllocationRunStatus;
  ranAt: string;
  ranBy: string;
  decidedAt: string | null;
  decidedBy: string | null;
  rules: { legacyMax: number; vulnerableMin: number; newMin: number; waitlistPerGroup: number };
  pooledDraw: boolean;
  quota: QuotaLine[];
  outcomes: RunOutcome[];
  unassignedLockCodes: string[];
}

export interface AuditChange {
  Entity: string;
  Id: string;
  State: 'Added' | 'Modified' | 'Deleted';
  Before: Record<string, unknown> | null;
  After: Record<string, unknown> | null;
}

export interface AuditEntry {
  id: string;
  at: string;
  actor: string;
  /** Masked name for vendors. */
  actorName: string;
  actorRole: string;
  action: string;
  entityType: string;
  entityId: string;
  changes: AuditChange[];
}

export interface AssistantAnswer {
  text: string;
  found: boolean;
  citations: { file: string; heading: string }[];
  source: string;
}

export type PreferredTime = 'Morning' | 'Evening';

export interface SpaceMatch {
  spaceId: string;
  name: string;
  score: number;
  parts: { product: number; freeLock: number; area: number; hours: number };
  reasons: string[];
}

export interface Session {
  user: string;
  role: string;
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  spaces() {
    return this.http.get<SpaceSummary[]>('/api/spaces');
  }

  session() {
    return this.http.get<Session>('/api/session');
  }

  submitStallRequest(roundId: string) {
    return this.http.post<StallRequest>('/api/stall-requests', { roundId });
  }

  myStallRequests() {
    return this.http.get<StallRequest[]>('/api/stall-requests/me');
  }

  rounds() {
    return this.http.get<RoundSummary[]>('/api/rounds');
  }

  roundRequests(roundId: string) {
    return this.http.get<RoundRequestRow[]>(`/api/rounds/${roundId}/stall-requests`);
  }

  checkEligibility(roundId: string) {
    return this.http.post<RoundRequestRow[]>(`/api/rounds/${roundId}/eligibility`, null);
  }

  allocationRuns(roundId: string) {
    return this.http.get<AllocationRun[]>(`/api/rounds/${roundId}/allocation-runs`);
  }

  confirmStallRequest(requestId: string) {
    return this.http.post<StallRequest>(`/api/stall-requests/${requestId}/confirm`, null);
  }

  forfeitStallRequest(requestId: string) {
    return this.http.post<StallRequest>(`/api/stall-requests/${requestId}/forfeit`, null);
  }

  checkIn(stallRequestId: string, qrCode: string, withinGeofence: boolean) {
    return this.http.post<CheckIn>('/api/check-ins', { stallRequestId, qrCode, withinGeofence });
  }

  auditTrail(entityType: string, entityId: string) {
    return this.http.get<AuditEntry[]>('/api/audit', { params: { entityType, entityId } });
  }

  matchSpaces(productType: string, preferredArea: string, preferredTime: PreferredTime | null) {
    return this.http.post<SpaceMatch[]>('/api/spaces/match', { productType, preferredArea, preferredTime });
  }

  askAssistant(question: string) {
    return this.http.post<AssistantAnswer>('/api/assistant/ask', { question });
  }

  /** Development only: 404 elsewhere. */
  demoClock() {
    return this.http.get<{ now: string }>('/api/demo/clock');
  }

  setDemoClock(now: string) {
    return this.http.post<{ now: string }>('/api/demo/clock', { now });
  }

  approveAllocationRun(runId: string) {
    return this.http.post<AllocationRun>(`/api/allocation-runs/${runId}/approve`, null);
  }

  /** No seed argument: the system picks it (ADR-0002). */
  runAllocation(roundId: string) {
    return this.http.post<AllocationRun>(`/api/rounds/${roundId}/allocation-runs`, null);
  }
}

/** Thai message from a ProblemDetails error, or a generic fallback. */
export function problemMessage(error: unknown): string {
  const detail = (error as { error?: { detail?: unknown } })?.error?.detail;
  return typeof detail === 'string' ? detail : 'เกิดข้อผิดพลาด ลองใหม่อีกครั้ง';
}
