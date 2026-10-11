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
  /** The latest review of this request and the district's answer, if any. */
  review: Review | null;
}

export type ReviewOutcome = 'Upheld' | 'Corrected';

export interface Review {
  id: string;
  stallRequestId: string;
  reason: string;
  submittedAt: string;
  outcome: ReviewOutcome | null;
  response: string | null;
  respondedAt: string | null;
}

export interface RoundReview {
  id: string;
  stallRequestId: string;
  vendorName: string;
  category: Category;
  requestStatus: StallRequestStatus;
  ineligibleReason: string | null;
  reason: string;
  submittedAt: string;
  respondBy: string;
  outcome: ReviewOutcome | null;
  response: string | null;
  respondedAt: string | null;
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
  /** Officer rejects still allowed in this round; at 0 only an executive may reject. */
  rejectsLeft: number;
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
  rejectReason: string | null;
  rejectCountsTowardLimit: boolean;
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
  /** 'claude' when the AI model answered, 'faq' when keyword matching did. */
  source: AssistantSource;
}

export type AssistantSource = 'claude' | 'faq';

export type PreferredTime = 'Morning' | 'Evening';

export interface SpaceMatch {
  spaceId: string;
  name: string;
  score: number;
  parts: { product: number; freeLock: number; area: number; hours: number };
  reasons: string[];
}

export interface Ratio {
  count: number;
  of: number;
  percent: number | null;
}

export interface DashboardSpaceRow {
  spaceId: string;
  name: string;
  roundId: string | null;
  startsOn: string | null;
  endsOn: string | null;
  referenceNo: string | null;
  locks: number;
  winners: number;
  confirmed: number;
  quota: { category: Category; base: number; winners: number }[];
}

export interface DashboardKpis {
  occupancy: Ratio;
  leadTime: { medianDays: number | null; runs: number };
  newVendors: Ratio;
  fairRotation: Ratio;
  spaces: DashboardSpaceRow[];
}

export type InsightKind = 'OutsideGeofence' | 'Absent' | 'LowCheckIn' | 'SharedSurname' | 'SharedPhone';
export type InsightSeverity = 'Info' | 'Warning' | 'Critical';

/** Read-only flag for an officer to look into; it never changes anything by itself. */
export interface InsightFlag {
  kind: InsightKind;
  severity: InsightSeverity;
  vendorName: string;
  lockCode: string;
  message: string;
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

  fileReview(stallRequestId: string, reason: string) {
    return this.http.post<Review>(`/api/stall-requests/${stallRequestId}/reviews`, { reason });
  }

  roundInsights(roundId: string) {
    return this.http.get<InsightFlag[]>(`/api/rounds/${roundId}/insights`);
  }

  roundReviews(roundId: string) {
    return this.http.get<RoundReview[]>(`/api/rounds/${roundId}/reviews`);
  }

  respondReview(reviewId: string, outcome: ReviewOutcome, response: string) {
    return this.http.post<Review>(`/api/reviews/${reviewId}/respond`, { outcome, response });
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

  dashboard() {
    return this.http.get<DashboardKpis>('/api/dashboard/kpis');
  }

  askAssistant(question: string) {
    return this.http.post<AssistantAnswer>('/api/assistant/ask', { question });
  }

  /** Development only: 404 elsewhere. */
  demoClock() {
    return this.http.get<{ now: string }>('/api/demo/clock');
  }

  /** Development only: rebuilds the seed data and puts the clock back to Demo:Now. */
  resetDemo() {
    return this.http.post<{ now: string }>('/api/demo/reset', null);
  }

  setDemoClock(now: string) {
    return this.http.post<{ now: string }>('/api/demo/clock', { now });
  }

  rejectAllocationRun(runId: string, reason: string) {
    return this.http.post<AllocationRun>(`/api/allocation-runs/${runId}/reject`, { reason });
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
