# 3D Virtual Museum — Backend API

ASP.NET Core 8 Web API for the 3D Virtual Museum: artifacts, eras, categories, materials, tags, JWT auth (email/password, Google ID token), OTP email verification, refresh tokens, and admin user management.

## Tech stack

| Area | Technology |
|------|------------|
| Runtime | .NET 8 |
| API | ASP.NET Core Web API |
| Database | PostgreSQL |
| ORM | Entity Framework Core 8 |
| Auth | JWT Bearer, BCrypt passwords |
| Google sign-in | Google.Apis.Auth (ID token validation) |
| Email | MailKit (optional; can be disabled for local dev) |
| Docs | Swagger / OpenAPI |

**Architecture:** layered solution — `API` → `Application` → `Domain` ← `Infrastructure` (repositories, EF Core, email).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 14+ (local Docker via `docker compose up db`, or [Railway](https://railway.app) managed Postgres)

## Run the API

```bash
cd "VirtualMuseum Back-End"
dotnet run --project VirtualMuseum.API
```

Only one instance should run at a time. If the API is already running in another terminal or IDE, **stop it first** (Ctrl+C) before starting again — otherwise the build step can fail with **MSB3027 / MSB3021** (DLL copy locked by `VirtualMuseum.API`).

Default URLs (see `Properties/launchSettings.json`):

| Profile | HTTP | HTTPS |
|--------|------|--------|
| `http` | https://egymuseum.runasp.net | — |
| `https` | https://egymuseum.runasp.net | https://egymuseum.runasp.net |

- **Swagger UI:** https://egymuseum.runasp.net/swagger  
On startup the app connects to the database, applies **migrations**, and runs **seeding** (roles, admin user, sample eras/categories/materials/artifacts).

## Configuration (`VirtualMuseum.API/appsettings.json`)

### Database

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=virtualmuseum;Username=postgres;Password=postgres"
}
```

On **Railway**, link a PostgreSQL service — Railway sets `DATABASE_URL` automatically and migrations run on startup (`Database:RunMigrationsOnStartup` defaults to `true`). You can also set `ConnectionStrings__DefaultConnection` in service variables.

### JWT

```json
"Jwt": {
  "Key": "<long random secret, min 32 chars>",
  "Issuer": "VirtualMuseum.API",
  "Audience": "VirtualMuseum.Client",
  "AccessTokenMinutes": 60,
  "RefreshTokenDays": 7
}
```

Use a strong key in production (User Secrets, environment variables, or a vault — not only a committed default).

### SMTP (optional)

```json
"Smtp": {
  "Enabled": "false",
  "SmtpServer": "smtp.example.com",
  "Port": "587",
  "SenderName": "Virtual Museum",
  "SenderEmail": "no-reply@example.com",
  "Username": "...",
  "Password": "..."
}
```

- If **`Enabled`** is `false`, emails are **not** sent.  
- **`POST /api/auth/send-otp`** still creates an OTP and, when SMTP is disabled, returns the code in **`data.code`** for local testing.  
- **Forgot password** still stores an OTP server-side, but with SMTP off the user does not receive email; enable SMTP for real password reset, or use a verified flow in development via `send-otp` + `verify-otp` where applicable.

### Google OAuth (Google Sign-In)

```json
"GoogleAuth": {
  "ClientId": "<your-web-client-id>.apps.googleusercontent.com"
}
```

Use the **same** OAuth 2.0 **Web** client ID as the frontend Google Sign-In / Identity Services configuration. The API validates the **`idToken`** from the client.

### CORS

```json
"Cors": {
  "Origins": [ "https://egymuseum.runasp.net", "https://egymuseum.runasp.net", "https://egymuseum.runasp.net" ]
}
```

Add your deployed frontend origin(s). If the array is empty, the app falls back to a permissive default for development only — prefer explicit origins in production.

---

## Response shape

Successful payloads are wrapped as:

```json
{
  "success": true,
  "data": { },
  "message": null
}
```

Errors: `success: false`, `message` (and optionally `details` in Development). Global errors are handled by `ExceptionHandlingMiddleware`.

---

## Roles: **User** vs **Admin**

| Role | JWT `role` claim | Typical use |
|------|------------------|-------------|
| **User** | `User` | Default for registration and Google sign-up. |
| **Admin** | `Admin` | Seeded admin account; full **user management** and **museum content writes**. |

Authorization behavior:

| Endpoint group | Anonymous GET | Authenticated **User** | **Admin** |
|----------------|---------------|-------------------------|-----------|
| `GET` artifacts, categories, eras, materials, tags | Allowed | Allowed (GET is public) | Allowed |
| `POST` / `PUT` / `DELETE` on those resources | — | **403 Forbidden** | **Allowed** |
| `GET` / `POST` / `PUT` / `DELETE` `/api/users` | **401** without token | **403** | **Allowed** |

Use header: `Authorization: Bearer <accessToken>`.

---

## Authentication flows

### Email + password

1. **`POST /api/auth/register`** — creates user with `EmailConfirmed = false`.  
2. **`POST /api/auth/send-otp`** — sends (or returns in dev) a 6-digit OTP.  
3. **`POST /api/auth/verify-otp`** — sets `EmailConfirmed = true`.  
4. **`POST /api/auth/login`** — returns `accessToken`, `refreshToken`, and profile including **`role`**.

Password login **requires** a confirmed email. If the password is correct but the email is not verified yet, **`POST /api/auth/login`** returns **403** with a message to use **`send-otp`** and **`verify-otp`** (not 401, so it is easier to tell apart from wrong password, which still returns **401**).

### Google

**`POST /api/auth/google-login`** with `{ "idToken": "..." }`. New users are created with email confirmed; role is **User** unless changed in the database.

### Refresh

**`POST /api/auth/refresh-token`** with `{ "refreshToken": "..." }` — returns a new access + refresh pair; old refresh is invalidated per implementation.

### Forgot password

1. **`POST /api/auth/forgot-password/request`** — `{ "email": "..." }`  
   - If the account exists, an OTP is created and email is sent when SMTP is enabled.  
   - Response message is generic (does not reveal whether the email exists).

2. **`POST /api/auth/forgot-password/reset`** — `{ "email", "otpCode", "newPassword", "confirmPassword" }`  
   - On success, the user can log in with the new password (subject to email confirmation rules for login).

---

## API reference (summary)

### Auth (no bearer token required)

| Method | Route | Description |
|--------|--------|-------------|
| POST | `/api/auth/register` | Register |
| POST | `/api/auth/login` | Login (email confirmed required) |
| POST | `/api/auth/send-otp` | Request verification OTP |
| POST | `/api/auth/verify-otp` | Confirm email |
| POST | `/api/auth/refresh-token` | Rotate tokens |
| POST | `/api/auth/google-login` | Login with Google `idToken` |
| POST | `/api/auth/forgot-password/request` | Request reset OTP |
| POST | `/api/auth/forgot-password/reset` | Reset password with OTP |

**Login response `data` fields:** `accessToken`, `refreshToken`, `userId`, `email`, `fullName`, `role`.

### Museum resources (artifacts, categories, eras, materials, tags)

For each controller pattern:

| Method | Auth |
|--------|------|
| `GET /api/{resource}` | **Public** (no token) |
| `GET /api/{resource}/{id}` | **Public** |
| `POST` / `PUT` / `DELETE` | **`Admin` JWT only** |

Replace `{resource}` with: `artifacts`, `categories`, `eras`, `materials`, `tags`.

**Example — public list:**

```bash
curl https://egymuseum.runasp.net/api/artifacts
```

**Example — create era (admin only):**

```bash
curl -X POST https://egymuseum.runasp.net/api/eras ^
  -H "Content-Type: application/json" ^
  -H "Authorization: Bearer <ADMIN_ACCESS_TOKEN>" ^
  -d "{\"name\":\"New Kingdom\",\"startYear\":-1550,\"endYear\":-1077}"
```

Use IDs from your database (e.g. from GET responses) when creating artifacts or admin users.

### Users (**Admin** only)

| Method | Route |
|--------|--------|
| GET | `/api/users` |
| GET | `/api/users/{id}` |
| POST | `/api/users` |
| PUT | `/api/users/{id}` |
| DELETE | `/api/users/{id}` |

`POST` body includes `fullName`, `email`, `region`, `password`, `roleId`, `isActive`.  
`PUT` body: `fullName`, `email`, `region`, `isActive` (no password in update DTO).

---

## Seeded admin (first run only if DB was empty)

| Field | Value |
|-------|--------|
| Email | `admin@museum.com` |
| Password | `admin@123` |
| Role | **Admin** |

Change this password in production.

---

## Automated tests

Integration tests (real PostgreSQL from configuration or `DATABASE_URL`; stop any manual `dotnet run` of the API if the build copies fail):

```bash
dotnet test VirtualMuseum.IntegrationTests/VirtualMuseum.IntegrationTests.csproj
```

Optional manual script (with API running):

```powershell
.\API-Tests.ps1
# Optional base URL:
.\API-Tests.ps1 -BaseUrl "https://egymuseum.runasp.net"
```

---

## EF Core migrations

```bash
dotnet ef migrations add <Name> -p VirtualMuseum.Infrastructure -s VirtualMuseum.API
dotnet ef database update -p VirtualMuseum.Infrastructure -s VirtualMuseum.API
```

---

## Repository layout

```
VirtualMuseum Back-End/
├── VirtualMuseum.API/              # Controllers, DTOs, middleware, Program.cs
├── VirtualMuseum.Application/      # Services, interfaces
├── VirtualMuseum.Domain/           # Entities
├── VirtualMuseum.Infrastructure/ # DbContext, repositories, migrations, email
└── VirtualMuseum.IntegrationTests/
```

---

## Troubleshooting

- **403 on POST/PUT/DELETE museum routes:** JWT must be for a user with role **Admin**.  
- **401 on login:** Email not verified — complete `send-otp` + `verify-otp`.  
- **Google login fails:** `GoogleAuth:ClientId` must match the client that mints the `idToken`.  
- **CORS errors from browser:** Add your frontend origin under `Cors:Origins`.  
- **Build errors MSB3027 / MSB3021 (“cannot access … VirtualMuseum.API.dll … used by another process”):** Another `VirtualMuseum.API` is still running. Close that terminal (Ctrl+C), stop debugging in Visual Studio, or end the process in Task Manager, then run `dotnet run` or `dotnet build` again. To find the PID on Windows: `tasklist | findstr VirtualMuseum` then `taskkill /PID <pid> /F` if needed.

