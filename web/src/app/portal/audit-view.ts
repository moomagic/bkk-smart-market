import { AuditChange, AuditEntry } from '../core/api';
import {
  AUDIT_ACTION_LABEL,
  CATEGORY_LABEL,
  REVIEW_OUTCOME_LABEL,
  ROLE_LABEL,
  ROUND_STATUS_LABEL,
  RUN_STATUS_LABEL,
  STATUS_LABEL,
  formatThaiDate,
  formatThaiDateTime,
} from '../shared/labels';

/**
 * Turns stored audit entries into what an officer reads: Thai field names and values, people by their masked
 * name, a one-line summary for commands that touch many rows, and the raw ids kept apart as technical detail.
 * Presentation only — the stored entry is unchanged and stays the record.
 */

/** Masked vendor names the page already has, so a change reads "คำขอของ บุญมี ร." instead of an id. */
export interface AuditNames {
  /** StallRequest id → masked vendor name */
  requests: ReadonlyMap<string, string>;
  /** Vendor id → masked vendor name */
  vendors: ReadonlyMap<string, string>;
}

export const NO_NAMES: AuditNames = { requests: new Map(), vendors: new Map() };

export interface FieldView {
  label: string;
  /** Set only for an edit that had a value before. */
  before?: string;
  after: string;
}

export interface ChangeView {
  key: string;
  entity: string;
  id: string;
  /** "คำขอของ บุญมี ร.", "ผลจัดสรร BPL-2569-1025-01", … */
  subject: string;
  state: string;
  fields: FieldView[];
  /** Fields left off the readable line, e.g. the overflow past MAX_SHOWN_FIELDS. */
  more: number;
}

export interface EntryView {
  id: string;
  at: string;
  /** Thai use-case name, e.g. "ตรวจสิทธิทั้งรอบ" */
  action: string;
  /** "เจ้าหน้าที่เขต · officer-bangphlat", "ผู้ค้า · สมศรี ใ.", "ระบบ" */
  actor: string;
  /** One line per kind of change, only when the command touched more than one row. */
  summary: string[];
  changes: ChangeView[];
  /** Long change lists start folded. */
  fold: boolean;
  /** Use-case name and the ids behind every change, for tracing the record back. */
  technical: string[];
}

const MAX_SHOWN_FIELDS = 6;
/** Change lists longer than this start folded behind their summary. */
const FOLD_AFTER = 3;
/** Values longer than this (a run's result JSON, the rules snapshot) stay in the stored entry only. */
const MAX_TECHNICAL_VALUE_LENGTH = 80;

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const INSTANT = /^\d{4}-\d{2}-\d{2}T/;
const DAY = /^\d{4}-\d{2}-\d{2}$/;

const ENTITY_LABEL: Record<string, string> = {
  StallRequest: 'คำขอ',
  Assignment: 'สิทธิ์ล็อค',
  AllocationRun: 'ผลจัดสรร',
  Round: 'รอบ',
  CheckIn: 'เช็คอิน',
  Review: 'คำขอทบทวน',
  Vendor: 'ข้อมูลผู้ค้า',
};

/** Summary lines follow the story of a round: the requests first, then what was decided about them. */
const ENTITY_ORDER = ['StallRequest', 'AllocationRun', 'Round', 'Assignment', 'CheckIn', 'Review', 'Vendor'];

const STATE_LABEL: Record<AuditChange['State'], string> = { Added: 'สร้าง', Modified: 'แก้ไข', Deleted: 'ลบ' };

const FIELD_LABEL: Record<string, string> = {
  Status: 'สถานะ',
  Category: 'กลุ่ม',
  SubmittedAt: 'ยื่นเมื่อ',
  CarryOverWeight: 'น้ำหนัก',
  IneligibleReason: 'เหตุผลที่ไม่ผ่าน',
  EligibilityCheckedAt: 'ตรวจสิทธิเมื่อ',
  CorrectedByReview: 'แก้ไขจากการทบทวน',
  WaitlistPosition: 'ลำดับสำรอง',
  Seed: 'Seed (ระบบสุ่ม)',
  ReferenceNo: 'เลขอ้างอิง',
  InputHash: 'Input hash',
  RanAt: 'รันเมื่อ',
  RanBy: 'รันโดย',
  DecidedAt: 'ตัดสินเมื่อ',
  DecidedBy: 'ตัดสินโดย',
  RejectReason: 'เหตุผลที่ปฏิเสธ',
  RejectCountsTowardLimit: 'นับรวมจำนวนครั้งที่ปฏิเสธ',
  ConfirmBy: 'ยืนยันภายใน',
  ConfirmedAt: 'ยืนยันเมื่อ',
  ForfeitedAt: 'สละเมื่อ',
  At: 'เวลา',
  Day: 'วันขาย',
  Method: 'วิธี',
  WithinGeofence: 'อยู่ในพื้นที่',
  Reason: 'เหตุผล',
  Outcome: 'ผลการทบทวน',
  Response: 'คำตอบ',
  RespondedAt: 'ตอบเมื่อ',
  RespondedBy: 'ตอบโดย',
  DisplayName: 'ชื่อ',
  MaskedName: 'ชื่อย่อ',
  Phone: 'เบอร์โทร',
  VulnerableReason: 'เหตุผลกลุ่มเปราะบาง',
  ProductType: 'ประเภทสินค้า',
  FoodHygieneTrained: 'ผ่านอบรมสุขาภิบาลอาหาร',
  BannedUntil: 'งดสมัครถึง',
  ConsentAt: 'ให้ความยินยอมเมื่อ',
  StartsOn: 'เริ่มรอบ',
  EndsOn: 'สิ้นสุดรอบ',
  ApplyOpensAt: 'เปิดรับ',
  ApplyClosesAt: 'ปิดรับ',
};

