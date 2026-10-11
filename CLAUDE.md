# BKK Smart Market — Prototype

Working prototype สำหรับ business pitch (การบ้าน course · วันสอบมีเจ้าหน้าที่ กทม. ร่วมฟัง)
บริหารการขอใช้และจัดสรร **พื้นที่ค้าขายเดิมที่ กทม. อนุญาตแล้ว** ให้เข้าถึงง่าย โปร่งใส และเป็นธรรม
Pilot: **เขตบางพลัด** · ข้อมูลทั้งหมดเป็น **mock data** · Policy anchor: P218

---

## Guardrails (ห้ามละเมิด)

1. **AI ไม่ตัดสินสิทธิ** — AI แนะนำ/ตอบคำถาม/แจ้งเตือนเท่านั้น การจัดสรรเป็น rule-based และเจ้าหน้าที่อนุมัติเสมอ
2. **Allocation ต้อง deterministic และตรวจย้อนได้** — ใช้ seeded RNG, บันทึก seed + input snapshot hash + เลขอ้างอิง, รันซ้ำด้วย seed เดิมต้องได้ผลเดิม
3. **ทุก command เขียน Audit entry 1 แถว** — ใคร ทำอะไร (`Action` = ชื่อ use case) เมื่อไร กับ entity ไหน ค่าก่อน/หลังเฉพาะฟิลด์ที่เปลี่ยน · ฟิลด์ข้อมูลส่วนบุคคล mask ก่อนบันทึก
4. **"จอง" = ยื่นคำขอ/เข้าคิว** ไม่ใช่ได้สิทธิ — ห้ามใช้คำว่า "จองสำเร็จ" ใน UI ให้ใช้ "ยื่นคำขอแล้ว"
5. **ทุกหน้าจอมีป้าย "ข้อมูลจำลอง"** — ชื่อผู้ค้าแสดงแบบย่อนามสกุล (สมศรี ใ.)
6. **PDPA** — เก็บเท่าที่จำเป็น, มี consent record, ไม่ log ข้อมูลส่วนบุคคลลง application log

---

## Stack

| Layer | Choice |
|---|---|
| API | .NET 10 Minimal API · Clean Architecture · MediatR-free (ใช้ handler class ตรงๆ) · FluentValidation |
| Persistence | EF Core 10 + PostgreSQL 17 · migrations ใน Infrastructure |
| Web | Angular 21 · standalone components · signals · zoneless · SCSS design tokens (ไม่ใช้ UI library หนัก) |
| AI | Claude Messages API ผ่าน typed `HttpClient` · model จาก env `ANTHROPIC_MODEL` · ไม่มี key = fallback FAQ matching |
| Test | xUnit + FluentAssertions · Testcontainers (PostgreSQL) สำหรับ integration |
| Run | Docker Compose · images ต้องรันบน arm64 (Apple Silicon) |

---

## Repo layout

```
bkk-smart-market/
├── CLAUDE.md
├── CONTEXT.md                  # glossary — ใช้คำตามนี้ในโค้ด/issue/test
├── docker-compose.yml
├── docs/adr/                   # การตัดสินใจที่ย้อนยาก (อ่านก่อนแตะ allocation)
├── docs/rules/                 # ระเบียบ/กติกา (markdown) — แหล่งเดียวที่ Vendor Assistant ตอบได้
├── api/
│   ├── src/
│   │   ├── SmartMarket.Domain/          # entities, value objects, domain rules — ไม่มี dependency
│   │   ├── SmartMarket.Application/     # use cases, ports, DTOs, allocation engine
│   │   ├── SmartMarket.Infrastructure/  # EF Core, Claude client, seed data
│   │   └── SmartMarket.Api/             # endpoints, auth (mock), DI
│   └── tests/
│       ├── SmartMarket.Application.Tests/   # allocation rules — สำคัญที่สุด
│       └── SmartMarket.Api.Tests/
└── web/                        # Angular workspace, app เดียว 3 areas
    └── src/app/
        ├── core/               # api client, role session, interceptors
        ├── shared/             # design tokens, ui primitives, mock badge
        ├── vendor/             # /vendor  — mobile-first, LIFF-ready
        ├── portal/             # /portal  — เจ้าหน้าที่เขต
        └── dashboard/          # /dashboard — ผู้บริหาร
```

