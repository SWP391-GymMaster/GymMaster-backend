# GymMaster.API — Setup

ASP.NET Core 10 (.NET 10) Web API + SQL Server + EF Core 10. Auth: JWT + BCrypt.

## Cau hinh secret (BAT BUOC truoc khi chay)

Secret **khong** luu trong `appsettings*.json` (theo CONSTITUTION SEC-05).
Chay cac lenh sau trong thu muc `backend/GymMaster.API`:

```bash
dotnet user-secrets init   # chi can lan dau, neu chua co UserSecretsId

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<SERVER>;Database=GymMaster;User Id=<USER>;Password=<PASSWORD>;TrustServerCertificate=True"

dotnet user-secrets set "Jwt:SecretKey" "<chuoi-ngau-nhien-it-nhat-32-ky-tu>"
```

(Tuy chon) Google login:
```bash
dotnet user-secrets set "Google:ClientId" "<google-oauth-client-id>"
```

## Chay

```bash
dotnet run
```

API: http://localhost:5042 — Swagger/OpenAPI o moi truong Development.
Admin seed mac dinh: `admin@gymmaster.local` / `Admin123!`.

## Ghi chu
- O **Production**: dat secret qua bien moi truong / Azure App Service Configuration,
  vd `ConnectionStrings__DefaultConnection`, `Jwt__SecretKey`.
- User Secrets nam ngoai repo (`%APPDATA%\Microsoft\UserSecrets\<id>`), khong bi commit.
