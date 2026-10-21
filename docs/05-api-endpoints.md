# API Endpoints — JobSeekerNet (.NET 10)

Dokumen ini adalah **spesifikasi kontrak endpoint** yang akan kamu implementasikan
manual di C#. Setiap endpoint diturunkan dari perilaku project Laravel
(`jobSeeker/`), dengan penyesuaian idiomatic .NET:

- **Semua endpoint bersifat JSON API** (Laravel sumber = MVC Blade + form; di .NET kita
  bikin REST API murni, karena frontend nanti dipisah).
- Auth = **JWT** (sesuai keputusan 2026-10-12 di handoff `dotnet-rewrite-learning.md`;
  Laravel memakai Sanctum/sesion — di .NET kita ganti ke JWT karena lebih natural
  untuk API).
- Setiap endpoint yang mengubah data **wajib menulis `ApplicationLog`** persis seperti
  di Laravel (`created`, `status_changed`, `follow_up_set`, `interview_added`,
  `bulk`, dsb). Jangan lewati ini — audit log adalah fitur, bukan dekorasi.
- `tags` di Laravel disimpan sebagai array JSON di DB. Di .NET kita simpan
  **string** (`"remote,finance,part-time"`) karena EF Core + SQL Server tidak
  support kolom JSON array secara native; `normalizeTags` di Laravel kita
  terjemahkan jadi helper `string → string` (lowercase, trim, unique, join `,`).
- Semua datetime = **DateTime Utc** (Laravel cast `date`; SQL Server + EF Core
  lebih aman dengan UTC, render lokal di frontend).

---

## Konvensi umum

| Item | Nilai |
|---|---|
| Base URL | `https://localhost:5001/api` (atau setara) |
| Content-Type | `application/json` (kecuali file upload/download) |
| Auth header | `Authorization: Bearer <jwt>` |
| Response sukses (write) | `201 Created` + body JSON |
| Response sukses (update/delete) | `204 No Content` |
| Validasi gagal | `400` + `{"errors": { field: ["pesan"] }}` |
| Token invalid / belum login | `401` |
| Resource bukan milik user | `403` |
| Resource tidak ada | `404` |

**Status yang valid** (`Application.StatUSES`):
`Wishlist`, `Applied`, `Assessment`, `HR Interview`, `User Interview`,
`Offered`, `Rejected`, `Ghosted`

**Status "aktif"** (untuk query `?active=true`): `Wishlist`, `Applied`,
`Assessment`, `HR Interview`, `User Interview`

**Stale** (ditandai `stale: true` di response): aplikasi aktif yang tidak bergerak —
stage awal (Wishlist/Applied/Assessment) idle > 14 hari, atau stage interview
(HR/User Interview) idle > 7 hari. Hitung dari `applicationDate` vs. `now()`.

---

## 1. Auth

### `POST /api/auth/register`
```json
// request
{ "name": "Budi", "email": "budi@example.com", "password": "rahasia123" }

// 201
{ "id": 1, "name": "Budi", "email": "budi@example.com" }
```
Validasi: `name` required max 255; `email` required, valid email, unik;
`password` required min 8.

### `POST /api/auth/login`
```json
// request
{ "email": "budi@example.com", "password": "rahasia123" }

// 200
{ "token": "<jwt>", "user": { "id": 1, "name": "Budi", "email": "..." } }

// 401
{ "message": "Email atau password salah." }
```

### `POST /api/auth/logout`
Stateless (JWT) → `204`. (Opsional: blacklist token di cache; untuk
versi belajar, cukup kembalikan 204 tanpa tindakan.)

### `GET /api/auth/me` → `200` profil user + `401`
```json
{ "id": 1, "name": "Budi", "email": "budi@example.com", "weeklyTarget": 3,
  "applications": 12, "interviewsUpcoming": 2 }
```
(Statistik ringkas dari Laravel `DashboardController` bisa ditaruh di sini
atau di endpoint dashboard terpisah — pilih salah satu, jangan duplikat.)

