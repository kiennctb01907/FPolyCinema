using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho từng dòng của bảng chuyển trạng thái hóa đơn (CONTRACTS mục 5) và các chuyển không có trong bảng.</summary>
public class BillStateMachineTests
{
    private static bool Can(string from, string to, string? reason, string actor, string? currentReason = null)
        => BillStateMachine.CanTransition(from, to, reason, actor, out _, currentReason);

    // Dòng 1: waiting → finish (PosStaff tại quầy; hệ thống khi webhook báo thành công)
    [Theory]
    [InlineData(Roles.POSCashier)]
    [InlineData(Roles.ShiftManager)]
    [InlineData(Roles.Manager)]
    [InlineData(Actors.System)]
    public void WaitingToFinish_AllowedForPosStaffAndSystem(string actor)
    {
        Assert.True(BillStateMachine.CanTransition(BillStatus.Waiting, BillStatus.Finish, null, actor, out var code));
        Assert.Null(code);
    }

    [Theory]
    [InlineData(Roles.Customer)]
    [InlineData(Roles.TicketInspector)]
    public void WaitingToFinish_RejectedForOtherActors(string actor)
        => Assert.False(Can(BillStatus.Waiting, BillStatus.Finish, null, actor));

    // Dòng 2: waiting → cancel, CANCELLED_BEFORE_PAYMENT (PosStaff; khách với hóa đơn online của mình)
    [Theory]
    [InlineData(Roles.POSCashier)]
    [InlineData(Roles.ShiftManager)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Customer)]
    public void WaitingToCancelBeforePayment_AllowedForPosStaffAndCustomer(string actor)
        => Assert.True(Can(BillStatus.Waiting, BillStatus.Cancel, CancelReason.CancelledBeforePayment, actor));

    [Fact]
    public void WaitingToCancelBeforePayment_RejectedForSystem()
        => Assert.False(Can(BillStatus.Waiting, BillStatus.Cancel, CancelReason.CancelledBeforePayment, Actors.System));

    // Dòng 3: waiting → cancel, EXPIRED (hệ thống)
    [Fact]
    public void WaitingToExpired_AllowedOnlyForSystem()
    {
        Assert.True(Can(BillStatus.Waiting, BillStatus.Cancel, CancelReason.Expired, Actors.System));
        Assert.False(Can(BillStatus.Waiting, BillStatus.Cancel, CancelReason.Expired, Roles.POSCashier));
        Assert.False(Can(BillStatus.Waiting, BillStatus.Cancel, CancelReason.Expired, Roles.Customer));
    }

    // Dòng 4: finish → cancel, VOIDED_BY_POS (ShiftManager hoặc Manager duyệt)
    [Theory]
    [InlineData(Roles.ShiftManager)]
    [InlineData(Roles.Manager)]
    public void FinishToVoidedByPos_AllowedForApprovers(string actor)
        => Assert.True(Can(BillStatus.Finish, BillStatus.Cancel, CancelReason.VoidedByPos, actor));

    [Theory]
    [InlineData(Roles.POSCashier)]
    [InlineData(Roles.Customer)]
    [InlineData(Actors.System)]
    public void FinishToVoidedByPos_RejectedWithoutApprover(string actor)
        => Assert.False(Can(BillStatus.Finish, BillStatus.Cancel, CancelReason.VoidedByPos, actor));

    // Dòng 5: finish → cancel, CANCELLED_BY_USER (khách)
    [Fact]
    public void FinishToCancelledByUser_AllowedOnlyForCustomer()
    {
        Assert.True(Can(BillStatus.Finish, BillStatus.Cancel, CancelReason.CancelledByUser, Roles.Customer));
        Assert.False(Can(BillStatus.Finish, BillStatus.Cancel, CancelReason.CancelledByUser, Roles.Manager));
        Assert.False(Can(BillStatus.Finish, BillStatus.Cancel, CancelReason.CancelledByUser, Roles.POSCashier));
    }

    // Dòng 6: finish → cancel, INCIDENT_COMPENSATED (SupervisorOrManager)
    [Theory]
    [InlineData(Roles.ShiftManager, true)]
    [InlineData(Roles.Manager, true)]
    [InlineData(Roles.POSCashier, false)]
    [InlineData(Roles.TicketInspector, false)]
    public void FinishToIncidentCompensated_OnlySupervisorOrManager(string actor, bool expected)
        => Assert.Equal(expected, Can(BillStatus.Finish, BillStatus.Cancel, CancelReason.IncidentCompensated, actor));

    // Dòng 7: cancel (EXPIRED) → finish (hệ thống, webhook đến muộn, còn giữ được ghế cũ)
    [Fact]
    public void ExpiredToFinish_AllowedForSystemOnlyWhenCurrentReasonExpired()
    {
        Assert.True(Can(BillStatus.Cancel, BillStatus.Finish, null, Actors.System, CancelReason.Expired));
        Assert.False(Can(BillStatus.Cancel, BillStatus.Finish, null, Actors.System, CancelReason.VoidedByPos));
        Assert.False(Can(BillStatus.Cancel, BillStatus.Finish, null, Actors.System, null));
        Assert.False(Can(BillStatus.Cancel, BillStatus.Finish, null, Roles.Manager, CancelReason.Expired));
    }

    // Dòng 8: cancel (EXPIRED) → cancel, OVERBOOKED_REFUNDED (hệ thống, ghế đã bán cho người khác)
    [Fact]
    public void ExpiredToOverbookedRefunded_AllowedForSystemOnlyFromExpired()
    {
        Assert.True(Can(BillStatus.Cancel, BillStatus.Cancel, CancelReason.OverbookedRefunded, Actors.System, CancelReason.Expired));
        Assert.False(Can(BillStatus.Cancel, BillStatus.Cancel, CancelReason.OverbookedRefunded, Actors.System, CancelReason.CancelledByUser));
        Assert.False(Can(BillStatus.Cancel, BillStatus.Cancel, CancelReason.OverbookedRefunded, Roles.ShiftManager, CancelReason.Expired));
    }

    // Các chuyển trạng thái không có trong bảng: luôn bị từ chối với INVALID_TRANSITION
    [Theory]
    [InlineData(BillStatus.Finish, BillStatus.Waiting, null, Roles.Manager, null)]
    [InlineData(BillStatus.Cancel, BillStatus.Waiting, null, Actors.System, CancelReason.Expired)]
    [InlineData(BillStatus.Waiting, BillStatus.Waiting, null, Roles.POSCashier, null)]
    [InlineData(BillStatus.Finish, BillStatus.Finish, null, Actors.System, null)]
    [InlineData(BillStatus.Waiting, BillStatus.Cancel, CancelReason.VoidedByPos, Roles.Manager, null)]
    [InlineData(BillStatus.Waiting, BillStatus.Cancel, null, Roles.POSCashier, null)]
    [InlineData(BillStatus.Finish, BillStatus.Cancel, CancelReason.Expired, Actors.System, null)]
    [InlineData(BillStatus.Cancel, BillStatus.Cancel, CancelReason.Expired, Actors.System, CancelReason.Expired)]
    [InlineData(BillStatus.Waiting, BillStatus.Finish, CancelReason.Expired, Actors.System, null)]
    public void TransitionsOutsideTable_AreRejectedWithInvalidTransition(string from, string to, string? reason, string actor, string? currentReason)
    {
        Assert.False(BillStateMachine.CanTransition(from, to, reason, actor, out var code, currentReason));
        Assert.Equal(ErrorCodes.InvalidTransition, code);
    }

    [Fact]
    public void EnsureTransition_ThrowsBusinessRuleExceptionWith409()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            BillStateMachine.EnsureTransition(BillStatus.Finish, BillStatus.Waiting, null, Roles.Manager));
        Assert.Equal("INVALID_TRANSITION", ex.Code);
        Assert.Equal(409, ex.HttpStatus);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void EnsureTransition_DoesNotThrowForValidRow()
        => BillStateMachine.EnsureTransition(BillStatus.Waiting, BillStatus.Finish, null, Actors.System);
}
