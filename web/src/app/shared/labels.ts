import { AllocationRunStatus, Category, OutcomeKind, RoundStatus, SpaceType, StallRequestStatus } from '../core/api';

export const SPACE_TYPE_LABEL: Record<SpaceType, string> = {
  Relaxation: 'จุดผ่อนผัน',
  DistrictMarket: 'ตลาดนัดเขต',
  TrialArea: 'พื้นที่ทดลองค้า',
};

export const CATEGORY_LABEL: Record<Category, string> = {
  Legacy: 'ผู้ค้าเดิม',
  Vulnerable: 'กลุ่มเปราะบาง',
  New: 'ผู้ค้าใหม่',
};

/** Guardrail 4: submitting is a request, never a booking — Submitted reads "ยื่นคำขอแล้ว". */
export const STATUS_LABEL: Record<StallRequestStatus, string> = {
  Submitted: 'ยื่นคำขอแล้ว',
  Eligible: 'ผ่านการตรวจสิทธิ',
  Ineligible: 'ไม่ผ่านการตรวจสิทธิ',
  Won: 'ได้สิทธิ์',
  Waitlisted: 'สำรอง',
  NotSelected: 'ไม่ได้สิทธิ์',
  Confirmed: 'ยืนยันสิทธิ์แล้ว',
  Forfeited: 'สละสิทธิ์',
};

export const ROUND_STATUS_LABEL: Record<RoundStatus, string> = {
  Open: 'เปิดรับคำขอ',
  Closed: 'ปิดรับแล้ว',
  Drawn: 'จับฉลากแล้ว',
  Approved: 'อนุมัติผลแล้ว',
};

export const RUN_STATUS_LABEL: Record<AllocationRunStatus, string> = {
  Draft: 'ร่าง รออนุมัติ',
  Approved: 'อนุมัติแล้ว',
  Rejected: 'ปฏิเสธแล้ว',
};

export const OUTCOME_LABEL: Record<OutcomeKind, string> = {
  Won: 'ได้สิทธิ์',
  Waitlisted: 'สำรอง',
  NotSelected: 'ไม่ได้สิทธิ์',
  Forfeited: 'สละสิทธิ์',
};

const thaiDate = new Intl.DateTimeFormat('th-TH', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'Asia/Bangkok',
});

/** "2026-10-31" or an ISO instant → "31 ต.ค. 2569" (Buddhist year). */
export function formatThaiDate(value: string): string {
  return thaiDate.format(new Date(value.length === 10 ? `${value}T00:00:00+07:00` : value));
}

const thaiDateTime = new Intl.DateTimeFormat('th-TH', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'Asia/Bangkok',
});

/** ISO instant → "25 ต.ค. 2569 09:00". */
export function formatThaiDateTime(value: string): string {
  return thaiDateTime.format(new Date(value));
}