Feature-folder ภายใน Application ตาม module: `Spaces`, `Vendors`, `StallRequests`, `Allocation`, `Approval`, `CheckIn`, `Audit`, `Assistant`, `Reporting`

---

## Domain model

```
District        Id, Name
Space           Id, DistrictId, Name, Type(Relaxation|DistrictMarket|TrialArea), Slots, ProductTypes[],
                OpenHours, Rules (markdown ref), IsActive
Round           Id, SpaceId, StartsOn, EndsOn, ApplyOpensAt, ApplyClosesAt, Status(Open|Closed|Drawn|Approved)
Lock            Id, SpaceId, Code ("B1".."B4")
Vendor          Id, DisplayName, MaskedName, Phone (เก็บเต็ม · mask ใน audit/UI), VulnerableReason?, ProductType,
                FoodHygieneTrained, BannedUntil?, ConsentAt
StallRequest    Id, RoundId, VendorId, SubmittedAt, Category(Legacy|Vulnerable|New) ← snapshot ตอนยื่น,
                Status(Submitted|Eligible|Ineligible|Won|Waitlisted|NotSelected|Confirmed|Forfeited),
                IneligibleReason?, CarryOverWeight (1|2), WaitlistPosition?
AllocationRun   Id, RoundId, ReferenceNo, Seed, InputHash, RulesSnapshot, RanAt, RanBy,
                ResultJson (ผลจับฉลาก · ห้ามแก้), CurrentResultJson (หลังสละสิทธิ์/เลื่อนสำรอง · ADR-0004),
                Status(Draft|Approved|Rejected), DecidedAt?, DecidedBy?, RejectReason?, RejectCountsTowardLimit
Assignment      Id, RoundId, VendorId, LockId, ConfirmBy, ConfirmedAt?, ForfeitedAt? (แถวที่สละแล้วเก็บเป็นประวัติ)
Review          Id, StallRequestId, Reason, SubmittedAt, Outcome?(Upheld|Corrected), Response?, RespondedAt?
CheckIn         Id, AssignmentId, At, Day (วันตามเวลาไทย · 1 ครั้ง/วัน), Method(QR), WithinGeofence
AuditEntry      Id, At, Actor, ActorRole, Action, EntityType, EntityId (entity หลักของ command),
                Changes (jsonb: [{Entity, Id, State, Before?, After?}] ทุก entity ที่ command แตะ · ฟิลด์ [PersonalData] = "***")
```

---

## Business rules (ค่า default — ทุกตัวอยู่ใน `appsettings` section `Rules`)

