using Applications.Services;
using Domains.Enums;
using Domains.Exceptions;

namespace api_scm.Tests.Foundations;

public sealed class StatusTransitionGuardTests
{
    private readonly StatusTransitionGuard _guard = new();

    // ---------- Purchase orders ----------

    [Theory]
    [InlineData(PurchaseOrderStatus.Pending, PurchaseOrderStatus.Arrived)]
    [InlineData(PurchaseOrderStatus.Pending, PurchaseOrderStatus.Cancelled)]
    [InlineData(PurchaseOrderStatus.Arrived, PurchaseOrderStatus.Completed)]
    [InlineData(PurchaseOrderStatus.Arrived, PurchaseOrderStatus.Rejected)]
    public void Legal_purchase_order_transitions_are_allowed(
        PurchaseOrderStatus from, PurchaseOrderStatus to)
        => _guard.CanTransition(from, to).Should().BeTrue();

    [Fact]
    public void A_purchase_order_cannot_skip_inspection_by_jumping_from_Pending_to_Completed()
    {
        var act = () => _guard.EnsureCanTransition(
            PurchaseOrderStatus.Pending, PurchaseOrderStatus.Completed);

        act.Should().Throw<InvalidStatusTransitionException>()
            .WithMessage("*from 'Pending' to 'Completed'*")
            .WithMessage("*Allowed next: 'Arrived', 'Cancelled'*");
    }

    [Fact]
    public void A_completed_purchase_order_is_final()
    {
        _guard.IsFinal(PurchaseOrderStatus.Completed).Should().BeTrue();

        var act = () => _guard.EnsureCanTransition(
            PurchaseOrderStatus.Completed, PurchaseOrderStatus.Cancelled);

        act.Should().Throw<InvalidStatusTransitionException>()
            .WithMessage("*'Completed' is a final state*");
    }

    // ---------- Shipments ----------

    [Fact]
    public void A_transfer_cannot_be_completed_without_going_through_transit()
    {
        // The old code let this through: Pending -> Completed matched no side-effect branch, so the
        // transfer was marked delivered while the source was never debited.
        var act = () => _guard.EnsureCanTransition(ShipmentStatus.Pending, ShipmentStatus.Completed);

        act.Should().Throw<InvalidStatusTransitionException>()
            .WithMessage("*from 'Pending' to 'Completed'*")
            .WithMessage("*'In Transit'*");
    }

    [Theory]
    [InlineData(ShipmentStatus.Pending, ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.Completed)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.Cancelled)]
    public void Legal_shipment_transitions_are_allowed(ShipmentStatus from, ShipmentStatus to)
        => _guard.CanTransition(from, to).Should().BeTrue();

    // ---------- Batches ----------

    [Fact]
    public void A_scheduled_batch_cannot_be_released_by_qa_before_it_starts()
    {
        var act = () => _guard.EnsureCanTransition(BatchStatus.Scheduled, BatchStatus.PassedQa);

        act.Should().Throw<InvalidStatusTransitionException>()
            .WithMessage("*from 'Scheduled' to 'Passed QA'*");
    }

    [Fact]
    public void A_rejected_batch_cannot_be_posted_to_inventory()
    {
        _guard.CanTransition(BatchStatus.Rejected, BatchStatus.InventoryAdded).Should().BeFalse();
        _guard.IsFinal(BatchStatus.Rejected).Should().BeTrue();
    }

    [Fact]
    public void Posting_a_batch_to_inventory_is_terminal_so_it_cannot_be_posted_twice()
    {
        _guard.IsFinal(BatchStatus.InventoryAdded).Should().BeTrue();
    }

    [Theory]
    [InlineData(BatchStatus.Scheduled, BatchStatus.InProgress)]
    [InlineData(BatchStatus.InProgress, BatchStatus.PassedQa)]
    [InlineData(BatchStatus.InProgress, BatchStatus.Rejected)]
    [InlineData(BatchStatus.PassedQa, BatchStatus.InventoryAdded)]
    [InlineData(BatchStatus.Completed, BatchStatus.InventoryAdded)]
    public void Legal_batch_transitions_are_allowed(BatchStatus from, BatchStatus to)
        => _guard.CanTransition(from, to).Should().BeTrue();

    // ---------- Lots ----------

    [Fact]
    public void Quarantined_stock_can_be_released_or_rejected_but_never_consumed_directly()
    {
        _guard.CanTransition(LotStatus.Quarantine, LotStatus.Available).Should().BeTrue();
        _guard.CanTransition(LotStatus.Quarantine, LotStatus.Rejected).Should().BeTrue();
        _guard.CanTransition(LotStatus.Quarantine, LotStatus.Consumed).Should().BeFalse(
            "uninspected stock must be released before it can be used");
    }

    [Fact]
    public void Consumed_and_disposed_lots_are_final()
    {
        _guard.IsFinal(LotStatus.Consumed).Should().BeTrue();
        _guard.IsFinal(LotStatus.Disposed).Should().BeTrue();
    }

    // ---------- General rules ----------

    [Theory]
    [InlineData(PurchaseOrderStatus.Completed)]
    [InlineData(PurchaseOrderStatus.Pending)]
    public void Re_applying_the_current_status_is_a_no_op_and_never_throws(PurchaseOrderStatus status)
    {
        // Several callers re-save an unchanged status; that must not be treated as an illegal move.
        _guard.CanTransition(status, status).Should().BeTrue();
    }

    [Fact]
    public void Nothing_can_transition_back_into_Unspecified()
    {
        _guard.CanTransition(PurchaseOrderStatus.Pending, PurchaseOrderStatus.Unspecified)
            .Should().BeFalse();
        _guard.CanTransition(BatchStatus.InProgress, BatchStatus.Unspecified).Should().BeFalse();
    }

    [Fact]
    public void Legacy_Unspecified_rows_have_a_way_forward()
    {
        _guard.AllowedFrom(PurchaseOrderStatus.Unspecified).Should().NotBeEmpty(
            "rows written before statuses existed must be recoverable");
    }

    [Fact]
    public void Classification_enums_are_rejected_with_an_explanation_rather_than_silently_passing()
    {
        // LocationType, MovementType and QcDisposition classify things; they are not lifecycles.
        // Asking the guard about them is a programming error and should say so.
        var act = () => _guard.CanTransition(LocationType.Warehouse, LocationType.Branch);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LocationType has no registered transition map*")
            .WithMessage("*classifications, not*lifecycles*");
    }
}
