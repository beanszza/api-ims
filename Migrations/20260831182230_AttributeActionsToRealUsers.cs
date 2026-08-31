using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <summary>
    /// Widens the actor columns so they can hold a real identity, and records a display name alongside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Identity is owned by the central auth service, whose subject claim is an ASP.NET Identity id - a
    /// string. An integer column could never hold one, which is precisely why every posting used to
    /// record the literal <c>UserId = 1</c>. Widening to text is what makes honest attribution possible.
    /// </para>
    /// <para>
    /// Hand-edited. The scaffolder emitted a bare <c>ALTER COLUMN ... TYPE text</c>. That works for
    /// integer to numeric (PostgreSQL has a built-in cast, which is why the decimal migration needed no
    /// help) but there is no automatic cast from integer to text, so without an explicit
    /// <c>USING</c> clause this fails with "column cannot be cast automatically to type text".
    /// </para>
    /// <para>
    /// Existing rows are rewritten as <c>legacy:N</c> rather than plain <c>N</c>. The old integers were
    /// never verified identities, so carrying them across unchanged would keep asserting something
    /// untrue; the prefix marks them as unattributed history and guarantees they can never collide with
    /// a genuine auth subject.
    /// </para>
    /// </remarks>
    public partial class AttributeActionsToRealUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "InventoryMovementLogs"
                ALTER COLUMN "UserId" TYPE text USING 'legacy:' || "UserId"::text;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "AuditLogs"
                ALTER COLUMN "UserId" TYPE text USING 'legacy:' || "UserId"::text;
                """);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "InventoryMovementLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Give the backfilled rows a readable name so history is legible in the UI.
            migrationBuilder.Sql("""
                UPDATE "InventoryMovementLogs"
                SET "UserName" = 'Unattributed (pre-audit)'
                WHERE COALESCE("UserName", '') = '';

                UPDATE "AuditLogs"
                SET "UserName" = 'Unattributed (pre-audit)'
                WHERE COALESCE("UserName", '') = '';
                """);

            // ItemService used to write the literal string "scmsuser" into FieldName, a column meant for
            // the field that changed. Clear it so the column means one thing again.
            migrationBuilder.Sql("""
                UPDATE "AuditLogs" SET "FieldName" = '' WHERE "FieldName" = 'scmsuser';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UserName", table: "InventoryMovementLogs");
            migrationBuilder.DropColumn(name: "UserName", table: "AuditLogs");

            // Strip the prefix and fall back to 0 for anything that is not a legacy integer, because a
            // real auth subject has no integer representation. Narrowing is inherently lossy.
            migrationBuilder.Sql("""
                ALTER TABLE "InventoryMovementLogs"
                ALTER COLUMN "UserId" TYPE integer
                USING COALESCE(NULLIF(regexp_replace("UserId", '^legacy:', ''), '')::integer, 0);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "AuditLogs"
                ALTER COLUMN "UserId" TYPE integer
                USING COALESCE(NULLIF(regexp_replace("UserId", '^legacy:', ''), '')::integer, 0);
                """);
        }
    }
}
