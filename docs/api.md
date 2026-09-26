# CloudStore REST API

## General conventions

The CloudStore API is a JSON REST API. All endpoints are served under the base path `/api/v1` and require a bearer token issued by Microsoft Entra ID (see the authentication document).

General conventions:

- Requests and responses use JSON with camelCase property names, except for file uploads and downloads.
- All timestamps are in UTC and formatted as ISO 8601.
- Identifiers are GUIDs.
- List endpoints are paginated with the query parameters `page` and `pageSize`. The default page size is 50 and the largest allowed page size is 200.
- All endpoints are tenant-scoped. The tenant is derived from the access token, never from the request.

## Documents

| Method | Path | Description | Required role |
|---|---|---|---|
| `GET` | `/api/v1/documents` | List documents, filterable by folder and tag | Reader |
| `GET` | `/api/v1/documents/{id}` | Get the metadata of a document | Reader |
| `GET` | `/api/v1/documents/{id}/content` | Download the file content | Reader |
| `POST` | `/api/v1/documents` | Upload a new document (multipart/form-data) | Editor |
| `PUT` | `/api/v1/documents/{id}` | Update name, folder or tags | Editor |
| `DELETE` | `/api/v1/documents/{id}` | Delete a document (soft delete) | Editor |

The upload endpoint accepts a multipart request with the fields `file`, `folderId` and an optional `tags` array. The response contains the metadata of the created document, including its id and checksum. The upload flow itself is described in the storage document.

Deleting a document is a soft delete: the metadata row is marked as deleted and the document disappears from all lists. The blob is removed later by the cleanup job.

## Folders and tags

| Method | Path | Description | Required role |
|---|---|---|---|
| `GET` | `/api/v1/folders` | List the folder tree | Reader |
| `POST` | `/api/v1/folders` | Create a folder | Editor |
| `PUT` | `/api/v1/folders/{id}` | Rename or move a folder | Editor |
| `DELETE` | `/api/v1/folders/{id}` | Delete an empty folder | Editor |
| `GET` | `/api/v1/tags` | List all tags of the tenant | Reader |

Folders can be nested. A folder that still contains documents cannot be deleted; the API returns HTTP 409 in that case.

## Sharing

| Method | Path | Description | Required role |
|---|---|---|---|
| `GET` | `/api/v1/folders/{id}/shares` | List who has access to a folder | Editor |
| `POST` | `/api/v1/folders/{id}/shares` | Share a folder with a user or group | TenantAdmin |
| `DELETE` | `/api/v1/folders/{id}/shares/{shareId}` | Remove a share | TenantAdmin |

Shares are always granted on folders, never on individual documents. A document inherits the shares of its folder.

## Error format

All errors are returned in the Problem Details format defined in RFC 9457 with the content type `application/problem+json`:

```json
{
  "type": "https://docs.cloudstore.example/errors/folder-not-empty",
  "title": "Folder is not empty",
  "status": 409,
  "detail": "The folder still contains 3 documents.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

The `traceId` corresponds to the operation id in Application Insights. When reporting a problem, always include it — it allows the team to find the complete trace of the request.

Commonly used status codes:

| Status | Meaning |
|---|---|
| 400 | The request is invalid, for example a required field is missing. Validation errors are listed in the `errors` property. |
| 401 | No token or an invalid token was sent. |
| 403 | The token is valid, but the user lacks the required role or folder permission. |
| 404 | The resource does not exist or belongs to another tenant. |
| 409 | The request conflicts with the current state. |
| 415 | The content type of an upload is not allowed. |
| 500 | An unexpected error occurred on the server. |
| 503 | The API is currently not able to handle requests. |

## Versioning

The API version is part of the URL path. The current version is `v1`. Breaking changes, such as removing a property or changing its type, require a new version. Adding new optional properties or new endpoints is not considered a breaking change and happens within `v1`.

When a new version is introduced, the previous version stays available for at least six months and responses from the old version carry a `Deprecation` header.

## OpenAPI description

The API publishes an OpenAPI document at `/openapi/v1.json`. In the dev environment, an interactive API explorer is available at `/scalar`. The explorer is disabled in prod.