| Rule | Default |
|---|---|
| ผู้สมัครต่อรอบ | 1 คน 1 ล็อค ทั้งเขต |
| คุณสมบัติ | มีตัวตน · ขายอาหารต้อง `FoodHygieneTrained` · ไม่อยู่ระหว่าง `BannedUntil` |
| โควตาต่อพื้นที่ | Legacy ≤ 50% · Vulnerable ≥ 25% · New ≥ 25% |
| จัดสรร | จับฉลากถ่วงน้ำหนักภายในกลุ่ม · ไม่ใช่ FCFS |
| Carry-over | รอบก่อนของ**พื้นที่เดียวกัน**จบด้วย NotSelected หรือ Waitlisted ที่ไม่ได้เลื่อน → น้ำหนัก ×2 (สูงสุด ×2 ไม่ทบ · Ineligible/Forfeited ไม่นับ) |
| รอบ | Relaxation/DistrictMarket 6 เดือน · TrialArea 1 เดือน (สมัครได้ 1 ครั้ง/ปี) |
| ต่อสิทธิ์ | ต่อเนื่องสูงสุด 2 รอบ |
| ยืนยันสิทธิ์ | ภายใน 3 วัน ไม่ยืนยัน = Forfeited (คำนวณตอนอ่านจาก `IClock` · ไม่มี background job) → waitlist กลุ่มเดียวกันเลื่อนขึ้น |
| สำรอง | สูงสุด 3 คน **ต่อกลุ่ม** |
| Reject | ต้องมีเหตุผล · ≤ 2 ครั้ง/รอบ เกินนั้นต้องผู้บริหารอนุมัติ · reject จาก Review = Corrected ไม่นับ |
| การใช้พื้นที่ | เช็คอิน ≥ 80% ของวันขาย/เดือน · ขาดไม่แจ้ง 7 วันติด = เตือน · 14 วัน = ยกเลิกสิทธิ์ |
| เช่าช่วง | ยืนยันพบ = ยกเลิกสิทธิ์ + งดสมัคร 1 รอบ |
| ขอทบทวนผล | ภายใน 7 วัน · เขตตอบภายใน 7 วัน · ผล Upheld หรือ Corrected (เฉพาะตรวจสิทธิผิด) — ผลจับฉลากทบทวนไม่ได้ |

**Implement จริง:** คุณสมบัติ 3 ข้อ · 1 คน 1 คำขอที่ยังไม่จบทั้งเขตต่อช่วงรอบที่ทับกัน (Ineligible/NotSelected/Forfeited = จบแล้ว) · TrialArea 1 ครั้ง/ปี · โควตา · carry-over · ยืนยัน/สละ · สำรอง · Reject limit · ขอทบทวน
**Flag read-only (Allocation Insight):** เช็คอิน < 80% · ขาด 7/14 วัน
**Config + `docs/rules` เท่านั้น (ยังไม่ implement — ระบุใน README):** ต่อสิทธิ์ 2 รอบ · เช่าช่วง

**Demo clock:** ทุก rule อ่านเวลาจาก `IClock` · `Demo:Now` (default `2026-10-25` = 25 ต.ค. 2569 อยู่ในช่วงเปิดรับ) · portal มีปุ่มเลื่อนเวลา (Development only)

### Allocation algorithm (spec — ต้องมี unit test ครบทุกข้อ · เหตุผลอยู่ใน ADR-0001, ADR-0002)

Input: round, ล็อค `N`, eligible stall requests (`VendorId`, `Category`, `CarryOverWeight`), `seed`, `RulesSnapshot`

0. เรียง input ตาม `VendorId` (stable) ก่อนเสมอ — ผลต้องไม่ขึ้นกับลำดับ insert
1. คำนวณโควตา
   - N=1 → จับรวมทุกกลุ่ม 1 ที่
   - N=2 → Vulnerable 1 + (New หรือ Legacy ตามที่มีผู้สมัคร; มีทั้งคู่ → New) 1
   - N≥3 → `vulnerableMin = ceil(N × 0.25)`, `newMin = ceil(N × 0.25)`, `legacyMax = min(N − vulnerableMin − newMin, floor(N × 0.5))` ส่วนที่เหลือ (ถ้ามี) เข้า New
   - ตัวอย่าง N=4 → Legacy 2 · Vulnerable 1 · New 1 · N=5 → 1 · 2 · 2
2. **Rollover** (คำนวณจากจำนวนผู้สมัครก่อนจับ): โควตาที่ผู้สมัครไม่พอ โอนไปกลุ่มถัดไปเป็นวงจร Vulnerable → New → Legacy → Vulnerable · Legacy เกิน 50% ได้ **เฉพาะจาก rollover**
3. จับฉลากในแต่ละกลุ่มด้วย **weighted sampling without replacement** (น้ำหนัก = `CarryOverWeight`) ใช้ `Random(seed)` ตัวเดียว ลำดับกลุ่มคงที่: Legacy → Vulnerable → New · จับต่อจนหมดกลุ่มเพื่อได้ลำดับสำรอง
4. ผู้ไม่ได้สิทธิ์ในแต่ละกลุ่ม เรียงตามลำดับที่ถูกจับ → Waitlist ของกลุ่มนั้น 1..3 · ที่เหลือ NotSelected
5. สลับลำดับรหัสล็อค (B1..BN) ด้วย RNG ตัวเดิม (Fisher–Yates) แล้ว assign ให้ผู้ได้สิทธิ์ — ไม่ใช่ตามลำดับที่ถูกจับ
6. บันทึก `AllocationRun` (Draft) พร้อม `ReferenceNo = BPL-{yyyy+543}-{MMdd}-{seq:00}`, `Seed`, `RulesSnapshot`,
   `InputHash = SHA256(canonical JSON { requests: [[VendorId, Category, Weight]...] เรียงตาม VendorId, N, RulesSnapshot })`