---

## 2. Applications

Semua endpoint di grup ini otomatis **filter `userId` = user login** (mirror
dari `where('user_id', $request->user()->id)` di Laravel). Endpoint yang
menerima `{id}` harus cek ownership → `403` kalau bukan miliknya (mirror
`abort_unless(..., 403)`).

### `GET /api/applications`
Query params (opsional):
| Param | Tipe | Catatan |
|---|---|---|
| `status` | string | salah satu dari STATUSES |
| `search` | string | LIKE company/title |
| `tags` | string | koma-dipisah: `remote,finance` → AND (semua harus ada) |
| `sort` | string | `newest` (default) / `oldest` / `company` |
| `staleOnly` | bool | hanya yang stale |
| `page`, `pageSize` | int | pagination, default `page=1`, `pageSize=10` |

```json
// 200
{
  "page": 1, "pageSize": 10, "totalPages": 3, "totalItems": 27,
  "items": [
    {
      "id": 1, "company": "PT ACME", "title": "Backend Dev",
      "status": "Applied", "jobPostingLink": "https://...", "contact": "hr@acme.co",
      "applicationDate": "2026-10-01", "salaryRange": "12jt-15jt",
      "notes": "...", "nextFollowUp": "2026-10-25", "followUpNotes": "...",
      "tags": ["remote", "finance"],
      "stale": false,
      "latestInterview": { "stage": "HR Interview", "scheduleDate": "2026-10-20", "...": "..." },
      "documentsCount": 2
    }
  ]
}
```
Mirror perilaku Laravel: `with(['interviews' => latest 1 desc by date, 'documents'])`,
`allTags` (daftar tag unik milik user) bisa di-expose di `GET /api/applications/tags`
(separasi lebih bersih).

### `GET /api/applications/tags` → `200` daftar tag unik milik user
```json
{ "tags": ["finance", "remote", "part-time"] }
```

### `GET /api/applications/{id}` → `200` / `403` / `404`
Full detail termasuk `interviews` (urut `scheduleDate` desc), `documents`,
`logs` (max 20 terbaru).

### `GET /api/applications/export` → `200` `text/csv`
Stream CSV, kolom: Company, Role, Status, Salary Range, Application Date,
Contact, Job Posting Link, Notes. Limit 5000 baris. Header
`Content-Disposition: attachment; filename="applications-{yyyyMMddHHmm}.csv"`.

### `POST /api/applications` → `201`
```json
// request (mirror ApplicationRequest rules)
{
  "company": "PT ACME",              // required, max 255
  "title": "Backend Dev",            // required, max 255
  "status": "Applied",               // required, in STATUSES
  "jobPostingLink": "https://...",   // optional, harus valid URL, max 2048
  "contact": "hr@acme.co",           // optional, max 255
  "applicationDate": "2026-10-01",   // optional, date
  "salaryRange": "12jt-15jt",        // optional, max 255
  "notes": "...",                    // optional
  "nextFollowUp": "2026-10-25",      // optional, date
  "followUpNotes": "...",            // optional, max 2000
  "tags": "remote, finance"          // optional, string koma-dipisah (normalize → lowercase/trim/unique)
}
```
Sisi server: `user_id` diisi dari JWT (jangan percaya dari client).
Tulis log: `created`.

### `PUT /api/applications/{id}` → `204` / `403`
Body sama dengan POST (semua field boleh di-patch, tapi endpoint tunggal
untuk simplicity belajar; kalau mau PATCH terpisah, validasi `status` tetap
diperlukan kalau disertakan).
Tulis log kalau:
- `status` berubah → `status_changed` (old → new)
- `nextFollowUp` berubah → `follow_up_set` (old → new)

### `DELETE /api/applications/{id}` → `204` / `403`
Hapus aplikasi **dan semua interviews terkait** (cascade, mirror
`$application->interviews()->delete()` di Laravel).

