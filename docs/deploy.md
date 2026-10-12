# ขึ้นเว็บให้ทีมลองเล่น (ฟรี)

แยก 3 ส่วนไปไว้บนบริการฟรี 3 ที่ ไม่ต้องแก้โค้ดแอป

| ส่วน | บริการ | ไฟล์ใน repo |
|---|---|---|
| ฐานข้อมูล PostgreSQL | Neon (Free) | — |
| API (.NET) | Render Web Service (Free, Docker) | `render.yaml` · `deploy/api.Dockerfile` |
| เว็บ (Angular) | Netlify (Free) | `netlify.toml` |

`docker compose up` ในเครื่องยังใช้ไฟล์เดิมทุกอย่าง ไฟล์ข้างบนใช้เฉพาะตอนขึ้น host

## 1. Neon — ฐานข้อมูล

1. สมัคร https://neon.tech แล้วสร้าง Project เลือก region **AWS Asia Pacific (Singapore)**
2. ที่ Connection Details เลือกแบบ **.NET** แล้วคัดลอก connection string รูปแบบนี้

   ```
   Host=ep-xxxx.ap-southeast-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=xxxx;SSL Mode=Require
   ```

   ถ้าได้แบบ `postgresql://…` ให้แปลงเป็นรูปแบบข้างบน และต้องมี `SSL Mode=Require`

ไม่ต้องสร้างตารางเอง API สร้างตารางและ seed ข้อมูลจำลองให้ตอนเริ่มครั้งแรก

## 2. Render — API

1. สมัคร https://render.com ด้วยบัญชี GitHub ที่เข้าถึง repo นี้ได้
2. **New → Blueprint** เลือก repo `bkk-smart-market` Render จะอ่าน `render.yaml` แล้วสร้าง service ชื่อ `bkk-smart-market-api`
3. ใส่ค่าที่ Render ถาม
   - `ConnectionStrings__SmartMarket` = connection string จาก Neon
   - `ANTHROPIC_API_KEY` = เว้นว่างได้ (Assistant จะตอบจากหัวข้อระเบียบ)
   - `ANTHROPIC_MODEL` = เว้นว่างได้
4. รอ build เสร็จ (ครั้งแรกราว 5–10 นาที) แล้วเปิด `https://bkk-smart-market-api.onrender.com/api/spaces` ต้องเห็นรายการพื้นที่เป็น JSON

ถ้า URL ที่ Render ให้มาไม่ใช่ `bkk-smart-market-api.onrender.com` (ชื่อซ้ำกับคนอื่น) ให้แก้ URL ใน `netlify.toml` ทั้ง 4 จุดให้ตรง แล้ว push ก่อนทำขั้นที่ 3

## 3. Netlify — เว็บ

1. สมัคร https://netlify.com ด้วย GitHub
2. **Add new site → Import an existing project** เลือก repo `bkk-smart-market`
3. Netlify อ่าน `netlify.toml` เอง (base `web` · build `npx ng build` · publish `dist/web/browser`) กด **Deploy** ได้เลย
4. ได้ URL เช่น `https://ชื่อ.netlify.app` เปลี่ยนชื่อได้ที่ Site configuration → Change site name
5. ส่ง URL นี้ให้ทีม ทุกหน้าใช้ได้ (`/vendor` · `/portal` · `/dashboard` · `/docs`)

## 4. (แนะนำ) กัน API หลับ

Render แบบฟรีจะหลับเมื่อไม่มีคนเรียก 15 นาที ตื่นใหม่ใช้ราว 1 นาที คนแรกที่เปิดอาจเห็น "โหลดไม่สำเร็จ" ให้กด "ลองอีกครั้ง"

กันไม่ให้หลับ: สมัคร https://cron-job.org (ฟรี) ตั้งให้เรียก `https://bkk-smart-market-api.onrender.com/api/spaces` ทุก 10 นาที
service เดียวเปิดทั้งเดือนใช้ไม่เกินโควตาฟรีของ Render (750 ชั่วโมง/เดือน)

## ต้องรู้ก่อนส่งลิงก์ให้ทีม

- **ทุกคนเล่นข้อมูลชุดเดียวกัน** — ยื่นคำขอ รันจัดสรร หรือกด reset demo ที่ portal คนอื่นเห็นผลทันที ควรนัดลำดับการเล่น
- **ใครมีลิงก์ก็เข้าได้ทุกบทบาท** — auth เป็น mock (header `X-Demo-Role`) และรันโหมด `Development` เพื่อให้ปุ่มเลื่อนเวลาและ reset ใช้ได้ ข้อมูลทั้งหมดเป็นข้อมูลจำลอง
- **อย่าใส่ข้อมูลจริง** — ระบบนี้เป็นต้นแบบ ไม่มีการยืนยันตัวตนจริง
- **คืนสภาพข้อมูล** — portal → ปุ่ม "เวลา demo" มุมขวาบน → reset

## อัปเดตเวอร์ชัน

push เข้า `main` แล้ว Render กับ Netlify จะ build และ deploy ใหม่ให้เอง