7. Seed มาจาก crypto RNG — เจ้าหน้าที่กำหนดเองไม่ได้ (ยกเว้น demo seed ผ่าน seed data)
8. เจ้าหน้าที่ Approve → Assignment ถูกสร้าง (ConfirmBy = +3 วัน) · Reject (ต้องมีเหตุผล) → รันใหม่ด้วย seed ใหม่ · ทุก run แสดงให้ผู้ค้าเห็น
9. Forfeit → เลื่อน waitlist กลุ่มเดียวกันขึ้นก่อน · กลุ่มว่าง → ตามวงจร rollover · ล็อคที่ว่างตกทอดไปยังผู้เลื่อนขึ้น

Test ขั้นต่ำ: โควตาถูกทุก N ∈ {1..10} · rollover ทุกกรณีกลุ่มว่าง/ไม่พอ · seed เดิม + input เดิม (ลำดับต่างกัน) ผลเดิม byte-equal · ×2 weight มีผลเชิงสถิติ (10,000 runs) · ไม่มีใครได้ 2 ล็อค · Ineligible ไม่ถูกจับ · waitlist ≤3 ต่อกลุ่ม · forfeit รักษาโควตา · InputHash เปลี่ยนเมื่อ category/rules เปลี่ยน · demo seed → สมศรี ใ. ได้ B2

---

## API (ทุก endpoint ภายใต้ `/api`)

```
GET    /spaces?districtId=&type=          รายการพื้นที่ + รอบที่เปิดรับ
GET    /spaces/{id}
POST   /spaces/match                      Space Matching (rule-based) ← productType, preferredArea
POST   /stall-requests                    ยื่นคำขอ
GET    /stall-requests/me                 สถานะคำขอของผู้ค้า
POST   /stall-requests/{id}/confirm
POST   /stall-requests/{id}/forfeit
POST   /stall-requests/{id}/reviews       ขอทบทวนผล
GET    /rounds/{id}/reviews               คำขอทบทวนของรอบ (portal)
GET    /rounds/{id}/insights              Allocation Insight — ธงอ่านอย่างเดียวของรอบ (portal)
POST   /reviews/{id}/respond              ตอบคำขอทบทวน Upheld | Corrected
POST   /rounds/{id}/eligibility           ตรวจสิทธิทั้งรอบ (portal)
POST   /rounds/{id}/allocation-runs       รันจัดสรร → Draft
POST   /allocation-runs/{id}/approve
POST   /allocation-runs/{id}/reject
GET    /allocation-runs/{id}              ผล + เหตุผลรายคน + ref/seed
POST   /check-ins                         QR check-in
GET    /dashboard/kpis?districtId=        Occupancy · LeadTime · NewVendors · FairRotation
POST   /demo/reset                        (Development only) คืนสภาพ seed + Demo:Now
POST   /demo/clock                        (Development only) เลื่อนเวลา demo
GET    /audit?entityType=&entityId=
POST   /assistant/ask                     Vendor Assistant
```

