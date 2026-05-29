using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applies_to_all_wallets = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_categories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "households",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_households", x => x.id);
                    table.ForeignKey(
                        name: "fk_households_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    initial_balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "VND"),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallets", x => x.id);
                    table.CheckConstraint("ck_wallets_credit_limit", "(type = 0) OR (type = 1 AND credit_limit IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_wallets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inviter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    invitee_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_invitations_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_invitations_users_inviter_user_id",
                        column: x => x.inviter_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "household_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_members_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transactions_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wallet_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_wallet_categories_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_wallet_categories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_wallet_categories_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_category_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_with_all_members = table.Column<bool>(type: "boolean", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_category_shares", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_category_shares_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_category_shares_household_members_household_membe",
                        column: x => x.household_member_id,
                        principalTable: "household_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_category_share_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_household_member_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_category_share_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_category_share_targets_household_category_shares_",
                        column: x => x.share_id,
                        principalTable: "household_category_shares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_category_share_targets_household_members_target_h",
                        column: x => x.target_household_member_id,
                        principalTable: "household_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_id_deleted_at",
                table: "categories",
                columns: new[] { "user_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_id_parent_id",
                table: "categories",
                columns: new[] { "user_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_user_id_updated_at",
                table: "categories",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_household_category_share_targets_share_id_target_household_",
                table: "household_category_share_targets",
                columns: new[] { "share_id", "target_household_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_household_category_share_targets_target_household_member_id",
                table: "household_category_share_targets",
                column: "target_household_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_category_shares_category_id",
                table: "household_category_shares",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_category_shares_household_member_id_category_id",
                table: "household_category_shares",
                columns: new[] { "household_member_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_household_invitations_code",
                table: "household_invitations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_household_invitations_household_id",
                table: "household_invitations",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_invitations_inviter_user_id",
                table: "household_invitations",
                column: "inviter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_members_household_id_user_id_status",
                table: "household_members",
                columns: new[] { "household_id", "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_household_members_user_id_status",
                table: "household_members",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_households_created_by_user_id",
                table: "households",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id_occurred_at",
                table: "transactions",
                columns: new[] { "category_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id_deleted_at",
                table: "transactions",
                columns: new[] { "user_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id_occurred_at",
                table: "transactions",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id_updated_at",
                table: "transactions",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_wallet_id_occurred_at",
                table: "transactions",
                columns: new[] { "wallet_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallet_categories_category_id",
                table: "wallet_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_categories_user_id_updated_at",
                table: "wallet_categories",
                columns: new[] { "user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_categories_wallet_id_category_id",
                table: "wallet_categories",
                columns: new[] { "wallet_id", "category_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_wallets_user_id_deleted_at",
                table: "wallets",
                columns: new[] { "user_id", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wallets_user_id_updated_at",
                table: "wallets",
                columns: new[] { "user_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "household_category_share_targets");

            migrationBuilder.DropTable(
                name: "household_invitations");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "wallet_categories");

            migrationBuilder.DropTable(
                name: "household_category_shares");

            migrationBuilder.DropTable(
                name: "wallets");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "household_members");

            migrationBuilder.DropTable(
                name: "households");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
