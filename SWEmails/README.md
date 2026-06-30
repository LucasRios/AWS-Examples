# SWEmails — Email Sync Pipeline

> Serverless email capture pipeline on AWS Lambda — syncs IMAP, Gmail, and Outlook accounts to SQL Server using SQS, S3, and the Gmail/Graph REST APIs.

---

## Overview

A three-Lambda pipeline that captures emails from multiple accounts (any provider) and persists them to a SQL Server database — without polling the database from the email capture Lambda.

```
EventBridge (schedule)
    │
    ▼
┌──────────────────────┐
│  EmailDispatcher     │  ← Reads pending accounts from SQL, sends one SQS message per account
└──────────┬───────────┘
           │ SQS (one message per email account)
           ▼
┌──────────────────────┐
│  EmailCaptureWorker  │  ← Connects to IMAP / Gmail API / Graph API, downloads emails
│                      │    Uploads email JSON + attachments to S3
└──────────┬───────────┘
           │ S3 ObjectCreated events
           ▼
┌──────────────────────┐
│  EmailStorageWorker  │  ← Reads S3 files, calls SQL stored procedures, deletes processed files
└──────────────────────┘
```

This design keeps email provider credentials out of SQL Server's hot path — the Dispatcher passes an encrypted blob through SQS, and the StorageWorker writes back asynchronously.

---

## Lambda Functions

| Function | Trigger | Description |
|---|---|---|
| [EmailDispatcherLambda](EmailDispatcherLambda/) | EventBridge schedule | Claims accounts from SQL, dispatches to SQS |
| [EmailCaptureWorkerLambda](EmailCaptureWorkerLambda/) | SQS | Fetches emails from IMAP / Gmail API / Graph API, stages to S3 |
| [EmailStorageWorkerLambda](EmailStorageWorkerLambda/) | S3 ObjectCreated | Reads S3 files, persists to SQL Server, deletes processed files |

---

## Supported Providers

| Provider | Protocol |
|---|---|
| KingHost, iCloud, generic IMAP | IMAP (MailKit) |
| Google (Gmail) | Gmail REST API (OAuth2) |
| Microsoft (Outlook, Office 365) | Microsoft Graph API (OAuth2) |

---

## Environment Variables

### EmailDispatcherLambda

| Variable | Description |
|---|---|
| `SQL_CONNECTION_STRING` | SQL Server connection string |
| `SQS_FILA_WORKER` | SQS queue URL for the CaptureWorker |

### EmailCaptureWorkerLambda

| Variable | Description |
|---|---|
| `S3_EMAIL_BUCKET` | S3 bucket name for staging emails and token updates |
| `GOOGLE_CLIENT_ID` | Google OAuth2 client ID |
| `GOOGLE_CLIENT_SECRET` | Google OAuth2 client secret |
| `MICROSOFT_CLIENT_ID` | Azure AD application (client) ID |
| `MICROSOFT_CLIENT_SECRET` | Azure AD client secret |
| `MICROSOFT_TENANT_ID` | Azure AD tenant ID (defaults to `common` for multi-tenant) |
| `RIJNDAEL_CRYPTO_KEY` | *(optional)* AES-256 key for credential decryption |
| `RIJNDAEL_IV` | *(optional)* AES-256 IV for credential decryption |

### EmailStorageWorkerLambda

| Variable | Description |
|---|---|
| `SQL_CONNECTION_STRING` | SQL Server connection string |

---

## S3 Folder Structure

```
<bucket>/
├── leituras-pendentes/<db>/<codconta>_<uid>.json   ← New email payload
├── tokens-atualizados/<db>/<codconta>_<provider>.json ← OAuth token update
├── tokens-atualizados/<db>/<codconta>_imap_uid.json   ← IMAP UID watermark
└── erros-storage/
    ├── emails/    ← Emails that failed to persist (quarantine)
    └── tokens/    ← Token updates that failed to persist
```

---

## Getting Started

**Prerequisites:** .NET 8 SDK, AWS CLI, access to AWS Lambda + SQS + S3.

```bash
# Build and deploy a single Lambda
cd EmailDispatcherLambda
dotnet publish -c Release -r linux-x64 --self-contained false
zip -r function.zip publish/
aws lambda update-function-code \
  --function-name EmailDispatcherLambda \
  --zip-file fileb://function.zip
```

Set environment variables in the Lambda configuration (AWS Console or CLI).

---

## Credential Security

- Credentials in SQL Server are **Rijndael-256 encrypted** at the database layer (SQL CLR function).
- The encrypted blob travels through SQS without the Lambda ever seeing plain-text passwords.
- OAuth2 client credentials are stored as **Lambda environment variables** or **AWS Secrets Manager** — never hardcoded.
- S3 bucket access is controlled by the **Lambda execution IAM role** — no static AWS keys.

---

## Tech Stack

- .NET 8 — Lambda runtime
- AWS Lambda, SQS, S3, EventBridge
- MailKit — IMAP/SMTP client
- Gmail REST API, Microsoft Graph API
- SQL Server (Microsoft.Data.SqlClient)

## License

MIT
