# Chem Classify (placeholder name)

This document is the working description of ChemClassify layers, Firebase data layout, existing code, and the first delivery pipeline.

**Stack:** Godot 4.7.2 · C# / .NET · Firebase

---

## Product layers

ChemClassify has three layers:

1. **Admin (desktop only, lab managers)**  
   Authenticated desktop app. Upload SDS PDFs, maintain chemical metadata (including expiration dates), list/query Firebase records, and later generate flask stickers with QR codes. Admin tools must not ship in the mobile build.

2. **Field user (mobile, lab assistants)**  
   Authenticated mobile app. QR scan and search to look up chemical / SDS records. Read-only: no admin registration tools. After lookup, open the SDS PDF via the **operating system** viewer (URL from Firebase), not an in-app PDF engine.

3. **Notification server (not necessarily Godot)**  
   Process that queries stored expiration dates and notifies admins (email via existing mail tooling). It must read **structured Firestore fields**, never open or parse PDF binaries.

### Data the product holds

| Kind | Role |
| --- | --- |
| SDS PDF | Full safety data sheet bytes (source of truth for document content) |
| Stable SDS id | Links sticker QR, Firestore metadata, and Storage object |
| Expiration date | Structured field for queries and mail alerts — not mined from PDFs |
| Optional summary fields | Name, CAS, short hazard line for mobile cards / search |

QR codes are intended for stickers on chemical flasks. **QR payload = SDS id only.** Interpreting that id stays inside our authenticated app (safety / access control). Scanning outside the app yields a meaningless id string, not a public PDF link.

---

## PDF viewing policy (hard rule)

- **Do not** install PDF viewer addons, WebView + PDF.js, PDFium, MuPDF, CEF, or any in-Godot PDF renderer.
- **Do not** rebuild SDS pages as Godot widgets.
- ChemClassify is a **Firebase talking layer**: auth, metadata CRUD, Storage upload/download URL resolution, QR id foundations, expiration queries.
- Whenever a user must **read** an SDS, resolve an HTTPS URL for the Storage object and open it with the **OS** (`OS.shell_open` / platform equivalent). Desktop and mobile alike.

In-app PDF preview is **out of scope**.

---

## 1. Firebase setup — where data lives

Admin registration creates **one logical SDS** as two coordinated writes sharing the same id. The notification server and mobile clients never reinterpret PDF contents for routine queries.

### IDs (Firestore document key)

- Collection: `sds`
- Document id = stable `sdsId` (e.g. auto-generated id or `sds_…` prefix)
- This id is what QR codes will encode later
- Clients look up with a direct get: `sds/{sdsId}` — no PDF parsing

### PDF bytes (Firebase Storage)

Canonical object path:

```text
gs://<bucket>/sds/{sdsId}/current.pdf
```

| Rule | Detail |
| --- | --- |
| One current object per id | Replace overwrites `current.pdf` (versioning optional later) |
| Original filename | Kept in Firestore `fileName`, not as the Storage object name |
| Access | Auth-gated rules; prefer short-lived download/signed URLs later |

Optional later: `sds/{sdsId}/versions/{timestamp}.pdf`.

### Metadata and dates (Firestore `sds/{sdsId}`)

Store queryable fields **explicitly** at registration time. The mail/notification server filters on timestamps and flags — it must **not** open hundreds of PDFs to find expiration dates.

| Field | Type | Purpose |
| --- | --- | --- |
| `sdsId` | string | Same as doc id; echo for clients |
| `displayName` | string | Chemical / product name |
| `casNumber` | string? | Optional summary / search |
| `hazardSummary` | string? | One short line for mobile card |
| `storagePath` | string | e.g. `sds/{sdsId}/current.pdf` |
| `fileName` | string | Original upload name |
| `contentType` | string | `application/pdf` |
| `expiresAt` | timestamp | Chemical expiration — **required for mail queries** |
| `createdAt` | timestamp | Audit |
| `updatedAt` | timestamp | Audit |
| `createdBy` | string? | Admin uid |
| `active` | bool | Soft-delete / hide from field |

Optional later: GHS codes, location, batch, version, checksum.

### Security rules (MVP intent)

- **Read:** authenticated users (admin + field)
- **Write (Firestore + Storage):** admin only (custom claim or allowlist for MVP)
- Soft-delete via `active: false` while stickers may still exist

### Query model for the notification server

```text
Query Firestore where active == true AND expiresAt <= threshold
  → build MailData → MailService.SendMail
```

No Storage downloads, no PDF text extraction, no “reinterpret SDS” step.

### QR foundation (not in first pipeline)

- Encode **only** `sdsId` (raw string). No Storage URLs in the QR.
- Sticker layout / automatic print generation is a **separate future service**.
- Deep links (`chemclassify://…`) or public HTTPS landings are optional later; MVP safety preference is id-only so the app is required to interpret.

---

## 2. Implementation pieces we already have

