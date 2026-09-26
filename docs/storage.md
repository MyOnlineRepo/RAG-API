# Document Storage

## Overview

Uploaded documents are stored in Azure Blob Storage. The binary content of every file lives in the storage account `stcloudstoreprod`, while everything that describes the file — its name, owner, folder, tags, content type and checksum — is stored as metadata in PostgreSQL.

Keeping content and metadata apart lets CloudStore list, search and filter documents with fast database queries, without ever touching the blobs. Blobs are only read when a user actually downloads a file.

How the API authenticates against the storage account is described in the security document.

## Containers and blob naming

The storage account contains the following blob containers:

| Container | Content |
|---|---|
| `documents` | The content of all uploaded documents. |
| `thumbnails` | Preview images generated for PDF and image files. |
| `quarantine` | Files that failed the malware scan. |

Blobs in the `documents` container are named by tenant and document id:

```
documents/{tenantId}/{documentId}
```

The original file name is **not** part of the blob path. It is stored only in the metadata, so renaming a document is a pure database operation and never moves a blob. Because the path starts with the tenant id, all files of one customer can be found, exported or deleted with a single prefix.

## Upload flow

An upload goes through these steps:

1. The frontend sends the file to `POST /api/v1/documents` as a multipart request.
2. The API validates the request: the user must have the `Editor` role and write access to the target folder, and the content type must be allowed.
3. The API creates a new document id and a metadata row in PostgreSQL with the status `Uploading`.
4. The API streams the file content directly to Blob Storage. The file is never written to the container's local disk and never loaded into memory as a whole.
5. While streaming, the API computes a SHA-256 checksum of the content.
6. After the upload completes, the metadata row is updated with the checksum, the content length and the status `Scanning`.
7. Microsoft Defender for Storage scans the new blob for malware. The scan result is delivered to the API through an Event Grid subscription.
8. If the scan is clean, the status changes to `Available` and the document becomes visible to other users. If malware is found, the blob is moved to the `quarantine` container and the status changes to `Rejected`.

Files are uploaded as block blobs in chunks, which allows large files to be transferred efficiently and retried chunk by chunk if a network error occurs.

## Allowed content types

CloudStore accepts the following content types:

- PDF (`application/pdf`)
- Microsoft Office documents (Word, Excel, PowerPoint in the Open XML formats)
- Images (`image/png`, `image/jpeg`)
- Plain text and CSV

Other content types are rejected with HTTP 415. The content type is checked both from the request header and from the first bytes of the file, so renaming an executable to `.pdf` does not bypass the check.

## Downloads

Downloads are streamed through the API from `GET /api/v1/documents/{id}/content`. The API checks the user's permission on the folder, then opens a read stream on the blob and passes it through to the response. Users never receive direct URLs to the storage account.

## Versioning and deletion

Blob versioning is enabled on the storage account. When a user replaces the content of a document, the previous content remains available as an older blob version and can be restored by the support team.

When a user deletes a document, CloudStore performs a soft delete in PostgreSQL. The nightly cleanup job later deletes the blobs of soft-deleted documents. Soft delete for blobs is additionally enabled on the storage account as a safety net against accidental deletion.

## Redundancy

The storage account uses zone-redundant storage (ZRS). Every blob is replicated synchronously across three availability zones in the region, so the loss of a single zone does not affect uploads or downloads.

## Storage tiers

All blobs are written to the Hot access tier. A lifecycle management policy moves blobs that have not been read for a long time to the Cool tier to reduce cost. Moving between tiers is transparent to users; the download endpoint works the same for both tiers.
