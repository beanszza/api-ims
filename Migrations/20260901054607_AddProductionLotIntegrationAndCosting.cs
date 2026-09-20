using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionLotIntegrationAndCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Recipes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "YieldUomId",
                table: "Recipes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "ProductionBatches",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedDate",
                table: "ProductionBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FgLotId",
                table: "ProductionBatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapQuantity",
                table: "ProductionBatches",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ScrapReason",
                table: "ProductionBatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalMaterialCost",
                table: "ProductionBatches",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "ProductionBatches",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "YieldPercentage",
                table: "ProductionBatches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "SourceSupplierId",
                table: "BatchConsumptions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "LotId",
                table: "BatchConsumptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityUsed",
                table: "BatchConsumptions",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "BatchConsumptions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "UomId",
                table: "BatchConsumptions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_YieldUomId",
                table: "Recipes",
                column: "YieldUomId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_FgLotId",
                table: "ProductionBatches",
                column: "FgLotId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchConsumptions_LotId",
                table: "BatchConsumptions",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchConsumptions_UomId",
                table: "BatchConsumptions",
                column: "UomId");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchConsumptions_InventoryLots_LotId",
                table: "BatchConsumptions",
                column: "LotId",
                principalTable: "InventoryLots",
                principalColumn: "LotId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BatchConsumptions_UnitOfMeasures_UomId",
                table: "BatchConsumptions",
                column: "UomId",
                principalTable: "UnitOfMeasures",
                principalColumn: "UomId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionBatches_InventoryLots_FgLotId",
                table: "ProductionBatches",
                column: "FgLotId",
                principalTable: "InventoryLots",
                principalColumn: "LotId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_UnitOfMeasures_YieldUomId",
                table: "Recipes",
                column: "YieldUomId",
                principalTable: "UnitOfMeasures",
                principalColumn: "UomId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BatchConsumptions_InventoryLots_LotId",
                table: "BatchConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_BatchConsumptions_UnitOfMeasures_UomId",
                table: "BatchConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionBatches_InventoryLots_FgLotId",
                table: "ProductionBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_UnitOfMeasures_YieldUomId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_YieldUomId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_ProductionBatches_FgLotId",
                table: "ProductionBatches");

            migrationBuilder.DropIndex(
                name: "IX_BatchConsumptions_LotId",
                table: "BatchConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_BatchConsumptions_UomId",
                table: "BatchConsumptions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "YieldUomId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "FgLotId",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "ScrapQuantity",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "ScrapReason",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "TotalMaterialCost",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "YieldPercentage",
                table: "ProductionBatches");

            migrationBuilder.DropColumn(
                name: "LotId",
                table: "BatchConsumptions");

            migrationBuilder.DropColumn(
                name: "QuantityUsed",
                table: "BatchConsumptions");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "BatchConsumptions");

            migrationBuilder.DropColumn(
                name: "UomId",
                table: "BatchConsumptions");

            migrationBuilder.AlterColumn<int>(
                name: "SourceSupplierId",
                table: "BatchConsumptions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
