using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <summary>
    /// Intentionally empty.
    /// </summary>
    /// <remarks>
    /// Task 2 converted PurchaseOrder.Status, ProductionBatch.Status/Stage/QualityStatus and
    /// StockTransfer.Status from <c>string</c> to enums. Because each conversion is backed by a
    /// value converter that writes the exact text those columns already contained, the physical
    /// schema is unchanged: every column stays <c>text</c> and every stored value stays valid.
    /// <para>
    /// The migration is still committed because it advances the model snapshot. Without it, the next
    /// schema change would generate a diff that also tried to re-describe these columns.
    /// </para>
    /// </remarks>
    public partial class AddStatusEnumConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change. See the class remarks.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No schema change. See the class remarks.
        }
    }
}
