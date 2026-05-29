# Money Tracker — Backend (Iteration 1)

Backend cho app **Money Tracker** — quản lý thu-chi cá nhân & gia đình. .NET 8 + ASP.NET Core + EF Core + PostgreSQL.

## Iteration 1 cover

- ✅ Cấu trúc solution 3 project (Api / Domain / Infrastructure)
- ✅ Data model **đầy đủ** (wallet, category, transaction, household, member, share — tất cả entity)
- ✅ Migration tạo full schema từ đầu
- ✅ Auth: register / login / refresh / logout với JWT (access 15min + refresh 30 ngày, rotation)
- ✅ Wallets CRUD (ví thường + ví tín dụng có hạn mức)
- ✅ Categories CRUD (cây cha-con, flag `appliesToAllWallets`)
- ✅ Wallet-Category assignment

**Chưa làm (iteration sau):**
- Transactions endpoints
- Sync push/pull
- Household + invitation + sharing
- Reports

## Yêu cầu

- .NET 8 SDK
- Docker + Docker Compose (cho Postgres)
- (Tùy chọn) Rider / VS / VS Code

## Setup

```bash
# 1. Chạy Postgres
docker compose up -d

# 2. Cài EF tool nếu chưa có
dotnet tool install --global dotnet-ef

# 3. Restore packages
dotnet restore

# 4. Tạo migration đầu tiên (chạy ở root)
dotnet ef migrations add InitialCreate \
    --project src/MoneyTracker.Infrastructure \
    --startup-project src/MoneyTracker.Api \
    --output-dir Persistence/Migrations

# 5. Apply migration
dotnet ef database update \
    --project src/MoneyTracker.Infrastructure \
    --startup-project src/MoneyTracker.Api

# 6. Chạy API
dotnet run --project src/MoneyTracker.Api
```

Swagger UI: `https://localhost:7xxx/swagger` (port hiện trong console).

**Trước khi chạy production:** đổi `Jwt:SigningKey` trong `appsettings.json` thành chuỗi random >= 32 ký tự (dùng user secrets hoặc env var).

## Quyết định thiết kế đáng chú ý

### Sync cursor
Mọi entity user-data (`ISyncEntity`) đều có `CreatedAt`, `UpdatedAt`, `DeletedAt`. Postgres `timestamptz` lưu microsecond → đủ cho cursor. Soft delete giữ tombstone để client biết mà xoá local.

### UUID
`Guid` cho mọi PK. Client (Flutter) có thể tự gen UUID v7 và POST lên với `Id` đã có — backend tôn trọng nếu chưa trùng. Đây là điều kiện cần cho offline-first (tạo trước, sync sau).

### Snake_case columns
Dùng `EFCore.NamingConventions` → entity `WalletCategory` → bảng `wallet_categories`, property `CreditLimit` → cột `credit_limit`. Dễ đọc khi query trực tiếp DB.

### Currency
`numeric(18,2)` đủ cho VND lẫn các tiền tệ khác. Default `"VND"` ở mức column và DTO.

### Ví tín dụng
- `Type = Credit` thì `CreditLimit` bắt buộc, có check constraint ở DB
- Số dư khả dụng = `CreditLimit + InitialBalance + sum(transactions có dấu)` — tính ở tầng query/report sau

### Category tree
- Self-reference qua `ParentId`. V1 chỉ check 1-cấp khi update (TODO: cycle check khi cho phép nested sâu)
- `AppliesToAllWallets = true` → không cần row trong `wallet_categories`. Cleaner khi user tạo ví mới sau này

### Sharing (chưa implement endpoint, schema đã sẵn)
- `HouseholdCategoryShare`: bao gồm cả visibility lẫn assignment cho target members (theo confirm của bạn)
- Time window: `[StartedAt, EndedAt]` ∩ `[member.JoinedAt, member.LeftAt]` của sharer và viewer
- Family report: query transactions tham gia khi điều kiện share + window thoả

## API examples

### Register
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "ba@gmail.com",
  "password": "matkhau123",
  "displayName": "Ba"
}
```

Response:
```json
{
  "accessToken": "eyJhbGc...",
  "accessTokenExpiresAt": "2026-05-28T10:15:00+00:00",
  "refreshToken": "abc...",
  "refreshTokenExpiresAt": "2026-06-27T10:00:00+00:00",
  "user": { "id": "...", "email": "ba@gmail.com", "displayName": "Ba" }
}
```

### Tạo ví thường
```http
POST /api/wallets
Authorization: Bearer <accessToken>

{
  "name": "Tiền mặt",
  "type": "Regular",
  "initialBalance": 500000,
  "icon": "wallet"
}
```

### Tạo ví tín dụng
```http
POST /api/wallets
{ "name": "Visa Techcombank", "type": "Credit", "creditLimit": 50000000 }
```

### Tạo danh mục có cha + auto-assign tất cả ví
```http
POST /api/categories
{
  "name": "Ăn uống",
  "type": "Expense",
  "appliesToAllWallets": true,
  "icon": "restaurant"
}
```

```http
POST /api/categories
{
  "name": "Ăn ngoài",
  "type": "Expense",
  "parentId": "<id của Ăn uống>",
  "appliesToAllWallets": false,
  "assignToWalletIds": ["<wallet1>", "<wallet2>"]
}
```

## Cấu trúc thư mục

```
src/
├── MoneyTracker.Api/                  # Web layer
│   ├── Controllers/
│   ├── Dtos/
│   ├── Auth/                            # CurrentUser từ JWT claims
│   ├── Middleware/
│   ├── Program.cs                       # Composition root
│   └── appsettings.json
├── MoneyTracker.Domain/               # Entities, pure
│   ├── Entities/
│   └── Common/
└── MoneyTracker.Infrastructure/       # EF Core, JWT, infra
    ├── Persistence/
    │   ├── AppDbContext.cs
    │   ├── Configurations/              # Fluent API per entity
    │   └── Migrations/                  # (sinh ra sau khi chạy ef migrations add)
    ├── Auth/                            # JWT + BCrypt
    └── DependencyInjection.cs
```

## Iteration 2 (next)

- `TransactionsController` CRUD + validate wallet/category ownership
- Daily history endpoint (`GET /api/transactions?from=&to=`)
- Sync push (`POST /api/sync`) + pull (`GET /api/sync?since=`)
- Report endpoint (`GET /api/reports/monthly?year=&month=`)
