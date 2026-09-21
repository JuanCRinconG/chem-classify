# Developer manual: setting up the project:

This manual is meant to be used by developers to be capable of setting up the project within their machines:  

This is a C# led project, using godot as an easy to use UI engine, and it will target android, IOS and desktop, the project currently uses firebase, here is how to setup the project:  

# Important: Do not push APIKeys to the repository

Keep service-account JSON, web API keys, SMTP passwords, and OneDrive / Azure secrets under `APIKeys/` (or equivalent local storage) and only in **User** environment variables. Never commit them.

---

# First dependency: FirebaseAdmin

Execute these commands within the project terminal, same folder the csproj is:  

```
dotnet add ChemClassify.csproj package FirebaseAdmin
```

Should work after that, some additional test if needed:  

```
dotnet --version

dotnet --list-sdks

Test-Path .\ChemClassify.csproj

Test-Path ".\APIKeys\<exact-filename>.json"  
```

Test-Path tests should return true after usage, if everything goes according to setup, now, execute these commands in order to setup credentials:

```
$credPath = (Resolve-Path ".\APIKeys\<exact-filename>.json").Path

$credPath
```

```
[System.Environment]::SetEnvironmentVariable(

  "GOOGLE_APPLICATION_CREDENTIALS",

$credPath,

"User"

)  
```

```
$env:GOOGLE_APPLICATION_CREDENTIALS = $credPath  
```

```
[System.Environment]::GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "User")

$env:GOOGLE_APPLICATION_CREDENTIALS

Test-Path $env:GOOGLE_APPLICATION_CREDENTIALS
```

you should see the credentials path after that, twice, as well as a True at the end for the Test-Path, reload your IDE and type this command to verify if everything worked:  

```
$env:GOOGLE_APPLICATION_CREDENTIALS
```

then, execute this command:

```
[System.Environment]::SetEnvironmentVariable("FIREBASE_WEB_API_KEY", "<the-key-in-APIKeys>", "User")

$env:FIREBASE_WEB_API_KEY = [System.Environment]::GetEnvironmentVariable("FIREBASE_WEB_API_KEY", "User")
```

this will provide the environment with the apiKey for the firebase web

Optionally set the Firebase / GCP project id explicitly (used by Firestore if the Admin SDK instance does not already expose it):

```
[System.Environment]::SetEnvironmentVariable("FIREBASE_PROJECT_ID", "<your-firebase-project-id>", "User")

$env:FIREBASE_PROJECT_ID = [System.Environment]::GetEnvironmentVariable("FIREBASE_PROJECT_ID", "User")
```

`GOOGLE_CLOUD_PROJECT` or `GCLOUD_PROJECT` also work as fallbacks.

---

# Second dependency: Google.Cloud.Firestore (SDS metadata)

The SDS registration API (`Systems/Firebase/FirebaseSDS.cs`, class `FirebaseSds`) writes chemical metadata to Firestore and stores PDF bytes in OneDrive. Install the Firestore client:

```
dotnet add ChemClassify.csproj package Google.Cloud.Firestore
```

The project already references `Google.Cloud.Firestore` when pulled from the repo; re-run the command only if restore failed or you are setting up from scratch.

Firestore uses the same Application Default Credentials as FirebaseAdmin (`GOOGLE_APPLICATION_CREDENTIALS`). No extra Google auth package is required beyond what you already set in the first section.

---

# Third dependency: Simple mail transfer protocol

First, install MailKit via this command:  

```
dotnet add ChemClassify.csproj package MailKit
```

Then, execute these commands to set up the environment variables for the system to work

```
[System.Environment]::SetEnvironmentVariable("SMTP_HOST", "smtp.gmail.com", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_PORT", "587", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_USER", "<MailerInAPIKeys>", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_PASS", "<MailerInAPIKeys>", "User")
[System.Environment]::SetEnvironmentVariable("SMTP_FROM", "<MailerInAPIKeys>", "User")
```

```
$env:SMTP_HOST = [System.Environment]::GetEnvironmentVariable("SMTP_HOST", "User")
$env:SMTP_PORT = [System.Environment]::GetEnvironmentVariable("SMTP_PORT", "User")
$env:SMTP_USER = [System.Environment]::GetEnvironmentVariable("SMTP_USER", "User")
$env:SMTP_PASS = [System.Environment]::GetEnvironmentVariable("SMTP_PASS", "User")
$env:SMTP_FROM = [System.Environment]::GetEnvironmentVariable("SMTP_FROM", "User")
```

```
$env:SMTP_HOST
$env:SMTP_PORT
$env:SMTP_USER
$env:SMTP_FROM
[string]::IsNullOrWhiteSpace($env:SMTP_PASS)
```

