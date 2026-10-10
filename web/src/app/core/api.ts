import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export type SpaceType = 'Relaxation' | 'DistrictMarket' | 'TrialArea';

export interface SpaceSummary {
  id: string;
  name: string;
  type: SpaceType;
  lockCount: number;
  productTypes: string[];
  openHours: string;
}

export interface Session {
  user: string;
  role: string;
}

export const SPACE_TYPE_LABEL: Record<SpaceType, string> = {
  Relaxation: 'จุดผ่อนผัน',
  DistrictMarket: 'ตลาดนัดเขต',
  TrialArea: 'พื้นที่ทดลองค้า',
};

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  spaces() {
    return this.http.get<SpaceSummary[]>('/api/spaces');
  }

  session() {
    return this.http.get<Session>('/api/session');
  }
}