const STATUS_BY_ENTITY: Record<string, Record<string, string>> = {
  StallRequest: STATUS_LABEL,
  Round: ROUND_STATUS_LABEL,
  AllocationRun: RUN_STATUS_LABEL,
};

export function describeEntry(e: AuditEntry, names: AuditNames): EntryView {
  const owner = soleRequester(e.changes, names);
  const changes = e.changes.map((c, i) => describeChange(c, i, names, owner));
  const fold = changes.length > FOLD_AFTER;
  return {
    id: e.id,
    at: e.at,
    action: AUDIT_ACTION_LABEL[e.action] ?? e.action,
    actor: actorOf(e),
    // A short list reads on its own; a folded one needs the summary in its place
    summary: fold ? summarize(e.changes) : [],
    changes,
    fold,
    technical: [`คำสั่ง ${e.action}`, ...e.changes.map(technicalLine)],
  };
}

function actorOf(e: AuditEntry): string {
  const role = ROLE_LABEL[e.actorRole] ?? e.actorRole;
  return e.actorRole === 'system' || !e.actorName ? role : `${role} · ${e.actorName}`;
}

/** "คำขอ 11 รายการ: ผ่านการตรวจสิทธิ 10 · ไม่ผ่านการตรวจสิทธิ 1", "สิทธิ์ล็อค 4 รายการ (สร้าง)", "รอบ: อนุมัติผลแล้ว" */
function summarize(changes: AuditChange[]): string[] {
  const groups = new Map<string, AuditChange[]>();
  for (const c of changes) {
    const key = `${c.Entity}|${c.State}`;
    groups.set(key, [...(groups.get(key) ?? []), c]);
  }
  const rank = (entity: string) => {
    const i = ENTITY_ORDER.indexOf(entity);
    return i === -1 ? ENTITY_ORDER.length : i;
  };
  return [...groups.values()].sort((a, b) => rank(a[0].Entity) - rank(b[0].Entity)).map((group) => {
    const { Entity: entity, State: state } = group[0];
    const noun = ENTITY_LABEL[entity] ?? entity;
    const count = group.length > 1 ? ` ${group.length} รายการ` : '';
    const statuses = new Map<string, number>();
    for (const c of group) {
      const status = c.After?.['Status'];
      if (status !== undefined && status !== null) {
        const label = statusLabel(entity, String(status));
        statuses.set(label, (statuses.get(label) ?? 0) + 1);
      }
    }
    if (statuses.size === 0) return `${STATE_LABEL[state]}${noun}${count}`;
    const order = Object.values(STATUS_BY_ENTITY[entity] ?? {});
    const breakdown = [...statuses.entries()]
      .sort((a, b) => order.indexOf(a[0]) - order.indexOf(b[0]))
      .map(([label, n]) => (group.length > 1 ? `${label} ${n}` : label))
      .join(' · ');
    return `${noun}${count}: ${breakdown}`;
  });
}

function describeChange(c: AuditChange, index: number, names: AuditNames, owner: string | undefined): ChangeView {
  const keys = Object.keys(c.After ?? c.Before ?? {});
  const fields = keys
    // The run's reference number is already its name on the line
    .filter((k) => readable(k, c) && !(c.Entity === 'AllocationRun' && k === 'ReferenceNo'))
    .map((k) => field(k, c))
    .filter((f): f is FieldView => f !== null);
  return {
    key: `${c.Entity}:${c.Id}:${index}`,
    entity: c.Entity,
    id: c.Id,
    subject: subjectOf(c, names, owner),
    state: STATE_LABEL[c.State],
    fields: fields.slice(0, MAX_SHOWN_FIELDS),
    more: Math.max(0, fields.length - MAX_SHOWN_FIELDS),
  };
}

