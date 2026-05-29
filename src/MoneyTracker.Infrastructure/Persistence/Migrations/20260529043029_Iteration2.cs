using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MoneyTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Iteration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "participant_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "categories",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "system_key",
                table: "categories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "household_wallet_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_with_all_members = table.Column<bool>(type: "boolean", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_wallet_shares", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_wallet_shares_household_members_household_member_",
                        column: x => x.household_member_id,
                        principalTable: "household_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_wallet_shares_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participant_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_a_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_b_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participant_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sync_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transaction_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_device = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_audits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "household_wallet_share_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_household_member_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_wallet_share_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_wallet_share_targets_household_members_target_hou",
                        column: x => x.target_household_member_id,
                        principalTable: "household_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_household_wallet_share_targets_household_wallet_shares_shar",
                        column: x => x.share_id,
                        principalTable: "household_wallet_shares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "applies_to_all_wallets", "color", "created_at", "deleted_at", "icon", "name", "parent_id", "system_key", "type", "updated_at", "user_id" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111001"), true, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Cho vay", null, "DEBT_LEND", 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("11111111-1111-1111-1111-111111111002"), true, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Thu nợ", null, "DEBT_COLLECT", 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("11111111-1111-1111-1111-111111111003"), true, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Đi vay", null, "DEBT_BORROW", 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("11111111-1111-1111-1111-111111111004"), true, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Trả nợ", null, "DEBT_REPAY", 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_participant_id_occurred_at",
                table: "transactions",
                columns: new[] { "participant_id", "occurred_at" },
                filter: "participant_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_categories_system_key",
                table: "categories",
                column: "system_key",
                unique: true,
                filter: "user_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_categories_system_consistency",
                table: "categories",
                sql: "(user_id IS NULL AND system_key IS NOT NULL) OR (user_id IS NOT NULL AND system_key IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_household_wallet_share_targets_share_id_target_household_me",
                table: "household_wallet_share_targets",
                columns: new[] { "share_id", "target_household_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_household_wallet_share_targets_target_household_member_id",
                table: "household_wallet_share_targets",
                column: "target_household_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_wallet_shares_household_member_id_wallet_id",
                table: "household_wallet_shares",
                columns: new[] { "household_member_id", "wallet_id" });

            migrationBuilder.CreateIndex(
                name: "ix_household_wallet_shares_wallet_id",
                table: "household_wallet_shares",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ix_participant_links_household_id",
                table: "participant_links",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ux_participant_links_pair",
                table: "participant_links",
                columns: new[] { "participant_a_id", "participant_b_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_participants_user_id_updated_at",
                table: "participants",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ux_participants_user_id_default",
                table: "participants",
                column: "user_id",
                unique: true,
                filter: "is_default = true AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_participants_user_id_name",
                table: "participants",
                columns: new[] { "user_id", "name" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sync_batches_user_id_processed_at",
                table: "sync_batches",
                columns: new[] { "user_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_audits_transaction_id",
                table: "transaction_audits",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_audits_user_id_occurred_at",
                table: "transaction_audits",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_participants_participant_id",
                table: "transactions",
                column: "participant_id",
                principalTable: "participants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_participants_participant_id",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "household_wallet_share_targets");

            migrationBuilder.DropTable(
                name: "participant_links");

            migrationBuilder.DropTable(
                name: "participants");

            migrationBuilder.DropTable(
                name: "sync_batches");

            migrationBuilder.DropTable(
                name: "transaction_audits");

            migrationBuilder.DropTable(
                name: "household_wallet_shares");

            migrationBuilder.DropIndex(
                name: "ix_transactions_participant_id_occurred_at",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ux_categories_system_key",
                table: "categories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_categories_system_consistency",
                table: "categories");

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111001"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111002"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111003"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111004"));

            migrationBuilder.DropColumn(
                name: "participant_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "system_key",
                table: "categories");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