the last test should provide the environment variables plus a false, meaning that the password is not null or whitespace

---

# Fourth: Firebase / Firestore SDS collection setup

ChemClassify treats each SDS as **metadata in Firestore** plus **PDF bytes in OneDrive**. Clients query Firestore; they open the PDF with the OS viewer using the stored URL. They never parse PDF binaries for search or expiration mail.

## Enable Firestore

1. Open the [Firebase Console](https://console.firebase.google.com/) for your project.
2. Create or open **Firestore Database** (Native mode).
3. Pick a location close to the lab / team and finish creation.

## Collection: `sds`

You do **not** need to pre-create documents. `FirebaseSds.RegisterAsync` creates documents under:

```text
sds/{sdsId}
```

Document id format: `sds_` + 32 hex chars (GUID without dashes), for example `sds_a1b2c3d4e5f64789a0b1c2d3e4f56789`.

### Fields written on register

| Field | Type | Required | Purpose |
| --- | --- | --- | --- |
| `sdsId` | string | yes | Same as document id; QR will encode this later |
| `displayName` | string | yes | Chemical / product name |
| `casNumber` | string | no | Optional search / summary |
| `hazardSummary` | string | no | One short line for mobile cards |
| `storageProvider` | string | yes | Always `"onedrive"` for the current MVP |
| `storagePath` | string | yes | OneDrive path / pointer string |
| `oneDriveItemId` | string | yes | Graph drive item id |
| `fileUrl` | string | yes | HTTPS URL for OS open (`OS.shell_open`) |
| `fileName` | string | yes | Original upload filename |
| `contentType` | string | yes | `application/pdf` |
| `expiresAt` | timestamp | yes | Expiration for mail / queries |
| `createdAt` | timestamp | yes | Audit |
| `updatedAt` | timestamp | yes | Audit |
| `createdBy` | string | no | Admin Firebase Auth uid |
| `active` | bool | yes | Soft-delete flag (`true` on create) |

Optional fields that are empty are **omitted** from the document (not stored as null).

### Service account access

The Admin / Firestore client uses the service account JSON from `GOOGLE_APPLICATION_CREDENTIALS`. In Google Cloud IAM for the same project, that account needs permission to read/write Firestore (for example **Cloud Datastore User** / Firestore roles used by your org). If register fails with a permission error after OneDrive upload succeeds, check IAM first — the PDF may already exist as an orphan in OneDrive.

### Security rules (MVP intent)

Desktop admin writes currently go through the **Admin SDK** (service account), which bypasses client security rules. Still configure rules for future mobile / client SDK access:

- **Read:** authenticated users (admin + field)
- **Write:** admin only (custom claim or allowlist)
- Prefer soft-delete via `active: false` rather than hard deletes while stickers may still reference an id

Exact rule JSON can be tightened later; for local Admin-SDK development, having Firestore enabled and the service account authorized is enough to register.

### Manual console check after a successful register

1. Firebase Console → Firestore → collection `sds`
2. Open the new document id shown in the app (`SdsId` from `SdsRegisterResult`)
3. Confirm `fileUrl`, `expiresAt`, `storageProvider == "onedrive"`, and `active == true`
4. Open `fileUrl` in a browser while signed into the org account if link scope is `organization`

---

# Fifth: OneDrive setup (PDF bytes)

PDF files are uploaded with Microsoft Graph into a specific drive folder. Firestore only stores the pointer and open URL.

## Azure app registration (recommended for the team)

1. Open [Azure Portal](https://portal.azure.com/) → **Microsoft Entra ID** → **App registrations** → **New registration**.
2. Create an app (name e.g. `ChemClassify-SDS`).
3. Note **Application (client) ID** and **Directory (tenant) ID**.
4. **Certificates & secrets** → new client secret. Copy the secret value once; store it with your other API keys (not in git).
5. **API permissions** → Microsoft Graph → **Application** permissions suitable for your tenant, typically:
   - `Files.ReadWrite.All` (or a narrower Sites/Files permission if your IT mandates it)
6. Click **Grant admin consent** for the tenant.

Client-credentials auth uses scope `https://graph.microsoft.com/.default` (handled in code).

## Target drive and folder

1. In OneDrive / SharePoint, create a folder dedicated to SDS PDFs (e.g. `ChemClassify-SDS`).
2. Obtain:
   - **Drive id** → env `ONEDRIVE_DRIVE_ID`
   - **Folder item id** → env `ONEDRIVE_FOLDER_ID`

How to find them (pick one approach your team prefers):

- Graph Explorer / Graph request against the signed-in user's drive and folder
- SharePoint / OneDrive developer tools that show item ids
- A one-off Graph call: list children under the drive and copy the folder `id`

Uploads land as:

```text
{folder}/{sdsId}.pdf
```

## Environment variables

### Required for production-style app credentials

```
[System.Environment]::SetEnvironmentVariable("ONEDRIVE_DRIVE_ID", "<drive-id>", "User")
[System.Environment]::SetEnvironmentVariable("ONEDRIVE_FOLDER_ID", "<folder-item-id>", "User")
[System.Environment]::SetEnvironmentVariable("ONEDRIVE_TENANT_ID", "<directory-tenant-id>", "User")
[System.Environment]::SetEnvironmentVariable("ONEDRIVE_CLIENT_ID", "<application-client-id>", "User")
[System.Environment]::SetEnvironmentVariable("ONEDRIVE_CLIENT_SECRET", "<client-secret-value>", "User")
```

Refresh the current PowerShell / IDE session:

```
$env:ONEDRIVE_DRIVE_ID = [System.Environment]::GetEnvironmentVariable("ONEDRIVE_DRIVE_ID", "User")
$env:ONEDRIVE_FOLDER_ID = [System.Environment]::GetEnvironmentVariable("ONEDRIVE_FOLDER_ID", "User")
$env:ONEDRIVE_TENANT_ID = [System.Environment]::GetEnvironmentVariable("ONEDRIVE_TENANT_ID", "User")
$env:ONEDRIVE_CLIENT_ID = [System.Environment]::GetEnvironmentVariable("ONEDRIVE_CLIENT_ID", "User")
$env:ONEDRIVE_CLIENT_SECRET = [System.Environment]::GetEnvironmentVariable("ONEDRIVE_CLIENT_SECRET", "User")
```

Verify (secret should be present but do not print it in shared logs):

```
$env:ONEDRIVE_DRIVE_ID
$env:ONEDRIVE_FOLDER_ID
$env:ONEDRIVE_TENANT_ID
$env:ONEDRIVE_CLIENT_ID
[string]::IsNullOrWhiteSpace($env:ONEDRIVE_CLIENT_SECRET)
```

The last line should print `False`.

### Optional: link scope

After upload, the API requests a view link. Default scope is `organization` (same Microsoft 365 tenant). Override if needed:

```
[System.Environment]::SetEnvironmentVariable("ONEDRIVE_LINK_SCOPE", "organization", "User")
$env:ONEDRIVE_LINK_SCOPE = [System.Environment]::GetEnvironmentVariable("ONEDRIVE_LINK_SCOPE", "User")
```

Use `anonymous` only if the team explicitly accepts public view links. Prefer `organization` for lab SDS.

### Optional: short-lived manual token (local debug only)

If app credentials are not ready, you can temporarily set a Graph access token:

```
[System.Environment]::SetEnvironmentVariable("ONEDRIVE_ACCESS_TOKEN", "<graph-bearer-token>", "User")
$env:ONEDRIVE_ACCESS_TOKEN = [System.Environment]::GetEnvironmentVariable("ONEDRIVE_ACCESS_TOKEN", "User")
```

This is for quick local tests only. Tokens expire; prefer `ONEDRIVE_TENANT_ID` / `ONEDRIVE_CLIENT_ID` / `ONEDRIVE_CLIENT_SECRET` for lasting setup. If `ONEDRIVE_ACCESS_TOKEN` is set, the code uses it first.

## Partial failure note

Register order is: **OneDrive upload → Firestore write**. If Firestore fails after a successful upload, the API returns an error and logs the orphan OneDrive item id. Delete that orphan from the SDS folder or retry carefully so you do not leave duplicate PDFs without metadata.

---

# Success criteria in Godot

Before any godot testing is done, you must reload godot entirely after all the setups (env vars are read from the process environment).

## Auth / Admin connection

When executing the project normally, you should see a sample UID from an user from the firebase auth section, as well as a message that says "Firebase admin connection seems ok" (or equivalent successful Admin init / sign-in flow).

## SDS registration spine

Once `PDFRegisterCore` is wired to `FirebaseSds.RegisterAsync`, a successful admin register should:

1. Upload a PDF into the configured OneDrive folder named `{sdsId}.pdf`
2. Create Firestore document `sds/{sdsId}` with `fileUrl`, `expiresAt`, and `storageProvider` = `onedrive`
3. Return / display the new `sdsId` in the register UI

If OneDrive env vars are missing, registration fails before any Firestore write. If Firestore / project id / IAM is wrong, you may get an orphan PDF in OneDrive and a metadata error in the Godot output.