| Piece | Path | Status / role |
| --- | --- | --- |
| Firebase Admin init | `FirebaseSystems/FirebaseConnect.cs` | Creates default `FirebaseApp` with ADC |
| Email/password auth | `FirebaseSystems/FirebaseAuthenticate.cs` | Sign-in, sign-up, password change/reset via Identity Toolkit |
| Misleading stub | `FirebaseSystems/FirebaseRead.cs` | Commented Cloud Function sketch; **name and contents are wrong for the product**. Repurpose into a real SDS Firebase layer (Firestore metadata + Storage PDF), not a generic “read” placeholder |
| SMTP mail | `Systems/ServiceSystems/MailService/MailService.cs` + `MailData.cs` | Can send plain-text mail when SMTP env vars are set; ready to be driven by expiration queries later |
| PDF register UI shell | `SystemsUI/ApplicationUI/FirebasePDFRegister.tscn` | Empty panel under main app theme — target surface for first admin pipeline |
| Expiration card UI shell | `SystemsUI/ApplicationUI/ChemicalExpirationCard.tscn` | UI placeholder for expiration-related presentation |
| UI navigation | `Systems/ServiceSystems/UIServices/*` | Screen swap / bindings used to reach app panels from login |

**Gap:** there is no working Firestore/Storage SDS API yet. Auth works; data spine and registration write path do not.

### Repurpose `FirebaseRead.cs`

Treat the file as debt: rename/replace so the module matches responsibility, for example:

- Firestore: get / list / upsert / soft-delete SDS metadata (including `expiresAt`)
- Storage: upload `current.pdf`, resolve open URL, delete object
- Keep platform viewers out of this layer — return URLs/records only; UI or a tiny helper calls `OS.shell_open`

Mirror existing `FirebaseSystems/` + `Systems/` style; avoid inventing an in-app PDF preview service.

---

## 3. First working pipeline (no QR yet)

**Goal:** one end-to-end admin path that proves Firebase as the talking layer.

```text
Admin signs in
  → opens main app → FirebasePDFRegister panel
  → picks a PDF from the OS file dialog
  → enters / confirms display fields + expiration date
  → system creates sdsId
  → uploads PDF to Storage sds/{sdsId}/current.pdf
  → writes Firestore sds/{sdsId} (metadata + expiresAt + storagePath)
  → success feedback (id visible for later QR work)
```

### In scope for this pipeline

1. Wire `FirebasePDFRegister.tscn` into the main app navigation (if not already reachable).
2. OS file picker for PDF only.
3. Generate stable `sdsId`.
4. Capture **expiration date** as a first-class field at register time.
5. Upload PDF + upsert Firestore in one logical operation (handle partial failure: orphan Storage or missing metadata).
6. Repurposed Firebase SDS service replacing the `FirebaseRead` stub.
7. Auth-gated rules sufficient for a prototype admin write.

### Explicitly out of scope for this pipeline

- QR encoding, sticker layout, print automation (own future service)
- Mobile scan / summary card
- In-app PDF viewing of any kind
- Notification server job (depends on `expiresAt` existing; mail code already exists)
- PDF replace/versioning UI polish beyond “create works”

### Foundations this pipeline leaves for QR

- Stable `sdsId` on every record
- Documented rule: QR will carry id only
- Storage path convention already keyed by that id  
  Later: sticker service reads `sdsId` (+ display name) and renders QR + label; it does not re-upload the PDF.

---

## 4. Later pipelines (ordered)

1. **Admin list / open SDS** — list Firestore docs; “open PDF” → Storage URL → OS viewer.
2. **Expiration mail job** — server queries `expiresAt`, uses `MailService`.
3. **QR + sticker service** — generate sticker image/PDF with id-only QR; print/export.
4. **Mobile** — scan id → Firestore get → summary → OS open PDF; search bar on same metadata.

---

## Decision summary


| Topic | Decision |
| --- | --- |
| QR contents | SDS id only; app required to interpret |
| PDF bytes | Firebase Storage `sds/{sdsId}/current.pdf` |
| Ids + dates + summary | Firestore `sds/{sdsId}`; dates never inferred from PDF at query time |
| PDF viewing | Always OS; no viewer addons |
| App role | Firebase talking layer + admin/field UX around metadata |
| Mail | Existing `MailService`; driven by Firestore `expiresAt` |
| `FirebaseRead.cs` | Misleading stub — fully repurpose into SDS Firestore/Storage API |
| First ship slice | Admin register PDF + id + expiration via `FirebasePDFRegister` |
| Stickers / QR | Separate automatic sticker generation service later |

---

## Bottom line

Get the **data spine** right first: Storage for PDF bytes, Firestore for id + expiration + summary, auth we already have. Ship **one admin registration pipeline** that writes that spine. Keep ChemClassify free of PDF renderers. Build QR stickers and mobile scan on top of the same id once registration is solid.
