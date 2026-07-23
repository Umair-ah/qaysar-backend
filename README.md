# Qaysar Backend (.NET 8 Web API)

## Stack
- ASP.NET Core 8 Web API
- Entity Framework Core (SQL Server)
- JWT auth (localStorage token on client; tokens do not expire)
- Cloudflare R2 for image uploads (S3-compatible via AWSSDK.S3)
- Clean layering: `Controllers -> Services (Interfaces via DI) -> Data (EF Core)`

## Setup
1. Copy `.env.example` to `.env` and fill in values:
   - `SQLSERVER_CONNECTION` — your SQL Server connection string
   - `JWT_KEY` — long random secret (>= 32 chars)
   - `ADMIN_USERNAME` / `ADMIN_PASSWORD` — seeded on first run
   - `R2_*` — leave blank until you have R2 credentials
2. Run:
   ```
   dotnet restore
   dotnet ef migrations add Init      # optional, EnsureCreated runs as fallback
   dotnet run
   ```
3. Swagger: http://localhost:5080/swagger

## Endpoints (summary)
- `POST /api/auth/login`
- `GET  /api/brands` | `POST/PUT/DELETE` (auth)
- `GET  /api/categories` | `POST/PUT/DELETE` (auth)
- `GET  /api/products?page=1&pageSize=20&search=&brandId=&categoryId=&inStock=` (public, only visible)
- `GET  /api/products/{id}` (public)
- `GET  /api/products/admin` + admin CRUD (auth)
- `POST /api/uploads/image` (multipart, auth) — returns `{ url }`

## Notes
- Products have many-to-many with categories, single brand.
- `IsVisible` flag hides products from public endpoints; `InStock` toggles UI badge.
- No token expiry — client removes token from localStorage on logout.