### `POST /api/applications/{id}/status` → `204` / `403`
```json
{ "status": "Offered" }   // required, in STATUSES
```
Tulis log `status_changed` kalau berubah.

### `POST /api/applications/{id}/follow-up` → `204` / `403`
```json
{ "nextFollowUp": "2026-10-25", "followUpNotes": "..." }   // keduanya optional
```
Tulis log `follow_up_set` kalau `nextFollowUp` berubah.

### `GET /api/applications/{id}/draft-follow-up` → `200`
Mirror `draftFollowUp` Laravel: bangun `to/subject/body` berdasarkan
`status`, `applicationDate` (kalau ≥ 14 hari → versi "check in" panjang),
`contact` (default "Hiring Team"), `name` user dari JWT.
```json
{ "to": "hr@acme.co", "subject": "Following up: Backend Dev application at PT ACME",
  "body": "I hope this message finds you well. ..." }
```

### `POST /api/applications/bulk` → `200`
```json
// request
{ "action": "set_status", "status": "Rejected", "ids": [1, 2, 3] }
// ATAU
{ "action": "delete", "ids": [1, 2, 3] }

// 200
{ "affected": 3, "message": "Status of 3 application(s) updated to Rejected." }
```
Validasi: `action` in `set_status,delete`; `status` wajib & in STATUSES
kalau `action=set_status`; `ids` array int non-kosong.
Sisi server filter `ids` dengan `userId` login (mirror
`where('user_id', user).whereIn('id', ids)`).
Tulis log `status_changed` (note=`bulk`) per aplikasi yang berubah status.

---

## 3. Interviews

Nested di bawah application, mirror `routes/web.php`:

### `POST /api/applications/{applicationId}/interviews` → `201`
Cek: `applicationId` milik user (`403` kalau bukan).
```json
// request (mirror InterviewRequest rules)
{
  "interviewStage": "HR Interview",   // required, in STAGES
  "interviewer": "Pak Andi",          // optional, max 255
  "scheduleDate": "2026-10-22",       // optional, date
  "linkMeeting": "https://meet...",   // optional, valid URL, max 2048
  "notes": "..."                      // optional
}
```
Tulis log di application: `interview_added` (newValue = interviewStage).

### `PUT /api/applications/{applicationId}/interviews/{interviewId}` → `204` / `403`
Cek ownership application **dan** `interview.application_id == applicationId`
(kedua-duanya `403`). Body sama dengan POST.

### `DELETE /api/applications/{applicationId}/interviews/{interviewId}` → `204` / `403`
Cek ownership application + interview milik application yang benar.

### `GET /api/interviews` → `200`
Mirror `InterviewController.index`: semua interview user, di-group:
```json
{
  "upcoming": [ ... ],      // scheduleDate > now, urut asc
  "past": [ ... ],          // scheduleDate <= now, urut desc
  "unscheduled": [ ... ]    // scheduleDate null
}
```
Setiap item: `{ "id", "applicationId", "company", "title", "interviewStage",
"interviewer", "scheduleDate", "linkMeeting", "notes" }`
(`company`/`title` dari relation application, eager load).

### `GET /api/interviews/calendar?year=2026&month=10` → `200`
Mirror `InterviewController.calendar`: interview dengan `scheduleDate` dalam
rentang bulan, di-group per tanggal:
```json
{
  "year": 2026, "month": 10,
  "days": {
    "2026-10-20": [ { "id": 5, "company": "PT ACME", "interviewStage": "HR Interview", "time": "14:00" } ],
    "2026-10-25": [ ... ]
  }
}
```
Default `year`/`month` = bulan & tahun berjalan kalau tidak disertakan.

**STAGES** (`Interview.STAGES`): `Screening`, `HR Interview`,
`Technical Test`, `User Interview`, `Final/C-Level Interview`

---

## 4. Documents

