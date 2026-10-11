import { AllocationRunStatus, StallRequest } from '../core/api';
import { CATEGORY_LABEL, formatThaiDateTime } from '../shared/labels';

/** The vendor-facing word for a run: they wait on the district, they do not hold a draft. */
export const VENDOR_RUN_STATUS: Record<AllocationRunStatus, string> = {
  Draft: 'รอเขตอนุมัติ',
  Approved: 'ประกาศผลแล้ว',
  Rejected: 'เขตไม่อนุมัติ จับฉลากใหม่',
};

/** Only an Ineligible result can be reviewed (draw outcomes cannot), once per request; the server checks the window. */
export function canReview(req: StallRequest): boolean {
  return req.status === 'Ineligible' && !req.review;
}

/** One plain line on what happens next, for the states where the vendor is waiting rather than deciding. */
export function nextStep(req: StallRequest): string | null {
  switch (req.status) {
    case 'Submitted':
      return 'รอเขตตรวจสิทธิ';
    case 'Eligible':
      return req.runs.some((r) => r.status === 'Draft')
        ? 'เขตจับฉลากแล้ว รออนุมัติผล'
        : 'ผ่านการตรวจสิทธิแล้ว รอเขตจับฉลาก';
    case 'Ineligible':
      return canReview(req) ? 'ถ้าคิดว่าตรวจสิทธิผิด ขอทบทวนผลได้' : null;
    case 'Waitlisted':
      return req.waitlistPosition
        ? `สำรองลำดับ ${req.waitlistPosition} · ถ้ามีผู้ได้สิทธิ์สละ คุณจะเลื่อนขึ้นตามลำดับ`
        : 'สำรอง · ถ้ามีผู้ได้สิทธิ์สละ คุณจะเลื่อนขึ้นตามลำดับ';
    case 'NotSelected':
      return 'ถ้ายื่นรอบถัดไปของพื้นที่นี้ จะได้น้ำหนัก ×2 ในการจับฉลาก';
    default:
      return null;
  }
}

/** The one line a card in the list carries: the lock and its deadline when there is one, otherwise what comes next. */
export function cardLine(req: StallRequest): string | null {
  if (req.status === 'Won' && req.lockCode) {
    return req.confirmBy
      ? `ได้ล็อค ${req.lockCode} · ยืนยันสิทธิ์ภายใน ${formatThaiDateTime(req.confirmBy)}`
      : `ได้ล็อค ${req.lockCode}`;
  }
  if (req.status === 'Confirmed' && req.lockCode) return `ล็อค ${req.lockCode}`;
  if (req.status === 'Ineligible' && req.ineligibleReason) return req.ineligibleReason;
  return nextStep(req);
}

export interface PendingStep {
  label: string;
  detail: string | null;
  state: 'done' | 'current' | 'upcoming';
}

/**
 * The district's steps for a request still waiting on it — submitted, checked, drawn, announced — and where this one
 * stands. Only for Submitted and Eligible: every later state has its own result to show instead. The last step
 * names every possible result, so a pending request never reads as a lock on its way.
 */
export function pendingSteps(req: StallRequest): PendingStep[] | null {
  if (req.status !== 'Submitted' && req.status !== 'Eligible') return null;
  const checked = req.status === 'Eligible';
  const draft = req.runs.some((r) => r.status === 'Draft');
  const redraw = !draft && req.runs.some((r) => r.status === 'Rejected');
  return [
    { label: 'ยื่นคำขอ', detail: formatThaiDateTime(req.submittedAt), state: 'done' },
    {
      label: 'ตรวจสิทธิ',
      detail: checked ? 'ผ่านการตรวจสิทธิแล้ว' : 'รอเขตตรวจสิทธิ',
      state: checked ? 'done' : 'current',
    },
    {
      label: 'จับฉลาก',
      detail: !checked
        ? `จับฉลากตามโควตา${CATEGORY_LABEL[req.category]}`
        : draft
          ? 'เขตจับฉลากแล้ว รออนุมัติผล'
          : redraw
            ? 'เขตไม่อนุมัติผลครั้งก่อน รอจับฉลากใหม่'
            : 'รอเขตจับฉลาก',
      state: checked ? 'current' : 'upcoming',
    },
    { label: 'ประกาศผล', detail: 'ได้สิทธิ์ สำรอง หรือไม่ได้สิทธิ์ ตามผลจับฉลาก', state: 'upcoming' },
  ];
}
