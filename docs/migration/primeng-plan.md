# แผนย้าย UI ไป PrimeNG

การตัดสินใจ: [ADR-0006](../adr/0006-primeng-replaces-no-ui-library-rule.md) · สถานะ: วางแผนแล้ว ยังไม่เริ่ม implement

เป้าหมาย: ใช้ PrimeNG เป็น UI library หลัก · ธีม minimal สม่ำเสมอทั้งแอป · ลด CSS ซ้ำ · **ไม่เปลี่ยน** business logic, API contract, validation, permission, ข้อความ guardrail และ UX flow

---

## 1. Assessment (สถานะ ณ `1ab9b64`)

| เรื่อง | ข้อเท็จจริง |
|---|---|
| Framework | Angular 21.2.25 · standalone · zoneless · signals · OnPush ทุก component · lazy route ต่อ area |
| Dependencies | `@angular/*` + rxjs เท่านั้น — ยังไม่มี UI library, icon font หรือ chart library |
| ขนาด | 8 component · ~2.8k บรรทัด (html/scss/ts) · ไม่มี frontend test (`skipTests: true`) |
| Shared | `MockBadge`, `RoleSwitcher`, `labels.ts` (label/format ไทย) — ไม่มี UI primitive กลาง |
| CSS ซ้ำ | สไตล์ `button` 4 ชุด (vendor, assistant, matching, portal) · ตาราง 2 ชุด (portal, dashboard) · pill/tag 3 แบบ (`.type/.status`, `.tag`, mock badge) · `.muted/.error/.sr-only/.small` ซ้ำหลายไฟล์ |
| Budget | `ng build` ปัจจุบันไม่มี budget warning · initial 259kB raw / 73kB transfer (warn 500kB) · `portal-home.scss` source 4.2kB ใกล้ `anyComponentStyle` 4kB (budget วัด CSS ที่ compile แล้ว) |
| Tokens | `styles.scss` `:root` — ground/surface/ink/muted/line/accent/accent-soft/warn + series-1..3 (ตรวจ CVD แล้ว) + 3 font |

### Compatibility

| Package | เวอร์ชัน | หมายเหตุ |
|---|---|---|
| `primeng` | **21.1.10** (`v21-stable`) | peer `@angular/core ^21.0.7` ✓ · ตัวล่าสุด 22.1.2 ต้อง Angular ^22.1 ✗ |
| `@angular/cdk` | ^21.2 | peer ของ primeng 21 |
| `@primeuix/themes` | **^2.0.3** | preset Aura + `definePreset` · 2.x ใช้ `@primeuix/styled ^0.7` ตรงกับ primeng 21 — **ห้ามใช้ 3.x** (styled ^1 คู่กับ primeng 22) |
| `chart.js` | 4.5.1 | สำหรับ `p-chart` (dashboard เท่านั้น) |
| `chartjs-plugin-annotation` | 3.1.0 | peer `chart.js >=4` · เส้นโควตา |
| `@playwright/test` | ล่าสุด | devDependency · e2e (M0) |

primeng 21 ไม่ต้องใช้ `@angular/animations` และ icon ภายในเป็น SVG → **ไม่ลง primeicons** (ดีไซน์ไม่มี icon)

---

## 2. Component mapping

Keep = คงไว้ (เปลี่ยนแค่ token) · Replace = แทนด้วย PrimeNG · Refactor = ปรับโครงแต่คงหน้าที่

### App shell

| ปัจจุบัน | → | การตัดสินใจ | Complexity / ความเสี่ยง |
|---|---|---|---|
| header + nav `routerLinkActive` | Keep | `p-menubar` จะเปลี่ยนเป็น hamburger บนมือถือ = เปลี่ยน UX | ต่ำ |
| `MockBadge` | Refactor → ใช้ `p-tag` ภายใน, คง component ไว้ | Guardrail 5 ต้องอยู่ทุกหน้า — component เป็นจุดเดียวที่รับประกัน | ต่ำ |
| `RoleSwitcher` `<select>` | Replace → `p-select` | **ระวัง bug เดิม** (reload แล้วแสดง role แรก) — bind ค่าจาก `session.role()` ไม่ใช่ตั้งตอน init | กลาง |