### `GET /api/documents` → `200`
Mirror `DocumentController.index`: dokumen user, pagination
(`page=1`, `pageSize=12`), filter opsional `?type=`.
```json
{
  "items": [
    { "id": 1, "fileName": "cv_budi.pdf", "type": "CV",
      "applicationId": 1, "applicationCompany": "PT ACME",
      "applicationTitle": "Backend Dev", "uploadedAt": "2026-10-15" }
  ]
}
```

### `POST /api/documents` → `201`
`multipart/form-data`:
| Field | Tipe | Aturan |
|---|---|---|
| `file` | file | optional; format: pdf, doc, docx, ppt, pptx, xls, xlsx, txt, png, jpg, jpeg, zip; max 5 MB |
| `type` | string | **required**, in `CV, Portfolio, Cover Letter, Other` |
| `applicationId` | int | optional, harus exist & milik user |

Server: simpan file ke `storage/documents/` (padanan Laravel `Storage::disk('local')`),
isi `filePath` & `fileName` (nama asli dari client).

### `GET /api/documents/{id}/download` → `200` binary / `403`
Stream file, `Content-Disposition: attachment; filename="<fileName>"`.

### `DELETE /api/documents/{id}` → `204` / `403`
Hapus record **dan file di disk** (mirror `Storage::disk('local')->delete(...)`).

**Path traversal** (lihat handoff): saat simpan, selalu
`Path.GetFullPath` + cek `StartsWith(rootDirectory)` sebelum menulis file.

---

## 5. Profile

### `GET /api/profile` → `200`
```json
{ "id": 1, "name": "Budi", "email": "budi@example.com", "weeklyTarget": 3 }
```

### `PATCH /api/profile` → `204` / `400`
```json
{ "name": "Budi S.", "email": "budi2@example.com", "weeklyTarget": 5 }
```
Validasi (mirror `ProfileUpdateRequest`): `name` required max 255;
`email` required, valid, lowercase, unik (abaikan diri sendiri);
`weeklyTarget` optional int 1–100.
Catatan: di Laravel kalau email berubah → `email_verified_at = null`.
Kalau kamu implementasikan email verification di .NET (belum ada di scope
belajar saat ini), terapkan logika yang sama; kalau belum, lewati
field ini dan beri catatan.

### `DELETE /api/profile` → `204` / `400`
```json
{ "password": "rahasia123" }   // required, harus cocok dengan password user
```
Hapus user + cascade (applications, interviews, documents) — sama seperti
`ProfileController.destroy` Laravel (logout + hapus user).

---

## 6. Dashboard (opsional, sesuai `DashboardController` Laravel)

Kalau mau satu endpoint agregat untuk dashboard frontend:

### `GET /api/dashboard` → `200`
Kumpulkan (semua filter per user login):
```json
{
  "totalApplications": 27,
  "byStatus": { "Wishlist": 4, "Applied": 10, "Offered": 2, "...": "..." },
  "activeCount": 20,
  "staleCount": 5,
  "upcomingInterviews": 2,
  "recentApplications": [ ... ]   // 5 terbaru
}
```
(Ini ringkasan dari `DashboardController` Laravel — cek file-nya untuk angka
yang pasti di-servisinya; kalau di Laravel masih placeholder, endpoint ini
juga boleh placeholder dulu.)

---

## Urutan implementasi yang disarankan (ikuti aturan "jangan lompat fase")

1. **Auth** (register, login, me) — prasyarat semua yang lain.
2. **Applications CRUD** (index, show, store, update, destroy) — inti.
3. **Applications ops** (status, follow-up, draft, bulk, tags, export).
4. **Interviews** (nested CRUD + overview + calendar).
5. **Documents** (upload + download + delete).
6. **Profile** (+ delete).
7. **Dashboard** (last, karena agregat dari data yang sudah ada).

Setelah tiap grup selesai: `dotnet build` hijau + minimal 1 unit test
untuk logika pure-nya (mis. `normalizeTags`, hitung stale, pilih template
draft follow-up) sebelum lanjut grup berikutnya. Ini aturan di handoff —
jangan skip.