KPI:
- **Occupancy** = ล็อคที่มีผู้ได้สิทธิ์ยืนยันแล้ว ÷ ล็อคทั้งหมด
- **LeadTime** = median วัน จากปิดรับคำขอ → Approve run
- **NewVendors** = % ผู้ได้สิทธิ์ที่เป็นกลุ่ม New ในรอบล่าสุด
- **FairRotation** = % ผู้ได้สิทธิ์รอบนี้ที่ไม่ได้สิทธิ์รอบก่อน (ผู้ได้สิทธิ์หน้าใหม่ + ผู้ได้น้ำหนัก ×2)
- แยกกราฟ: สัดส่วนผู้ได้สิทธิ์จริงเทียบโควตา

Auth: mock — header `X-Demo-Role: vendor|officer|executive` + `X-Demo-User` · web มี role switcher มุมบนขวา

---

## AI components

**Vendor Assistant** (`/assistant/ask`)
- Context = ไฟล์ทั้งหมดใน `docs/rules/` ใส่ใน system prompt (context stuffing — ไม่ต้องมี vector DB สำหรับ prototype)
- ตอบภาษาไทย สั้น ≤ 4 บรรทัด · อ้างชื่อไฟล์/หัวข้อที่ใช้ตอบ · ไม่พบในเอกสาร → "ไม่พบในระเบียบที่ประกาศ กรุณาติดต่อสำนักงานเขตบางพลัด"
- ห้ามตอบเรื่องผลการจัดสรรของบุคคล · ห้ามรับปากเรื่องสิทธิ
- ไม่มี `ANTHROPIC_API_KEY` → fallback: keyword match กับหัวข้อใน `docs/rules/` (ทุก `##` ต้องมีบรรทัด `<!-- keywords: ... -->` ใต้หัวข้อ · เนื้อหาไม่เกิน 3 บรรทัดเพื่อให้คำตอบ ≤ 4 บรรทัดรวมอ้างอิง)

**Space Matching** — rule-based score: ประเภทสินค้าตรง (40) · มีล็อคว่าง (30) · ระยะทาง/ย่านที่เลือก (20) · ช่วงเวลาตรง (10) · คืน top 3 พร้อมเหตุผลเป็นข้อความ

**Allocation Insight** (Phase 2 — ทำถ้ามีเวลา) — rule-based flags: เช็คอินนอก geofence · ล็อคมีผู้ถือสิทธิ์แต่ไม่เช็คอิน N วัน · ผู้ได้สิทธิ์กระจุกนามสกุล/เบอร์เดียวกัน

---

## Seed data (mock — `Infrastructure/Seed`)

- District: บางพลัด
- Spaces
  - ปากซอยจรัญฯ 79 · Relaxation · 4 ล็อค · อาหาร
  - หน้า MRT บางพลัด ทางออก 1 · Relaxation · 5 ล็อค · อาหาร/เครื่องดื่ม
  - ตลาดนัดเขตบางพลัด · DistrictMarket · 20 ล็อค · ทั่วไป
  - ลานใต้สะพานพระราม 8 · TrialArea · 6 ล็อค · ทั่วไป
- Round ปัจจุบัน: 1 พ.ย. 2569 – 30 เม.ย. 2570 · ปิดรับ 31 ต.ค. 2569
- ปากซอยจรัญฯ 79 ผู้สมัครรวม 11 คน — **seed ไว้ 10 คน** สมศรี ใ. ยื่นสดใน demo ข้อ 2 เป็นคนที่ 11:
  Legacy: สมศรี ใ. (ข้าวแกง · ยื่นสด), บุญมี ร. · Vulnerable: มาลี ส. (ผู้สูงอายุ), อนุชา ก. (ผู้พิการ), วรรณา ท. (ผู้สูงอายุ) · New: ธนพล จ., ปิยะ น., สุดา พ., กิตติ ว., ศิริ ม., ชัย ด.
