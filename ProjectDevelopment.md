# ChemClassify — project development

Working description of product layers, data layout, implemented code, and delivery pipelines.

**Stack:** Godot 4.7.2 · C# / .NET 8 · Firebase (Firestore + Auth) · OneDrive (PDF bytes) · QRCoder

---

## Product layers

ChemClassify has three layers:

1. **Admin (desktop only, lab managers)**  
   Authenticated desktop app. Upload SDS PDFs to OneDrive, write metadata to Firestore, list/query records, generate QR codes and flask stickers. Admin tools must not ship in the mobile build.

2. **Field user (mobile, lab assistants)**  
   Authenticated mobile app. QR scan and search to look up chemical / SDS records. Read-only: no admin registration tools. After lookup, open the SDS PDF via the **operating system** viewer (URL from Firestore), not an in-app PDF engine.

3. **Notification server (not necessarily Godot)**  
   Process that queries stored expiration dates and notifies admins (email via existing mail tooling). Reads **structured Firestore fields** only — never opens or parses PDF binaries.

### Data the product holds

| Kind | Where | Role |
| --- | --- | --- |
| SDS PDF bytes | **OneDrive** (faculty 1 TB accounts) | Source of truth for document content |
| SDS metadata | **Firestore** `sds/{sdsId}` | Id, expiration, name, OneDrive URL, search fields |
| Stable SDS id | Firestore doc id + QR payload | Links sticker, metadata, and OneDrive file |
| Expiration date | Firestore `expiresAt` | Queries and mail alerts — never mined from PDFs |

QR codes are for stickers on chemical flasks. **QR payload = SDS id only.** Scanning outside the app yields a meaningless id string, not a public PDF link.

---

## PDF viewing policy (hard rule)

- **Do not** install PDF viewer addons, WebView + PDF.js, PDFium, MuPDF, CEF, or any in-Godot PDF renderer.
- **Do not** rebuild SDS pages as Godot widgets.
- ChemClassify is a **Firebase + OneDrive talking layer**: auth, metadata CRUD, OneDrive upload/URL resolution, QR encoding, expiration queries.
- Whenever a user must **read** an SDS, resolve the stored HTTPS URL and open it with the **OS** (`OS.shell_open`). Desktop and mobile alike.

In-app PDF preview is **out of scope**. QR codes are **not previewed in Godot** — they are written to PNG files for external print.

---

## 1. Data layout

Admin registration creates **one logical SDS** as two coordinated writes sharing the same id.

### IDs (Firestore document key)

- Collection: `sds`
- Document id = stable `sdsId` (`sds_` + 32 hex chars)
- QR codes encode this id
- Clients look up with a direct get: `sds/{sdsId}`

### PDF bytes (OneDrive — not Firebase Storage)

```text
{configured-folder}/{sdsId}.pdf
```

| Rule | Detail |
| --- | --- |
| One file per id | Named `{sdsId}.pdf` in the configured OneDrive folder |
| Original filename | Stored in Firestore `fileName`, not used as remote name |
| Access | Organization-scoped view link by default (`ONEDRIVE_LINK_SCOPE`) |

### Metadata (Firestore `sds/{sdsId}`)

| Field | Type | Purpose |
| --- | --- | --- |
| `sdsId` | string | Same as doc id |
| `displayName` | string | Chemical / product name |
| `casNumber` | string? | Optional search |
| `hazardSummary` | string? | One short line for mobile card |
| `storageProvider` | string | `"onedrive"` |
| `storagePath` | string | OneDrive path pointer |
| `oneDriveItemId` | string | Graph item id |
| `fileUrl` | string | HTTPS URL for OS open |
| `fileName` | string | Original upload name |
| `contentType` | string | `application/pdf` |
| `expiresAt` | timestamp | Chemical expiration |
| `createdAt` / `updatedAt` | timestamp | Audit |
| `createdBy` | string? | Admin uid |
| `active` | bool | Soft-delete |

Full schema and Firebase Console setup: see `FirebaseSetupPlan.md`.

### Notification query model

```text
Query Firestore where active == true AND expiresAt <= threshold
  → build MailData → Mail.SendMail
```

---

## 2. Implementation status

| Piece | Path | Status |
| --- | --- | --- |
| Firebase Admin init | `Systems/Firebase/FirebaseConnect.cs` | Done |
| Email/password auth | `Systems/Firebase/FirebaseAuthenticate.cs` | Done |
| SDS register API | `Systems/Firebase/FirebaseSDS.cs` | Done — OneDrive upload + Firestore write |
| PDF register UI + core | `Displays/Application/FirebasePDFRegister.tscn`, `Systems/PDFRegister/PDFRegisterCore.cs` | Done — wired to `FirebaseSds.RegisterAsync` |
| QR encoding | `Systems/QRGeneration/QREncoder.cs`, `QRGenerator.cs` | Done — PNG bytes / file export, no Godot preview |
| SMTP mail | `Systems/Services/Mail/Mail.cs`, `MailData.cs` | Done — ready for expiration job |
| UI navigation | `Systems/DisplaySystems/UISwapButton.cs`, `UIBindings.cs`, `AppManager` | Done |
| Main menu + admin nav | `Displays/Application/MainMenu.tscn` | Done — links to register, library, QR lookup, expiration |
| QR lookup UI shell | `Displays/Application/QRLookup.tscn` | Shell only — no scan/lookup logic yet |
| SDS library UI shell | `Displays/Application/SDSLibrary.tscn` | Shell only |
| SDS detail UI shell | `Displays/Application/SDSDetail.tscn` | Shell only |
| Expiration monitor shell | `Displays/Application/ExpirationMonitor.tscn` | Shell only |
| Sticker layout / batch QR | — | **Not started** |
| Firestore read/list API | — | **Not started** — needed for library, lookup, batch QR |
| Expiration mail job | — | **Not started** |
| Mobile scan | — | **Not started** |

