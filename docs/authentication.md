# User Authentication

## Overview

CloudStore uses Microsoft Entra ID as its only identity provider. Users never create a CloudStore-specific password. Each customer organisation signs in with its own Entra ID tenant, and CloudStore is registered as a multi-tenant application.

Authentication is split between the Angular frontend, which obtains tokens, and the CloudStore API, which validates them. This document describes how end users authenticate. How the API itself authenticates against Azure services is described in the security document.

## App registrations

There are two app registrations per environment in Entra ID:

- **CloudStore SPA** – a public client for the Angular application. It has no client secret, because a browser application cannot keep a secret. Its redirect URIs point to the CloudStore frontend URLs.
- **CloudStore API** – represents the backend. It exposes the scope `api://cloudstore-api/Documents.ReadWrite` and defines the app roles `Reader`, `Editor` and `TenantAdmin`.

The SPA registration is granted permission to request the API scope. Customer administrators consent to CloudStore once for their organisation.

## Sign-in flow in the Angular application

The Angular application uses the OAuth 2.0 Authorization Code Flow with PKCE (Proof Key for Code Exchange). The implicit flow is not used, because it exposes tokens in the browser URL and is no longer recommended.

The flow works as follows:

1. The user opens CloudStore. The Angular application detects that no valid session exists.
2. The application creates a random code verifier and derives a code challenge from it.
3. The browser is redirected to the Entra ID authorize endpoint, including the code challenge and the requested API scope.
4. The user signs in with their organisational account, including multi-factor authentication if their organisation requires it.
5. Entra ID redirects back to CloudStore with an authorization code.
6. The Angular application exchanges the code and the original code verifier for an access token and a refresh token.
7. The access token is attached to every API request as a bearer token.

The frontend uses the Microsoft Authentication Library for JavaScript (MSAL Angular). MSAL handles token caching and silent renewal, so users are not asked to sign in again while their session in Entra ID is still valid.

## Token validation in the API

The CloudStore API validates JSON Web Tokens (JWT) on every request. It uses the ASP.NET Core JWT bearer authentication handler configured through Microsoft.Identity.Web.

The API checks:

- **Signature** – the token must be signed by Entra ID. Signing keys are loaded from the Entra ID metadata endpoint and refreshed automatically.
- **Issuer** – the token must be issued by an Entra ID tenant that is registered as a CloudStore customer. Unknown tenants are rejected even if the token is otherwise valid.
- **Audience** – the token must be issued for the CloudStore API.
- **Lifetime** – expired tokens are rejected. A small clock skew is tolerated.
- **Scope** – the token must contain the `Documents.ReadWrite` scope.

Requests without a valid token receive HTTP 401. Requests with a valid token but insufficient permissions receive HTTP 403.

## Authorization

After authentication, the API resolves the user and tenant from the token claims. The `tid` claim identifies the customer tenant and the `oid` claim identifies the user.

Authorization in CloudStore has two layers:

1. **App roles** – `Reader` can view documents, `Editor` can upload, change and delete documents, and `TenantAdmin` can manage users and sharing settings of the organisation. Roles are assigned by the customer's administrator in their own Entra ID tenant.
2. **Resource permissions** – folders can be shared with individual users or groups. The API checks in PostgreSQL whether the user may access a specific folder or document.

Every query in the API is filtered by tenant. It is not possible to access another tenant's data, even with a valid token and the `TenantAdmin` role.

## Sign-out and session handling

When a user signs out, MSAL clears the local token cache and redirects the browser to the Entra ID sign-out endpoint. The API does not keep sessions, so there is nothing to invalidate on the server side.

Access tokens are short-lived. If a user is disabled in Entra ID, they lose access as soon as their current access token expires and the refresh fails.

## Local development

For local development, developers use the dev app registrations. The Angular development server runs on `https://localhost:4200` and the API on `https://localhost:7080`; both URLs are registered as redirect URIs in the dev SPA registration. Developers sign in with accounts from the CloudStore development tenant.