### `/vendor`

| ปัจจุบัน | → | หมายเหตุ | Complexity |
|---|---|---|---|
| `button` (primary · `.secondary` · `.danger` · `.link-button`) | `p-button` (default · `outlined` · `severity="danger"` · `link`) · `fluid` บนการ์ด | คง label ข้อความเดิมรวม "กำลัง…" — e2e อ้างข้อความ | ต่ำ |
| `.notice` (info/error/warn) | `p-message` | คง `role="status"` · neutral → primary/accent-soft · error/warn → warn palette | ต่ำ |
| `.card` (คำขอ, พื้นที่) | `p-card` ภายใน `<li>` | คง `ul/li` semantics | ต่ำ |
| `.status`, `.type` pill | `StatusTag` (shared ใหม่) → `p-tag` | ใช้ `STATUS_LABEL` เดิม — ห้ามมีข้อความ "จองสำเร็จ" | ต่ำ |
| checkbox "จำลอง: อยู่นอกพื้นที่" | `p-checkbox` binary | | ต่ำ |
| textarea ขอทบทวน | `pTextarea` | คง `maxlength`, label `for` | ต่ำ |
| `<details>` ผลจัดสรรทุกครั้ง | Keep (native) | | – |
| `dl` ข้อมูลพื้นที่, `.won`, `.deadline` | Keep | typographic, เป็นเนื้อหา domain | – |
| Assistant: bubble ข้อความ | Keep | PrimeNG ไม่มี chat component | – |
| Assistant: chip คำถามแนะนำ | `p-button` rounded outlined small | `p-chip` ไม่ใช่ปุ่ม (a11y) | ต่ำ |
| Assistant/Matching: `input`, `select` | `pInputText`, `p-select` | select ช่วงเวลา: ค่าว่าง = `null` เหมือนเดิม | กลาง |
| Matching: ผลลัพธ์ + score | Keep โครง, ปุ่ม → `p-button outlined` | | ต่ำ |

### `/portal`

| ปัจจุบัน | → | หมายเหตุ | Complexity |
|---|---|---|---|
| ตาราง 4 ชุด (รอบ, คำขอ, โควตา, ผลรายคน) | `p-table` (ไม่มี paginator/sort — เท่าเดิม) | คง `[class.selected]`, `.num` ชิดขวา, `.mono`, `[class.warn]`, scroll แนวนอน | **กลาง** — มากที่สุดในแผน |
| demo clock buttons | `p-button` outlined small · reset = `severity="danger"` outlined ตอนรอยืนยัน | คง flow กดสองครั้ง | ต่ำ |
| ปุ่ม action + เงื่อนไข `[disabled]` | `p-button` | **ความเสี่ยงสูงสุด:** เงื่อนไข disabled ต้องคัดลอกตรงตัว (checking/running/canRun/rejectsLeft/isExecutive) | กลาง |
| `.tag`, `.tag.draft` | `p-tag` (secondary / warn) | | ต่ำ |
| ฟอร์มปฏิเสธผล, ตอบคำขอทบทวน | Keep inline · control → `pTextarea`, `p-radiobutton` | คง disabled ของ Corrected เมื่อไม่ใช่ Ineligible | กลาง |
| รายการคำขอทบทวน | `p-card` ต่อรายการ | | ต่ำ |
| Allocation Insight list | Keep โครง + `p-tag` | border-left ตาม severity คงไว้ | ต่ำ |
| `run-meta` dl (ref, seed, hash) | Keep | ตัวเลขอ้างอิง mono — เนื้อหาตรวจย้อน | – |
| `AuditTimeline` | `p-timeline` `align="left"` + template `opposite` = เวลา | คง `fields()`, การคลิก entity, mask `***` · ต้องเขียน CSS ยุบเป็นคอลัมน์เดียว ≤640px | กลาง |

### `/dashboard`

