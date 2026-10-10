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
}

/** Thai message from a ProblemDetails error, or a generic fallback. */
export function problemMessage(error: unknown): string {
  const detail = (error as { error?: { detail?: unknown } })?.error?.detail;
  return typeof detail === 'string' ? detail : 'เกิดข้อผิดพลาด ลองใหม่อีกครั้ง';
}
