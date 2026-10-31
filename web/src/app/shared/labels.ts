import { AllocationRunStatus, Category, InsightKind, OutcomeKind, ReviewOutcome, RoundStatus, RunMismatch, RunVerification, SpaceType, StallRequestStatus } from '../core/api';

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

/** One line for a replay verification: what an officer or vendor reads under the run's evidence. */
export function verificationSummary(v: RunVerification): string {
  if (v.resultMatches && v.inputHashMatches) return 'ผลตรงกันทุกราย · ค่าแฮชข้อมูลตรงกัน';
  const hash = v.inputHashMatches ? 'ค่าแฮชข้อมูลตรงกัน' : 'ค่าแฮชข้อมูลไม่ตรง: ข้อมูลนำเข้าถูกเปลี่ยนหลังจับ';
  return v.resultMatches ? `ผลตรงกันทุกราย · ${hash}` : `ผลไม่ตรงกับที่บันทึกไว้ · ${hash}`;
}

const MISMATCH_FIELD: Record<RunMismatch['field'], string> = {
  kind: 'ผล',
  lockCode: 'ล็อค',
  waitlistPosition: 'ลำดับสำรอง',
  outcome: 'ผล',
  quota: 'โควตา',
  unassignedLockCodes: 'ล็อคที่ว่าง',
  pooledOrder: 'ลำดับการจับรวม',
  order: 'ลำดับ',
  record: 'ผลที่บันทึก',
};

/** "สมศรี ใ. · ล็อค: บันทึกไว้ B3 · รันซ้ำได้ B2" */
export function describeMismatch(m: RunMismatch): string {
  if (m.field === 'record') return 'ผลที่บันทึกไว้อ่านกลับไม่ได้ · ถูกแก้ไขหลังจับ';
  if (m.field === 'order') return 'ลำดับในผลที่บันทึกต่างจากลำดับที่จับได้';
  if (m.field === 'outcome') return `${m.vendorName ?? ''} · ไม่อยู่ในผลที่รันซ้ำ`;
  const show = (v: string | null) =>
    v === null ? 'ไม่มี' : m.field === 'kind' ? (OUTCOME_LABEL[v as OutcomeKind] ?? v) : v;
  const who = m.vendorName ? `${m.vendorName} · ` : '';
  return `${who}${MISMATCH_FIELD[m.field]}: บันทึกไว้ ${show(m.recorded)} · รันซ้ำได้ ${show(m.replayed)}`;
}

export const REVIEW_OUTCOME_LABEL: Record<ReviewOutcome, string> = {
  Upheld: 'ยืนตามเดิม',
  Corrected: 'แก้ไขแล้ว',
};

/** Allocation Insight flag kinds (read-only checks for the officer). */
export const INSIGHT_KIND_LABEL: Record<InsightKind, string> = {
  OutsideGeofence: 'เช็คอินนอกพื้นที่',
  Absent: 'ขาดเช็คอิน',
  LowCheckIn: 'เช็คอินต่ำกว่าเกณฑ์',
  SharedSurname: 'นามสกุลซ้ำ',
  SharedPhone: 'เบอร์โทรซ้ำ',
  SharedDevice: 'อุปกรณ์ใช้ร่วมกัน',
};

/** Audit Action (use-case name) → what the officer reads on the timeline. */
export const AUDIT_ACTION_LABEL: Record<string, string> = {
  'StallRequest.Submit': 'ยื่นคำขอ',
  'Round.CheckEligibility': 'ตรวจสิทธิทั้งรอบ',
  'Round.RunAllocation': 'รันจัดสรร (ร่าง)',
  'Round.ApproveAllocation': 'อนุมัติผลจัดสรร',
  'Round.ConfirmAssignment': 'ยืนยันสิทธิ์',
  'Round.ForfeitAssignment': 'สละสิทธิ์',
  'Round.ExpireAssignment': 'ไม่ยืนยันภายในกำหนด (ระบบ)',
  'Round.CheckIn': 'เช็คอิน',
  'Round.RejectAllocation': 'ปฏิเสธผลจัดสรร',
  'Round.FileReview': 'ยื่นขอทบทวนผล',
  'Round.RespondReview': 'ตอบคำขอทบทวน',
};

export const ROLE_LABEL: Record<string, string> = {
  vendor: 'ผู้ค้า',
  officer: 'เจ้าหน้าที่เขต',
  executive: 'ผู้บริหาร',
  system: 'ระบบ',
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

const thaiDateTimeSeconds = new Intl.DateTimeFormat('th-TH', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  timeZone: 'Asia/Bangkok',
});

/** ISO instant → "25 ต.ค. 2569 09:00:04": the audit trail, where entries in one minute must still read in order. */
export function formatThaiDateTimeSeconds(value: string): string {
  return thaiDateTimeSeconds.format(new Date(value));
}