| ปัจจุบัน | → | หมายเหตุ | Complexity |
|---|---|---|---|
| KPI tiles | `p-card` | ตัวเลข mono คงไว้ | ต่ำ |
| กราฟโควตา (CSS bar + เส้นตั้ง) | `p-chart` horizontal bar + annotation marker ต่อกลุ่ม | สีอ่านจาก `--series-*` ตอนรันไม่ hardcode · ซ่อน legend ของ Chart.js คง legend HTML · โหลดเฉพาะ route นี้ | **สูง** |
| ตารางรายพื้นที่ | `p-table` | คงไว้เป็นทางเลือกที่อ่านได้ของกราฟ (canvas อ่านด้วย screen reader ไม่ได้) | ต่ำ |

### Global

`.mono`, `.muted`, `.sr-only`, `.small` → ย้ายเป็น utility กลางใน `styles.scss` ที่เดียว · h1–h3 font คงไว้

### Breaking risks

1. **เงื่อนไข `[disabled]`/`@if`** หลุดระหว่างย้าย template → พฤติกรรมเปลี่ยนเงียบๆ (กันด้วย e2e)
2. **390px**: padding/font ของ Aura ใหญ่กว่าเดิม → ปุ่มสองปุ่มใน `.actions` ตกบรรทัด, ตารางล้น
3. **Specificity**: style ของ PrimeNG ชนกับ CSS เดิม → ใช้ `cssLayer` และลบ CSS เดิมในคอมมิตเดียวกับที่ย้าย
4. **Bundle**: theme + components เพิ่ม initial size · chart.js ต้องไม่หลุดเข้า initial
5. **ข้อความ guardrail**: "ยื่นคำขอแล้ว" และ "ข้อมูลจำลอง" ต้องคงทุกหน้า

---

## 3. Design system

**Styled mode + preset `SmartMarketPreset = definePreset(Aura, …)`** เก็บที่ `web/src/app/shared/theme` · ลงทะเบียนผ่าน `providePrimeNG` ใน `app.config` พร้อม `darkModeSelector: false` และ `cssLayer` (app style อยู่นอก layer จึงชนะเสมอ)

### Token map (ค่าตั้งต้น — ตรวจ contrast ใน M1)

| Semantic ใน preset | ค่า | ที่มา |
|---|---|---|
| primary 50/100/200/300/400/500/**600**/700/800/900/950 | `#EEF5F2` `#DCEBE5` `#B8D7CB` `#8DBEAC` `#5C9F89` `#36836D` **`#1F6F5C`** `#1A5C4C` `#164A3E` `#123B32` `#0A231D` | `--accent` = 600, `--accent-soft` = 100 |
| primary.color / hover / active | 600 / 700 / 800 | |
| surface 0 / 50 / 200 / 500 / 900 | `#FFFFFF` `#F3F5F2` `#D9DFDB` `#55625D` `#17201D` (ช่วงอื่น interpolate) | surface / ground / line / muted / ink |
| text color / muted | `#17201D` / `#55625D` | ink / muted |
| warn + danger severity (button, tag, message) | สเกลจาก `#9A5F0E` | `--warn` — เดิมใช้สีเดียวทั้ง error, deadline และ destructive |
| border radius sm / md / lg | 4px / 6px / 8px | ตามเดิม |
| shadow (form field, card, overlay) | none — ใช้ border 1px `--line` | "ไม่มีเงา" |
| focus ring | 2px solid primary, offset 2px | ตาม `:focus-visible` เดิม |
| form field padding | 8px 10px | ตาม input เดิม |
| font | inherit จาก body | Plex Sans Thai / Bai Jamjuree / Plex Mono |

**แหล่งเดียวของสี:** CSS variables เดิมใน `:root` คงอยู่และเป็นที่อ้างอิงของ preset (เขียนค่าสีที่เดียว แล้วให้ preset ใช้ `var(--…)` หรือ generate จากค่าเดียวกัน) · `--series-*` ใช้กับ chart

---

## 4. Milestones

Branch `feat/primeng-migration` จาก tag `pre-primeng` (commit ที่เพิ่มเอกสารแผนนี้) · commit ต่อ milestone · **merge เข้า main ครั้งเดียว** · ทุก milestone ต้องผ่าน gate

**Gate ทุก milestone:** `ng build` (production) ผ่าน · e2e suite ผ่าน · `dotnet test` ผ่าน (ไม่ควรเปลี่ยน) · ไม่มีไฟล์ใน `api/` เปลี่ยน · ไม่มีการแก้ `core/api.ts` contract

