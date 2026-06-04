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

## Service layer convention
- Services tại `src/MoneyTracker.Api/Services/`, concrete class, no interface (thêm interface sau khi có nhu cầu mock thật sự)
- Constructor inject `AppDbContext`, KHÔNG inject `ICurrentUser` hoặc `IHttpContextAccessor` — caller (controller) pass `userId` qua method parameter
- Throw `DomainException` subtypes: `NotFoundException` → 404, `ConflictException` → 409, `ValidationException` → 400, `ForbiddenException` → 403, `ServiceBusyException` → 503; throw base `DomainException` cho 500 với specific error code (VD: `DEFAULT_PARTICIPANT_MISSING`)
- KHÔNG trả `IActionResult` / `ActionResult<T>` — trả DTO hoặc throw domain exception
- `ExceptionHandlingMiddleware` map `DomainException` → status + `ApiError(ex.ErrorCode, ex.Fields)`; non-DomainException → 500 `INTERNAL_ERROR`
- Controller thin: inject `ICurrentUser` + service → parse request → call service → map result sang ActionResult. Không try/catch. Không DbContext trực tiếp.
- KHÔNG repository layer — DbContext là Unit of Work
- `WalletsController` ngoại lệ: CRUD đủ đơn giản, giữ DbContext trực tiếp, không có WalletService

## Workflow
1. Sau khi đổi entity hoặc configuration: `dotnet ef migrations add <Name> --project src/MoneyTracker.Infrastructure --startup-project src/MoneyTracker.Api --output-dir Persistence/Migrations`
2. Apply: `dotnet ef database update --project src/MoneyTracker.Infrastructure --startup-project src/MoneyTracker.Api`
3. Sau khi thêm API endpoints mới: viết thêm vào file test.http
4. Sửa các unit test có gọi đến các hàm đã thay đổi

## Verification
1. `dotnet build` pass
2. Chạy lại tất cả unit test, đảm bảo pass tất cả unit test
3. Chạy: `dotnet run --project src/MoneyTracker.Api`

## Critical conventions (BẮT BUỘC tuân thủ khi thêm code)

### Sync model
- **Interface hierarchy** (tại `src/MoneyTracker.Domain/Common/`):
  - `IAuditableEntity` — `Id`, `CreatedAt`, `UpdatedAt`. Dùng cho mọi entity cần auto-stamp timestamp, kể cả entity không thuộc sync (User, Household, HouseholdCategoryShare, HouseholdWalletShare).
  - `ISyncEntity : IAuditableEntity` — thêm `UserId`, `DeletedAt`. Dùng cho mọi entity user-data tham gia offline-first sync (Wallet, Category, WalletCategory, Participant, Transaction).
- Khi thêm entity mới: implement `ISyncEntity` nếu cần sync, `IAuditableEntity` nếu chỉ cần timestamp.
- **Soft delete** dùng `DeletedAt` (tombstone cho client biết mà xóa local). Không bao giờ hard-delete user data.
- **Sync cursor** = `UpdatedAt` (Postgres timestamptz microsecond precision)
- **UUID** sinh client. Endpoint POST nhận optional `Id` từ client; nếu trùng → 409. Đây là điều kiện cần cho offline-first.
- `AppDbContext.SaveChanges` tự động stamp `CreatedAt`/`UpdatedAt` qua `ChangeTracker.Entries<IAuditableEntity>()` — không cần set tay. 

### Distributed synchronization convention
Khi một operation cần exactly-once execution trên môi trường **multi-instance**:
- Dùng **PostgreSQL advisory lock** (`pg_advisory_xact_lock`) — KHÔNG dùng `SemaphoreSlim` (chỉ works trong 1 process)
- Luôn set timeout trước khi acquire để tránh chờ vô hạn:
  ```sql
  SET LOCAL lock_timeout = '5000';   -- transaction-scoped, tự reset khi commit/rollback
  SELECT pg_advisory_xact_lock({key});
  ```
- Advisory lock **phải nằm trong explicit transaction** (`BeginTransactionAsync`) để auto-release khi commit/rollback
- Lock key: derive `bigint` từ GUID bằng `BitConverter.ToInt64(id.ToByteArray(), 0)`
- Dùng `ExecuteSqlAsync($"...")` (không phải `ExecuteSqlRawAsync`) để tránh SQL injection warning
- Nếu timeout (`PostgresException` SqlState `55P03`) → throw `ServiceBusyException` → 503
- Guard bằng `_db.Database.IsRelational()` để unit test (in-memory) không bị lỗi
- Extension method `_db.Database.AcquireAdvisoryLockAsync(key, ct)` tại `src/MoneyTracker.Api/Common/DatabaseFacadeExtensions.cs` — dùng trực tiếp trong bất kỳ service nào
- **BẮT BUỘC**: `AcquireAdvisoryLockAsync` phải được gọi bên trong `await using var tx = await _db.Database.BeginTransactionAsync(ct)` — `pg_advisory_xact_lock` là transaction-scoped, nếu không có transaction thì lock tự release ngay lập tức và không có tác dụng
- Để unit test timeout path trong service: extract lock call vào `protected virtual` method, subclass trong test để inject `ServiceBusyException` trực tiếp

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

