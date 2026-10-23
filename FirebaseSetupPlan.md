# ChemClassify — Firebase setup plan (handoff for Gemini)

Use this document as the single source of truth when configuring Firebase for the ChemClassify MVP. Follow each section in order. **Do not store SDS PDF bytes in Firebase Storage** — PDFs live in Microsoft OneDrive; Firestore holds metadata and a OneDrive open URL.

---

## 1. Project context

ChemClassify is a Godot 4.7.2 desktop admin app (C# / .NET 8) plus a future mobile field app. Firebase provides:

| Service | Role |
| --- | --- |
| **Firebase Auth** | Email/password sign-in for admins and field users |
| **Cloud Firestore** | Queryable SDS metadata (id, name, expiration, OneDrive link) |
| **Firebase Admin SDK** | Desktop app writes via service account (bypasses client rules) |

Firebase Storage is **not used** for SDS PDFs in the current MVP. OneDrive holds PDF bytes via Microsoft Graph.

---

## 2. Enable Firebase services

### 2.1 Firebase Authentication

1. Open [Firebase Console](https://console.firebase.google.com/) → select or create the project.
2. **Build → Authentication → Sign-in method**
3. Enable **Email/Password** (Email link optional — not required for MVP).
4. Create at least one admin test user for desktop sign-in.

### 2.2 Cloud Firestore

1. **Build → Firestore Database → Create database**
2. Mode: **Native mode** (not Datastore mode)
3. Location: pick a region close to the lab (e.g. `us-central1`, `southamerica-east1`) — **immutable after creation**
4. Start in **production mode** or test mode; security rules in section 5 must be applied before mobile clients connect.

### 2.3 Service account (Admin SDK)

1. **Project settings → Service accounts → Generate new private key**
2. Save JSON locally as `APIKeys/<service-account>.json` — never commit to git.
3. Developer machines set `GOOGLE_APPLICATION_CREDENTIALS` to that file path.
4. In **Google Cloud Console → IAM**, grant the service account a Firestore write role, e.g.:
   - `Cloud Datastore User`, or
   - `Firebase Admin SDK Administrator Service Agent` (broader)

---

## 3. Firestore data model

### 3.1 Collection

| Property | Value |
| --- | --- |
| Collection id | `sds` |
| Document id | Same as `sdsId` field (see format below) |
| Pre-create documents? | **No** — the desktop app creates documents on register |

### 3.2 Document id format

```
sds_<32 lowercase hex chars>
```

Example: `sds_a1b2c3d4e5f64789a0b1c2d3e4f56789`

Generated in code as `sds_{Guid.NewGuid():N}`.

### 3.3 Document schema

Each document at `sds/{sdsId}` must conform to this schema. Fields marked **required** are always written on register; **optional** fields are omitted when empty (never stored as `null`).

| Field | Firestore type | Required | Description |
| --- | --- | --- | --- |
| `sdsId` | string | yes | Duplicate of document id; encoded in QR stickers |
| `displayName` | string | yes | Chemical / product name |
| `casNumber` | string | no | CAS registry number for search |
| `hazardSummary` | string | no | One-line hazard text for mobile cards |
| `storageProvider` | string | yes | Always `"onedrive"` for current MVP |
| `storagePath` | string | yes | OneDrive path pointer (Graph path string) |
| `oneDriveItemId` | string | yes | Microsoft Graph drive item id |
| `fileUrl` | string | yes | HTTPS URL to open PDF in OS viewer |
| `fileName` | string | yes | Original upload filename (e.g. `acetone_sds.pdf`) |
| `contentType` | string | yes | Always `"application/pdf"` |
| `expiresAt` | timestamp | yes | Chemical expiration date (UTC midnight of selected day) |
| `createdAt` | timestamp | yes | Registration time (UTC) |
| `updatedAt` | timestamp | yes | Last metadata update (UTC) |
| `createdBy` | string | no | Firebase Auth uid of registering admin |
| `active` | boolean | yes | `true` on create; set `false` for soft-delete |

### 3.4 Example document

Path: `sds/sds_a1b2c3d4e5f64789a0b1c2d3e4f56789`

```json
{
  "sdsId": "sds_a1b2c3d4e5f64789a0b1c2d3e4f56789",
  "displayName": "Acetone",
  "casNumber": "67-64-1",
  "hazardSummary": "Flammable liquid and vapor",
  "storageProvider": "onedrive",
  "storagePath": "/drives/b!abc123/root:/ChemClassify-SDS/sds_a1b2c3d4e5f64789a0b1c2d3e4f56789.pdf",
  "oneDriveItemId": "01ABCDEF123456789",
  "fileUrl": "https://contoso-my.sharepoint.com/...",
  "fileName": "acetone_sds.pdf",
  "contentType": "application/pdf",
  "expiresAt": "2027-06-15T00:00:00.000Z",
  "createdAt": "2026-03-20T14:30:00.000Z",
  "updatedAt": "2026-03-20T14:30:00.000Z",
  "createdBy": "firebase-auth-uid-here",
  "active": true
}
```

### 3.5 QR code rule

QR stickers encode **only** the raw `sdsId` string. They do **not** contain `fileUrl`, Storage paths, or deep links. The app resolves the id via Firestore after authentication.

---

## 4. Composite indexes

Create these indexes in **Firestore → Indexes → Composite** (or wait for the console link after the first query fails).

### Index 1 — expiration notification query

| Collection | Fields | Query scope |
| --- | --- | --- |
| `sds` | `active` Asc, `expiresAt` Asc | Collection |

Used by the notification job:

```
WHERE active == true AND expiresAt <= <threshold>
```

### Index 2 — active library list (optional, for sorted admin library)

| Collection | Fields | Query scope |
| --- | --- | --- |
| `sds` | `active` Asc, `displayName` Asc | Collection |

---

## 5. Security rules (MVP)

Apply in **Firestore → Rules**. Desktop admin writes use the **Admin SDK service account**, which bypasses these rules. Rules govern future **mobile / client SDK** access.

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    function isSignedIn() {
      return request.auth != null;
    }

    // MVP: any authenticated user can read active SDS records.
    // Tighten later with custom claims (admin vs field).
    match /sds/{sdsId} {
      allow read: if isSignedIn()
                  && resource.data.active == true;

      // Client writes disabled for MVP — desktop uses Admin SDK.
      // Mobile is read-only. Admin writes go through service account.
      allow create, update, delete: if false;
    }
  }
}
```

**Later tightening:** add `request.auth.token.admin == true` for write paths when client-side admin tools exist.

---

## 6. Environment variables the app expects

Set on developer machines (User-level env vars). Gemini does not need to set these in Firebase — list for completeness:

| Variable | Purpose |
| --- | --- |
| `GOOGLE_APPLICATION_CREDENTIALS` | Path to service account JSON |
| `FIREBASE_WEB_API_KEY` | Web API key for Identity Toolkit auth |
| `FIREBASE_PROJECT_ID` | GCP project id (fallback if not in service account JSON) |
| `ONEDRIVE_DRIVE_ID` | OneDrive drive id for PDF uploads |
| `ONEDRIVE_FOLDER_ID` | Target folder item id |
| `ONEDRIVE_TENANT_ID` | Azure AD tenant id |
| `ONEDRIVE_CLIENT_ID` | Azure app client id |
| `ONEDRIVE_CLIENT_SECRET` | Azure app client secret |
| `ONEDRIVE_LINK_SCOPE` | Optional; default `organization` |

OneDrive setup is documented in `DeveloperManual.Setup.md`. Firebase and OneDrive must both work for registration to succeed.

---

## 7. What Gemini should deliver

When setup is complete, confirm:

- [ ] Firebase project exists with Auth (Email/Password) enabled
- [ ] Firestore Native mode database created in chosen region
- [ ] Service account JSON downloaded; IAM grants Firestore read/write
- [ ] Composite index `sds`: `active` + `expiresAt` created
- [ ] Security rules applied (section 5)
- [ ] At least one test Auth user created
- [ ] `FIREBASE_WEB_API_KEY` and project id noted for developers
- [ ] **No** Firebase Storage bucket configured for SDS PDFs (OneDrive handles bytes)

---

## 8. Verification after desktop register test

After a developer runs the ChemClassify desktop app and registers one SDS PDF:

1. Firestore → `sds` collection → new document with id `sds_…`
2. Document contains all required fields from section 3.3
3. `storageProvider` == `"onedrive"`
4. `fileUrl` opens the PDF when authenticated to the org Microsoft account
5. OneDrive folder contains `{sdsId}.pdf`
6. QR encoding of the `sdsId` string scans to the same id text on a phone camera

---

## 9. Explicit non-goals for Firebase setup

- Do **not** create Firebase Storage rules or buckets for SDS PDFs
- Do **not** store PDF binary data in Firestore documents
- Do **not** parse PDF contents in Cloud Functions for expiration dates
- Do **not** put public URLs inside QR codes — id-only payloads

---

## 10. Reference: code that writes this schema

Implementation: `Systems/Firebase/FirebaseSDS.cs` → `FirebaseSds.RegisterAsync`

Call chain: `Displays/Application/FirebasePDFRegister.tscn` → `Systems/PDFRegister/PDFRegisterCore.cs` → `FirebaseSds.RegisterAsync`

Registration order: **OneDrive PDF upload → Firestore metadata write**. Partial failure may leave an orphan PDF in OneDrive without a Firestore doc.
