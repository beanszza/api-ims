using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddPOApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""PurchaseRequisitions"" ADD COLUMN IF NOT EXISTS ""AdminNotes"" text;
                ALTER TABLE ""PurchaseRequisitions"" ADD COLUMN IF NOT EXISTS ""Notes"" text;
                ALTER TABLE ""PurchaseRequisitions"" ADD COLUMN IF NOT EXISTS ""Priority"" text;
                ALTER TABLE ""PurchaseRequisitions"" ADD COLUMN IF NOT EXISTS ""RequestType"" text;
                ALTER TABLE ""PurchaseRequisitions"" ADD COLUMN IF NOT EXISTS ""UpdatedAt"" timestamp with time zone;
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""AdminNotes"" text;
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""PrId"" integer;
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""PurchaseRequisitionPrId"" integer;
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""RequestedBy"" text NOT NULL DEFAULT '';
                CREATE INDEX IF NOT EXISTS ""IX_PurchaseOrders_PurchaseRequisitionPrId"" ON ""PurchaseOrders"" (""PurchaseRequisitionPrId"");
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PurchaseOrders_PurchaseRequisitions_PurchaseRequisitionPrId') THEN 
                        ALTER TABLE ""PurchaseOrders"" ADD CONSTRAINT ""FK_PurchaseOrders_PurchaseRequisitions_PurchaseRequisitionPrId"" 
                        FOREIGN KEY (""PurchaseRequisitionPrId"") REFERENCES ""PurchaseRequisitions"" (""PrId""); 
                    END IF; 
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""PurchaseOrders"" DROP CONSTRAINT IF EXISTS ""FK_PurchaseOrders_PurchaseRequisitions_PurchaseRequisitionPrId"";
                DROP INDEX IF EXISTS ""IX_PurchaseOrders_PurchaseRequisitionPrId"";
                ALTER TABLE ""PurchaseOrders"" DROP COLUMN IF EXISTS ""RequestedBy"";
                ALTER TABLE ""PurchaseOrders"" DROP COLUMN IF EXISTS ""PurchaseRequisitionPrId"";
                ALTER TABLE ""PurchaseOrders"" DROP COLUMN IF EXISTS ""PrId"";
                ALTER TABLE ""PurchaseOrders"" DROP COLUMN IF EXISTS ""AdminNotes"";
                ALTER TABLE ""PurchaseRequisitions"" DROP COLUMN IF EXISTS ""UpdatedAt"";
                ALTER TABLE ""PurchaseRequisitions"" DROP COLUMN IF EXISTS ""RequestType"";
                ALTER TABLE ""PurchaseRequisitions"" DROP COLUMN IF EXISTS ""Priority"";
                ALTER TABLE ""PurchaseRequisitions"" DROP COLUMN IF EXISTS ""Notes"";
                ALTER TABLE ""PurchaseRequisitions"" DROP COLUMN IF EXISTS ""AdminNotes"";
            ");
        }
    }
}