### Error Response Convention
- Mọi error response dùng `ApiError` record: `{"error":"CODE"}` hoặc `{"error":"VALIDATION_FAILED","fields":{"field":"CODE"}}`
- `ApiError` định nghĩa tại `src/MoneyTracker.Api/Common/ApiError.cs`
- Error codes định nghĩa tại `src/MoneyTracker.Domain/Common/ErrorCodes.cs`
- **KHÔNG BAO GIỜ** trả natural language text trong error — kể cả "details for dev"
- Model validation error: override qua `InvalidModelStateResponseFactory` trong Program.cs
- Unhandled exception: `ExceptionHandlingMiddleware` trả `INTERNAL_ERROR`

### API Response
List/CRUD/sync endpoints trả về FK IDs ONLY, KHÔNG include resolved object của entity khác.

Client (Flutter) đã có SQLite local cache (categories, wallets, participants được sync sẵn), tự map FK → object qua in-memory map. Embedding object trong response gây ra:
- Stale data khi user rename category/wallet (transactions list còn show tên cũ)
- Payload size tăng nhiều lần (vài MB cho 1000 txs qua mạng yếu)
- Conflict source-of-truth với SQLite local
- Phá vỡ sync model (2 nguồn truth cho cùng 1 entity)

#### Áp dụng
- Transaction response: `categoryId`, `walletId`, `participantId` — KHÔNG có `category`, `wallet`, `participant` object
- Sync pull: chỉ trả các entity tables riêng biệt, không nested object trong transactions
- Bất kỳ list endpoint nào reference entity khác: chỉ ID

#### Exception: Report endpoints
Report là snapshot tại 1 thời điểm, không phải data sống. Cho phép include name (CHỈ name, không full object) để tiện hiển thị:
- `GET /api/reports/monthly` → `byCategory: [{ categoryId, name, amount, ... }]`
- `GET /api/reports/debt` → `[{ participantId, participantName, outstanding }]`

Phân biệt:
- **List/CRUD/Sync** → ID only (data sống, UI có thể re-render khi entity update)
- **Reports** → ID + name (snapshot, đọc 1 lần)

## System categories convention
- `UserId IS NULL` = system category. Hiện có 4 Debt categories với `SystemKey`: `DEBT_LEND`, `DEBT_COLLECT`, `DEBT_BORROW`, `DEBT_REPAY`.
- Hard-coded UUIDs: `11111111-1111-1111-1111-11111111100{1-4}` — KHÔNG thay đổi.
- **Mọi query category** PHẢI dùng extension method trong `MoneyTracker.Infrastructure.Persistence.Extensions.CategoryQueryExtensions`:
  - `ForUserIncludingSystem(userId)` — cho GET/list/report/sync pull
  - `ForUserOnly(userId)` — cho PUT/DELETE
  - KHÔNG bao giờ viết `WHERE UserId == ...` trực tiếp cho categories.
- Nếu client cố PUT/DELETE system category → 403 `SYSTEM_CATEGORY_READ_ONLY`.

## Default participant
- Mỗi user có đúng 1 participant `IsDefault=true` với `Name="Ai đó"`, tạo tự động trong `AuthController.Register`.
- Debt transactions (`CategoryType.Debt`) không có `ParticipantId` → server tự lookup participant IsDefault.

## Sync invariants
- `batchId` idempotent: nếu đã có SyncBatch với Id này → trả lại cached `ResponseJson` ngay, không xử lý lại.
- Atomic all-or-nothing: nếu BẤT KỲ item nào fail validation → ROLLBACK toàn batch, 400 `SYNC_BATCH_REJECTED`, KHÔNG lưu SyncBatch.
- LWW (Last Write Wins): nếu `existing.UpdatedAt > item.UpdatedAt` → skip (status="skipped"), không apply.
- Thứ tự apply: participants → wallets → walletCategories → categories → transactions.
- Sync pull trả về system categories qua `ForUserIncludingSystem`.

## Audit trail
- Chỉ `Transaction` entity được audit, không phải Wallet hay Category.
- `TransactionAudit` là append-only — không bao giờ xóa row.
- Populated qua `TransactionAuditInterceptor` (SaveChangesInterceptor), detect "delete" bằng `OriginalValues["DeletedAt"] == null && entity.DeletedAt != null`.
- `ActorDevice` từ HTTP header `X-Device-Id` (optional).

## What's done (Iteration 1)
- Auth: register / login / refresh / logout
- Wallets CRUD (Regular + Credit với CreditLimit, có check constraint DB)
- Categories CRUD (tree với ParentId, flag AppliesToAllWallets)
- Wallet-Category assignment endpoints

## What's done (Iteration 2)
- Transactions CRUD + daily history (flat list with date range filter)
- Participants CRUD (no DELETE), default participant "Ai đó" tạo lúc register
- System categories (Debt type, UserId=NULL): DEBT_LEND, DEBT_COLLECT, DEBT_BORROW, DEBT_REPAY
- Sync push/pull (idempotent batchId, LWW, atomic batch)
- Monthly/yearly/debt reports
- TransactionAudit interceptor

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
