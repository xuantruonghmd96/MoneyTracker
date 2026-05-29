# Money Tracker — Project Context for Claude Code

## What this is
Backend cho app **Money Tracker** — quản lý thu-chi cá nhân + gia đình. Mobile client là Flutter (chưa làm).

## Stack
- .NET 8 + ASP.NET Core Web API
- EF Core 8 + Npgsql + PostgreSQL
- EFCore.NamingConventions (snake_case tables/columns)
- JWT auth (BCrypt password) + refresh token rotation
- Swagger ở /swagger trong dev

## Solution layout
- `src/MoneyTracker.Api/` — controllers, DTOs, Program.cs
- `src/MoneyTracker.Domain/` — entities (no dependencies)
- `src/MoneyTracker.Infrastructure/` — EF, migrations, JWT, password hasher

## Critical conventions (BẮT BUỘC tuân thủ khi thêm code)

### Sync model
- Mọi entity user-data implement `ISyncEntity` (Id, UserId, CreatedAt, UpdatedAt, DeletedAt)
- **Soft delete** dùng `DeletedAt` (tombstone cho client biết mà xóa local). Không bao giờ hard-delete user data.
- **Sync cursor** = `UpdatedAt` (Postgres timestamptz microsecond precision)
- **UUID** sinh client. Endpoint POST nhận optional `Id` từ client; nếu trùng → 409. Đây là điều kiện cần cho offline-first.
- `AppDbContext.SaveChanges` tự động stamp `CreatedAt`/`UpdatedAt` qua reflection — không cần set tay.

### Auth
- Mọi controller (trừ `/api/auth/*`) require `[Authorize]`
- Lấy user hiện tại qua `ICurrentUser _currentUser` (inject), dùng `_currentUser.Id`
- **Mọi query** phải filter `UserId == _currentUser.Id` để tránh data leak giữa users
- **Mọi query** phải filter `DeletedAt == null` (trừ sync pull endpoint)

### DB
- Snake_case columns tự động qua `UseSnakeCaseNamingConvention()`
- Tiền: `numeric(18,2)`, lưu dương; dấu xác định bởi `Category.Type` (Income/Expense)
- Index pattern: `(UserId, UpdatedAt)` cho sync, `(UserId, OccurredAt)` cho list theo ngày
- Enum lưu int qua `HasConversion<int>()`

### Sharing semantics (cho household — iteration 3)
Khi share một category trong household, theo confirm: **cả hai**:
1. Visibility: target members thấy giao dịch sharer trong category đó trong family report
2. Assignment: category trở nên dùng được trong ví của target members

Time window: `[Share.StartedAt, Share.EndedAt]` ∩ `[Member.JoinedAt, Member.LeftAt]` của sharer.

## Error Response Convention
- Mọi error response dùng `ApiError` record: `{"error":"CODE"}` hoặc `{"error":"VALIDATION_FAILED","fields":{"field":"CODE"}}`
- `ApiError` định nghĩa tại `src/MoneyTracker.Api/Common/ApiError.cs`
- Error codes định nghĩa tại `src/MoneyTracker.Domain/Common/ErrorCodes.cs`
- **KHÔNG BAO GIỜ** trả natural language text trong error — kể cả "details for dev"
- Model validation error: override qua `InvalidModelStateResponseFactory` trong Program.cs
- Unhandled exception: `ExceptionHandlingMiddleware` trả `INTERNAL_ERROR`

## What's done (Iteration 1)
- Auth: register / login / refresh / logout
- Wallets CRUD (Regular + Credit với CreditLimit, có check constraint DB)
- Categories CRUD (tree với ParentId, flag AppliesToAllWallets)
- Wallet-Category assignment endpoints

## What's next (Iteration 2)
- Transactions CRUD
- Daily history với date range filter
- Sync push/pull cho mọi syncable entity
- Monthly/yearly reports theo category

## What's after (Iteration 3)
- Household creation + invitation flow (code + QR)
- Member join/leave với JoinedAt/LeftAt timeline
- Category sharing rules (cả visibility + assignment)
- Family report tổng hợp theo membership window

## Workflow
1. Sau khi đổi entity hoặc configuration: `dotnet ef migrations add <Name> --project src/MoneyTracker.Infrastructure --startup-project src/MoneyTracker.Api --output-dir Persistence/Migrations`
2. Apply: `dotnet ef database update --project src/MoneyTracker.Infrastructure --startup-project src/MoneyTracker.Api`
3. Chạy: `dotnet run --project src/MoneyTracker.Api`
