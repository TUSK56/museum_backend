# VirtualMuseum Backend API Reference

This document is based on the currently verified backend source files:
`VirtualMuseum.API/Program.cs`, `VirtualMuseum.API/Controllers/*.cs`, `VirtualMuseum.API/DTOs/*.cs`, and the backend README.

## Base URLs

- Local development Swagger/API: `https://egymuseum.runasp.net`
- Local development HTTPS: `https://localhost:7032`
- Docker Compose backend: `https://egymuseum.runasp.net`

## Response Format

Most successful responses are wrapped in `ApiResponse<T>`:

```json
{
    "success": true,
    "data": {},
    "message": null,
    "details": null
}
```

Error responses use the same wrapper shape with `success: false` and a message.

## Authentication Endpoints

All routes are under `/api/auth`.

### `POST /api/auth/register`

Request body:

```json
{
    "fullName": "string",
    "email": "string",
    "region": "string",
    "password": "string"
}
```

Response data:

```json
{
    "userId": "guid",
    "email": "string",
    "fullName": "string",
    "region": "string"
}
```

### `POST /api/auth/login`

Request body:

```json
{
    "email": "string",
    "password": "string"
}
```

Response data:

```json
{
    "accessToken": "string",
    "refreshToken": "string",
    "userId": "guid",
    "email": "string",
    "fullName": "string",
    "role": "string"
}
```

Notes:

- If the email is not confirmed, the controller returns `403 Forbidden` with a message that asks the client to call OTP verification first.
- If the password is wrong, the controller returns `401 Unauthorized`.

### `POST /api/auth/send-otp`

Request body:

```json
{
    "email": "string"
}
```

Notes:

- In development, when SMTP is disabled, the API returns the OTP code in `data.code`.
- When SMTP is enabled, the API returns a success message only.

### `POST /api/auth/verify-otp`

Request body:

```json
{
    "email": "string",
    "code": "string"
}
```

### `POST /api/auth/refresh-token`

Request body:

```json
{
    "refreshToken": "string"
}
```

Response data matches the login response shape.

### `POST /api/auth/google-login`

Request body:

```json
{
    "idToken": "string"
}
```

### `POST /api/auth/forgot-password/request`

Request body:

```json
{
    "email": "string"
}
```

### `POST /api/auth/forgot-password/reset`

Request body:

```json
{
    "email": "string",
    "otpCode": "string",
    "newPassword": "string",
    "confirmPassword": "string"
}
```

## Museum Resource Endpoints

These controllers all use the `/api/[controller]` route pattern.

### Artifacts

- `GET /api/artifacts` - public
- `GET /api/artifacts/{id}` - public
- `POST /api/artifacts` - Admin only
- `PUT /api/artifacts/{id}` - Admin only
- `DELETE /api/artifacts/{id}` - Admin only

### Categories

- `GET /api/categories` - public
- `GET /api/categories/{id}` - public
- `POST /api/categories` - Admin only
- `PUT /api/categories/{id}` - Admin only
- `DELETE /api/categories/{id}` - Admin only

### Eras

- `GET /api/eras` - public
- `GET /api/eras/{id}` - public
- `POST /api/eras` - Admin only
- `PUT /api/eras/{id}` - no explicit `[Authorize]` attribute in the controller code
- `DELETE /api/eras/{id}` - Admin only

### Materials

- `GET /api/materials` - public
- `GET /api/materials/{id}` - public
- `POST /api/materials` - Admin only
- `PUT /api/materials/{id}` - no explicit `[Authorize]` attribute in the controller code
- `DELETE /api/materials/{id}` - Admin only

### Tags

- `GET /api/tags` - public
- `GET /api/tags/{id}` - public
- `POST /api/tags` - no explicit `[Authorize]` attribute in the controller code
- `PUT /api/tags/{id}` - Admin only
- `DELETE /api/tags/{id}` - Admin only

### Users

Controller-level `[Authorize(Roles = "Admin")]` is applied.

- `GET /api/users` - Admin only
- `GET /api/users/{id}` - Admin only
- `POST /api/users` - Admin only
- `PUT /api/users/{id}` - Admin only
- `DELETE /api/users/{id}` - Admin only

Request DTOs from `UsersController`:

```json
{
    "fullName": "string",
    "email": "string",
    "region": "string",
    "password": "string",
    "roleId": "guid",
    "isActive": true
}
```

```json
{
    "fullName": "string",
    "email": "string",
    "region": "string",
    "isActive": true
}
```

## Verified Backend Behavior Notes

- JWT bearer authentication is configured in `Program.cs`.
- CORS origins are read from `Cors:Origins`.
- The app runs EF Core migrations and seeding on startup.
- Swagger is enabled.
- The backend README states that a seeded admin account exists on first run if the database was empty.

