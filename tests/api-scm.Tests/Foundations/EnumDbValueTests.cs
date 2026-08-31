using Domains.Enums;
using Domains.Exceptions;

namespace api_scm.Tests.Foundations;

/// <summary>
/// The enums must reproduce the legacy strings byte for byte, otherwise introducing them silently
/// breaks the existing database and the existing frontend.
/// </summary>
public sealed class EnumDbValueTests
{
    [Theory]
    [InlineData(PurchaseOrderStatus.Pending, "Pending")]
    [InlineData(PurchaseOrderStatus.Arrived, "Arrived")]
    [InlineData(PurchaseOrderStatus.Completed, "Completed")]
    [InlineData(PurchaseOrderStatus.Rejected, "Rejected")]
    [InlineData(PurchaseOrderStatus.Cancelled, "Cancelled")]
    public void PurchaseOrderStatus_matches_the_strings_the_procurement_ui_sends(
        PurchaseOrderStatus status, string expected)
        => EnumDbValue.ToDbValue(status).Should().Be(expected);

    [Theory]
    [InlineData(BatchStatus.Scheduled, "Scheduled")]
    [InlineData(BatchStatus.InProgress, "In Progress")]
    [InlineData(BatchStatus.PassedQa, "Passed QA")]
    [InlineData(BatchStatus.InventoryAdded, "Inventory Added")]
    public void BatchStatus_preserves_the_spaced_strings_the_production_ui_filters_on(
        BatchStatus status, string expected)
        => EnumDbValue.ToDbValue(status).Should().Be(expected);

    [Theory]
    [InlineData(ShipmentStatus.InTransit, "In Transit")]
    [InlineData(ShipmentStatus.Completed, "Completed")]
    public void ShipmentStatus_preserves_distribution_ui_strings(ShipmentStatus status, string expected)
        => EnumDbValue.ToDbValue(status).Should().Be(expected);

    [Theory]
    [InlineData(ProductionStage.QaReview, "QA Review")]
    [InlineData(ProductionStage.QualityControl, "Quality Control")]
    [InlineData(ProductionStage.MixingAndProcessing, "Mixing and Processing")]
    public void ProductionStage_absorbs_both_competing_frontend_vocabularies(
        ProductionStage stage, string expected)
        => EnumDbValue.ToDbValue(stage).Should().Be(expected);

    [Fact]
    public void Every_stage_string_used_anywhere_in_the_frontend_still_parses()
    {
        // ViewProduction.tsx STAGES plus UploadImagesModal.tsx stages plus what the service writes.
        string[] observed =
        [
            "Preparation", "Mixing and Processing", "Cooking", "Cooling", "Quality Control", "Packaging",
            "Peeling", "Steaming", "Mixing", "QA Review",
            "Completed", "Cancelled"
        ];

        foreach (var stage in observed)
        {
            EnumDbValue.TryParse<ProductionStage>(stage, out _)
                .Should().BeTrue($"'{stage}' appears in the frontend and must keep loading");
        }
    }

    [Fact]
    public void Parsing_is_case_insensitive_and_accepts_the_member_name_too()
    {
        EnumDbValue.Parse<BatchStatus>("in progress").Should().Be(BatchStatus.InProgress);
        EnumDbValue.Parse<BatchStatus>("InProgress").Should().Be(BatchStatus.InProgress);
        EnumDbValue.Parse<ShipmentStatus>("IN TRANSIT").Should().Be(ShipmentStatus.InTransit);
    }

    [Fact]
    public void An_empty_column_maps_to_Unspecified_rather_than_throwing()
    {
        // Several legacy entities default their status to string.Empty, so this is a real stored value.
        EnumDbValue.Parse<PurchaseOrderStatus>("").Should().Be(PurchaseOrderStatus.Unspecified);
        EnumDbValue.Parse<ProductionStage>(null).Should().Be(ProductionStage.Unspecified);
        EnumDbValue.Parse<QcStatus>("").Should().Be(QcStatus.Unspecified);
    }

    [Fact]
    public void Aliases_let_historical_spellings_load_without_being_written_back()
    {
        EnumDbValue.Parse<QcStatus>("Passed").Should().Be(QcStatus.Approved);
        EnumDbValue.Parse<MovementType>("Order Arrival").Should().Be(MovementType.PurchaseReceipt);
        EnumDbValue.Parse<MovementType>("OUT").Should().Be(MovementType.ProductionConsumption);
        EnumDbValue.Parse<LocationType>("Storage").Should().Be(LocationType.Warehouse);

        // The canonical value is what gets written, so aliases die out as data is rewritten.
        EnumDbValue.ToDbValue(QcStatus.Approved).Should().Be("Approved");
        EnumDbValue.ToDbValue(MovementType.PurchaseReceipt).Should().Be("Purchase Receipt");
    }

    [Fact]
    public void An_unrecognised_value_throws_with_the_accepted_list()
    {
        var act = () => EnumDbValue.Parse<PurchaseOrderStatus>("Shipped");

        act.Should().Throw<InvalidEnumDbValueException>()
            .WithMessage("*'Shipped' is not a valid PurchaseOrderStatus*")
            .WithMessage("*Pending*Arrived*Completed*");
    }

    [Fact]
    public void Db_values_are_unique_within_each_enum()
    {
        // A duplicated DbValue would make the reverse lookup ambiguous and silently pick one member.
        AssertUnique<PurchaseOrderStatus>();
        AssertUnique<BatchStatus>();
        AssertUnique<ProductionStage>();
        AssertUnique<ShipmentStatus>();
        AssertUnique<QcStatus>();
        AssertUnique<LotStatus>();
        AssertUnique<MovementType>();
        AssertUnique<LocationType>();
        AssertUnique<ReceiptStatus>();
        AssertUnique<QcDisposition>();
        AssertUnique<ApprovalStatus>();
        return;

        static void AssertUnique<TEnum>() where TEnum : struct, Enum
            => EnumDbValue.AllDbValues<TEnum>().Should().OnlyHaveUniqueItems(
                $"{typeof(TEnum).Name} must not map two members to the same stored string");
    }
}