- ตรวจสิทธิใน demo: ชัย ด. ยังไม่ผ่านอบรมสุขาภิบาลอาหาร → Ineligible · ปิยะ น. ไม่ได้สิทธิ์ที่จรัญฯ 79 รอบก่อน → น้ำหนัก ×2
- `VendorId` ของ seed ทุกคน (รวมสมศรี) เป็นค่าคงที่ — ห้ามสุ่ม เพราะ engine เรียง input ตาม `VendorId`
- เลือก seed ที่ทำให้ **สมศรี ใ. ได้ล็อค B2** บันทึกใน seed data พร้อม comment และมี test ล็อกไว้
- Phase 1 seed เฉพาะ district · space · lock · vendor · รอบปัจจุบัน
- รอบก่อน (1 พ.ค. – 31 ต.ค. 2569) ของพื้นที่อื่น สร้างใน Phase 3 ด้วย engine จริง + seed คงที่ (มี AllocationRun ตรวจย้อนได้) แล้วเติม check-in ย้อนหลัง 3 เดือนพอให้ dashboard มีตัวเลข

---

## Frontend

Design: minimal, ตัวหนังสือเป็นหลัก ไม่มี gradient/emoji
Tokens: `--ground #F3F5F2` · `--surface #FFFFFF` · `--ink #17201D` · `--muted #55625D` · `--line #D9DFDB` · `--accent #1F6F5C` · `--accent-soft #DCEBE5` · `--warn #9A5F0E`
Fonts: IBM Plex Sans Thai (body) · Bai Jamjuree (heading) · IBM Plex Mono (ตัวเลข/เลขอ้างอิง)

| Route | หน้าจอ |
|---|---|
| `/vendor` | ค้นหาพื้นที่ + Space Matching · แชท Vendor Assistant · ยื่นคำขอ · สถานะคำขอ/คิว · ผลพร้อมเหตุผลและเลขอ้างอิง · ยืนยัน/สละสิทธิ์ · เช็คอิน QR (จำลอง) · ขอทบทวนผล |
| `/portal` | รายการรอบ · ตรวจสิทธิ · รันจัดสรร → ดูผลตามโควตา → Approve/Reject · คำขอทบทวน · audit trail ต่อ entity |
| `/dashboard` | 4 KPI · ตารางรายพื้นที่ · สัดส่วนผู้ได้สิทธิ์จริงเทียบโควตา |

`/vendor` ต้องใช้งานได้ดีที่ 390px · `/portal` และ `/dashboard` responsive ถึง 1280px+

---

## Conventions

- C#: file-scoped namespace, `sealed` by default, records สำหรับ DTO, `Result<T>` แทน exception สำหรับ business error
- ไม่มี business logic ใน endpoint หรือ EF configuration
- Angular: standalone + signals, `inject()`, ไม่มี NgModule, lazy route ต่อ area
- Commit เล็ก ข้อความภาษาอังกฤษแบบ conventional commits
- ทุก phase จบด้วย `dotnet test` และ `ng build` ผ่าน

## Out of scope

ชำระเงิน · LINE/LIFF จริง · OTP/ยืนยันตัวตนจริง · เชื่อม ThaiMarket/BMAOSS/Traffy จริง (ใช้ seed JSON) · credit score (Phase 3)

## Demo script (Definition of Done)

1. `/vendor` ป้าสมศรีถาม Assistant ว่าต้องใช้เอกสารอะไร → ได้คำตอบพร้อมอ้างอิง
2. Space Matching แนะนำ ปากซอยจรัญฯ 79 → ยื่นคำขอ → สถานะ "ยื่นคำขอแล้ว"
3. สลับ role → `/portal` ตรวจสิทธิ → รันจัดสรร → เห็นผลตามโควตา 2/1/1 + ref + seed → Approve
4. กลับ `/vendor` → เห็น "ได้ล็อค B2" พร้อมเหตุผล → ยืนยันสิทธิ์ → เช็คอิน
5. `/portal` เปิด audit trail ของรอบนี้ เห็นทุกขั้น
6. `/dashboard` เห็น KPI อัปเดต
7. `docker compose up` จากเครื่องเปล่า → ทั้งหมดใช้งานได้ภายใน 1 คำสั่ง

---

## Agent skills

### Issue tracker

Issues are tracked in this repo's GitHub Issues via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Uses the five default labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