/** Ids, GUID values and long blobs go to the technical detail; the readable line keeps what a person checks. */
function readable(key: string, c: AuditChange): boolean {
  if (key.endsWith('Id') || key === 'InputHash') return false;
  return [c.Before?.[key], c.After?.[key]].every(
    (v) => typeof v !== 'string' || (!GUID.test(v) && v.length <= MAX_TECHNICAL_VALUE_LENGTH && !v.startsWith('{')),
  );
}

function field(key: string, c: AuditChange): FieldView | null {
  const label = FIELD_LABEL[key] ?? key;
  const before = c.Before?.[key];
  const after = c.After?.[key];
  if (c.State === 'Modified') {
    const hadValue = before !== null && before !== undefined;
    return hadValue ? { label, before: value(c.Entity, key, before), after: value(c.Entity, key, after) } : { label, after: value(c.Entity, key, after) };
  }
  const v = after ?? before;
  // A new or removed row lists only what it actually holds; empty and "no" defaults are noise
  if (v === null || v === undefined || v === false) return null;
  return { label, after: value(c.Entity, key, v) };
}

function value(entity: string, key: string, v: unknown): string {
  if (v === null || v === undefined) return '–';
  if (typeof v === 'boolean') return v ? 'ใช่' : 'ไม่ใช่';
  const s = String(v);
  if (key === 'Status') return statusLabel(entity, s);
  if (key === 'Category') return CATEGORY_LABEL[s as keyof typeof CATEGORY_LABEL] ?? s;
  if (key === 'Outcome') return REVIEW_OUTCOME_LABEL[s as keyof typeof REVIEW_OUTCOME_LABEL] ?? s;
  if (key === 'CarryOverWeight') return `×${s}`;
  if (INSTANT.test(s)) return formatThaiDateTime(s);
  if (DAY.test(s)) return formatThaiDate(s);
  return s;
}

function statusLabel(entity: string, status: string): string {
  return STATUS_BY_ENTITY[entity]?.[status] ?? status;
}

/**
 * The one vendor whose request a command touched, if there is exactly one. An edit to an Assignment stores only
 * the changed fields (no VendorId), so a confirm or forfeit names its owner through the request edited with it.
 */
function soleRequester(changes: AuditChange[], names: AuditNames): string | undefined {
  const people = new Set(
    changes.filter((c) => c.Entity === 'StallRequest').map((c) => names.requests.get(c.Id)).filter((n) => n !== undefined),
  );
  return people.size === 1 ? [...people][0] : undefined;
}

function subjectOf(c: AuditChange, names: AuditNames, owner: string | undefined): string {
  const vendorId = c.After?.['VendorId'] ?? c.Before?.['VendorId'];
  const vendor = typeof vendorId === 'string' ? names.vendors.get(vendorId) : owner;
  switch (c.Entity) {
    case 'StallRequest': {
      // Never the entry's sole requester: that name belongs to a different request than this unknown one
      const name = names.requests.get(c.Id) ?? (typeof vendorId === 'string' ? names.vendors.get(vendorId) : undefined);
      return name ? `คำขอของ ${name}` : `คำขอ …${c.Id.slice(-4)}`;
    }
    case 'Assignment':
      return vendor ? `สิทธิ์ล็อคของ ${vendor}` : `สิทธิ์ล็อค …${c.Id.slice(-4)}`;
    case 'AllocationRun': {
      const ref = c.After?.['ReferenceNo'] ?? c.Before?.['ReferenceNo'];
      return typeof ref === 'string' ? `ผลจัดสรร ${ref}` : 'ผลจัดสรร';
    }
    case 'Vendor': {
      const name = names.vendors.get(c.Id);
      return name ? `ผู้ค้า ${name}` : 'ข้อมูลผู้ค้า';
    }
    default:
      return ENTITY_LABEL[c.Entity] ?? c.Entity;
  }
}

/** "StallRequest 3bb5…41df (สร้าง) · RoundId 5000…0001 · VendorId 4000…0001" */
function technicalLine(c: AuditChange): string {
  const ids = Object.keys(c.After ?? c.Before ?? {})
    .filter((k) => !readable(k, c))
    .map((k) => {
      const v = c.After?.[k] ?? c.Before?.[k];
      const s = v === null || v === undefined ? '–' : String(v);
      return s.length > MAX_TECHNICAL_VALUE_LENGTH ? `${k} (เก็บครบในบันทึก)` : `${k} ${s}`;
    });
  return [`${c.Entity} ${c.Id} (${STATE_LABEL[c.State]})`, ...ids].join(' · ');
}