---

## 3. Architecture pattern

```text
Displays/*.tscn          UI scenes (forms, buttons, labels)
      ↓
Systems/*Core.cs         Godot scripts: read UI, validate, call services
      ↓
Systems/Firebase/        FirebaseSds, FirebaseAuthenticate (backend)
Systems/QRGeneration/    QREncoder, QRGenerator (pure C#, no Godot)
```

**Rule:** OneDrive + Firestore writes live in `FirebaseSds`. UI cores delegate — they do not reimplement upload logic.

**Rule:** QR encoding is file/bytes only. No `ImageTexture`, no in-app QR preview.

---

## 4. Completed pipeline — SDS registration

```text
Admin signs in
  → Main menu → Register SDS
  → PDFRegisterCore: pick PDF, enter name + expiration (+ optional CAS/hazard)
  → FirebaseSds.RegisterAsync:
       1. Upload PDF to OneDrive as {sdsId}.pdf
       2. Write Firestore sds/{sdsId} with metadata + fileUrl
  → UI shows assigned sdsId
```

Partial failure: if Firestore write fails after OneDrive upload, an orphan PDF may exist. Error message includes the OneDrive item id in logs.

---

## 5. Completed pipeline — QR encoding (library only)

```text
Arbitrary string (typically sdsId)
  → QRGenerator.Generate(payload) → PNG bytes
  → QRGenerator.GenerateToFile(payload, path) → PNG on disk
```

Not yet wired to register UI or library. Sticker layout (name + expiration + QR composite) not yet built.

---

## 6. Next pipelines (desktop MVP, ordered)

### 6.1 Firestore read API

Add to `FirebaseSDS.cs`:

- `GetAsync(sdsId)` — single record for QR lookup / detail view
- `ListActiveAsync()` — admin library grid
- `SoftDeleteAsync(sdsId)` — set `active: false`

### 6.2 Post-register QR export

After successful register in `PDFRegisterCore`, optionally call:

```csharp
QRGenerator.GenerateToFile(result.SdsId!, stickerPath);
```

No Godot preview — open saved PNG with OS or send to printer externally.

### 6.3 Sticker generation service

New code in `Systems/QRGeneration/`:

- `StickerLayout.cs` — composite QR + `displayName` + `expiresAt` on a printable canvas
- `StickerExport.cs` — save label PNG/PDF for print
- Trigger from register success and from SDS library (per-record + batch)

### 6.4 QR for existing Firestore records

Admin SDS library → select record(s) → generate sticker PNG from stored `sdsId` + metadata. Requires 6.1 + 6.3.

### 6.5 SDS library + open PDF

Wire `SDSLibrary.tscn` to list Firestore docs. "Open SDS" → `OS.shell_open(fileUrl)`.

### 6.6 QR lookup (desktop test path)

Wire `QRLookup.tscn` manual id entry → `GetAsync` → show summary → open PDF.

### 6.7 Expiration mail job

Separate process or scheduled task: query `expiresAt`, send via `Mail.SendMail`.

### 6.8 Mobile (later, separate export)

Scan QR → `GetAsync` → summary card → OS open PDF. No admin tools in mobile build.

---

## 7. Decision summary

| Topic | Decision |
| --- | --- |
| QR contents | SDS id only; app required to interpret |
| PDF bytes | OneDrive `{folder}/{sdsId}.pdf` — not Firebase Storage |
| Metadata | Firestore `sds/{sdsId}` |
| PDF viewing | Always OS; no viewer addons |
| QR preview | Never in Godot; PNG file export only |
| Registration | `PDFRegisterCore` → `FirebaseSds.RegisterAsync` |
| QR encoding | `QRGenerator` / `QREncoder` (QRCoder NuGet) |
| Stickers | Separate layout service; desktop only |
| Firebase setup handoff | `FirebaseSetupPlan.md` for Console configuration |

---

## 8. Bottom line

The **data spine is implemented**: OneDrive for PDF bytes, Firestore for id + expiration + summary + URL, auth in place, register UI wired. **QR encoding library is implemented** but not yet connected to UI or sticker layout.

Next desktop MVP focus: Firestore read/list, sticker generation, batch QR for existing records, and library/lookup displays.