| # | งาน | Acceptance เพิ่มเติม |
|---|---|---|
| **M0** | Playwright e2e ตาม demo script 1–6 บน **UI เดิม** · รันกับ docker compose + `POST /demo/reset` ก่อนแต่ละ test · viewport 390 (vendor) และ 1280 (portal/dashboard) · locator ด้วย role + ข้อความไทย | ผ่านบน UI เดิม 3 ครั้งติดกันโดยไม่ flaky |
| **M1** | ติดตั้ง package · preset + `providePrimeNG` · utility CSS กลาง · ยังไม่แตะหน้าใด | หน้าตาเดิมไม่เปลี่ยน (เทียบ screenshot ด้วยตา) · บันทึก bundle size ก่อน/หลัง |
| **M2 (pilot)** | App shell (`MockBadge`, `RoleSwitcher`) + `/vendor` ทั้งหมด + `StatusTag` | ที่ 390px ไม่มี horizontal scroll · reload แล้ว role switcher แสดง role ถูก · ลบ CSS เดิมของ vendor ที่ถูกแทน · **ทบทวน Q5 (inline confirm)** |
| **M3** | `/portal` + `AuditTimeline` | `ng build` ไม่มี `anyComponentStyle` warning · disabled ทุกปุ่มตรงเดิม · 1280px ตารางไม่ล้น |
| **M4** | `/dashboard` + `p-chart` | chart.js อยู่ใน lazy chunk เท่านั้น · เส้นโควตาตรง base · ตารางยังอยู่ |
| **M5** | ลบ CSS ตาย · ตรวจ budget · เพิ่ม visual snapshot (`toHaveScreenshot`) 390/1280 ล็อกหน้าตาใหม่ · demo script 7 (`docker compose up` เครื่องเปล่า, arm64) | initial bundle ≤ 500kB (ถ้าเกิน **รายงานก่อน** ไม่ขยับ budget เอง) · merge |
| M6 (optional) | Angular 22 + PrimeNG 22 + `@primeuix/themes` 3 คู่กัน | gate เดิม + snapshot · **ตรวจ license ก่อน:** primeng 22 มี dependency `@primeui/license-manager` และ `@primeicons/angular` |

## 5. Testing

- Seam เดียว: **เบราว์เซอร์ผ่าน e2e** — ทดสอบพฤติกรรมที่ผู้ใช้เห็น (ข้อความ, ปุ่มเปิด/ปิด, สถานะ) ไม่ทดสอบ class หรือโครง DOM ของ PrimeNG
- เขียนก่อน migrate (M0) เพื่อเป็นตัววัดว่า behavior ไม่เปลี่ยน
- รัน **โดยไม่ตั้ง `ANTHROPIC_API_KEY`** ให้ Assistant ใช้ keyword fallback — คำตอบ deterministic
- docker compose รัน API เป็น `Development` อยู่แล้ว → `POST /demo/reset` และ `/demo/clock` ใช้ได้ ไม่ต้องมี profile เพิ่ม
- Guardrail assertions: ป้าย "ข้อมูลจำลอง" ทุก route · ไม่มี "จองสำเร็จ" · ชื่อแบบย่อ "สมศรี ใ." · audit แสดง `***`
- Backend: `dotnet test` เดิม (ไม่ควรมีอะไรเปลี่ยน)

## 6. Rollback

| สถานการณ์ | วิธี |
|---|---|
| ก่อน merge | ทิ้ง branch — `main` ไม่เคยถูกแตะ |
| milestone ใน branch พัง | `git revert` commit ของ milestone นั้น (commit ต่อ milestone จึงสำคัญ) |
| หลัง merge พบปัญหาก่อนสอบ | `git revert -m 1 <merge>` หรือ build/deploy จาก tag `pre-primeng` |

## 7. ทบทวนภายหลัง

- Inline confirm (forfeit / reject / reset) vs `p-confirmPopup` — ตัดสินหลัง pilot
- push tag `pre-primeng` ขึ้น GitHub หรือไม่ — ถามตอนเริ่ม M0
