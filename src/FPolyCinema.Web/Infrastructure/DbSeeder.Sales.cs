using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure.Seed;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Phần mô phỏng bán hàng của DbSeeder: hóa đơn đã thanh toán của 30 ngày qua (tại quầy và online, có vé và bắp nước,
/// cuối tuần và buổi tối đông hơn), vé bán trước cho các suất sắp chiếu (suất bị khóa), vài hóa đơn hủy với các lý do khác nhau.
/// </summary>
public sealed partial class DbSeeder
{
    private const long KhachVipTargetSpend = 4_300_000;
    private const int VoidedBillTarget = 3;

    private enum CheckInMode { None, Full, Partial }

    private sealed record SaleRequest(
        int CinemaIndex,
        SeedShow? Show,
        string Channel,
        int? CustomerId,
        List<SeedSeat> Seats,
        List<string> Audience,
        List<(SeedDrink Drink, int Qty)> Fnb,
        DateTime PaidAt,
        string PaymentMethod,
        string? VoucherCode,
        CheckInMode CheckIn,
        bool FnbPrepared);

    private sealed record SaleResult(int BillId, string Code, long Total, DateTime PaidAt, int? ShiftKeyCinema, SeedShift? Shift);

    private int _voidedBills;
    private readonly HashSet<string> _qrCodes = new();

    // ---------------------------------------------------------------- điều phối
    private void SeedSales(SqlConnection conn, SqlTransaction tx)
    {
        foreach (var show in _shows.Where(s => s.Start < _now).OrderBy(s => s.Start))
        {
            SellPastShow(conn, tx, show);
        }
        SellFnbOnly(conn, tx);
        TopUpKhachToVip(conn, tx);

        int k = 0;
        foreach (var show in _shows.Where(s => s.Start >= _now && s.Start < _now.AddDays(4)).OrderBy(s => s.Start))
        {
            if (k++ % 3 == 0) SellUpcoming(conn, tx, show);
        }
        SellKhachUpcoming(conn, tx);
        SeedCancellations(conn, tx);
    }

    // ---------------------------------------------------------------- suất đã chiếu
    private double Occupancy(SeedShow s)
    {
        // Buổi tối (17:00–22:00) và cuối tuần đông hơn; thứ Ba giá rẻ đông hơn ngày thường một chút
        double occ = 0.02;
        var t = s.Feature.TimeOfDay;
        if (t >= new TimeSpan(17, 0, 0) && t < new TimeSpan(22, 0, 0)) occ += 0.06;
        else if (t >= new TimeSpan(12, 0, 0)) occ += 0.015;
        var dow = s.Plan.OperatingDate.DayOfWeek;
        if (dow is DayOfWeek.Saturday or DayOfWeek.Sunday) occ += 0.045;
        if (dow == DayOfWeek.Tuesday) occ += 0.01;
        if (s.Movie.IsBlockbuster) occ += 0.015;
        return occ * (0.7 + _rnd.NextDouble() * 0.6);
    }

    private void SellPastShow(SqlConnection conn, SqlTransaction tx, SeedShow show)
    {
        int sellable = show.Auditorium.Seats.Count(x => !x.House && !x.Maintenance && x.Type != SeatType.Wheelchair);
        int target = (int)Math.Round(sellable * Occupancy(show));
        int sold = 0, guard = 0;
        while (sold < target && guard++ < 60)
        {
            bool pos = _rnd.NextDouble() < 0.68;
            int size = GroupSize();
            bool sweet = show.Auditorium.Spec.SweetboxLastRow && size == 2 && _rnd.NextDouble() < 0.35;
            var seats = PickSeats(show, size, sweet);
            if (seats.Count == 0) break;
            sold += seats.Count;

            var paidAt = pos ? PosPaidAt(show) : OnlinePaidAt(show);
            if (paidAt == null) { ReleaseTaken(show, seats); continue; }

            bool toVoid = pos && _voidedBills < VoidedBillTarget && _rnd.NextDouble() < 0.004;
            var fnb = toVoid ? new List<(SeedDrink, int)>() : PickFnb(pos ? 0.40 : 0.35);
            var checkIn = toVoid ? CheckInMode.None : PickCheckInMode(seats.Count);
            int? customer = pos ? (_rnd.NextDouble() < 0.15 ? RandomCustomer() : null) : PickOnlineCustomer();
            var req = new SaleRequest(show.CinemaIndex, show, pos ? BillChannel.Pos : BillChannel.Online, customer, seats,
                pos ? PickAudiencePos(show, seats.Count) : PickAudienceOnline(seats.Count), fnb, paidAt.Value,
                pos ? (_rnd.NextDouble() < 0.65 ? PaymentMethod.Cash : PaymentMethod.VietQrSim) : PaymentMethod.VietQrSim,
                pos ? null : PickVoucher(), checkIn, true);
            var result = CreateSale(conn, tx, req);
            if (result != null && toVoid)
            {
                VoidPosBill(conn, tx, req, result);
                _voidedBills++;
            }
        }
    }

    private int GroupSize()
    {
        var r = _rnd.NextDouble();
        return r < 0.25 ? 1 : r < 0.70 ? 2 : r < 0.85 ? 3 : 4;
    }

    private CheckInMode PickCheckInMode(int tickets)
    {
        var r = _rnd.NextDouble();
        if (r < 0.88) return CheckInMode.Full;
        if (r < 0.93 && tickets > 1) return CheckInMode.Partial;
        return CheckInMode.None;
    }

    private DateTime? PosPaidAt(SeedShow show)
    {
        var date = PricingCalculator.OperatingDate(show.Start);
        if (!_shifts.TryGetValue((show.CinemaIndex, date), out var shift)) return null;
        var from = Max(shift.OpenedAt, show.Start.AddMinutes(-90));
        var to = Min(show.Start.AddMinutes(10), _now.AddMinutes(-1));
        if (to <= from) return null;
        return Trim(from.AddSeconds(_rnd.NextDouble() * (to - from).TotalSeconds));
    }

    private DateTime? OnlinePaidAt(SeedShow show)
    {
        var from = Max(show.Start.AddHours(-72), _today.AddDays(-HistoryDays - 3));
        var to = Min(show.Start.AddMinutes(-30), _now.AddMinutes(-2));
        if (to <= from) return null;
        return Trim(from.AddSeconds(_rnd.NextDouble() * (to - from).TotalSeconds));
    }

    private static DateTime Trim(DateTime t) => new(t.Ticks - t.Ticks % TimeSpan.TicksPerSecond, t.Kind);

    // ---------------------------------------------------------------- chọn ghế, đối tượng, món, khách, voucher
    private List<SeedSeat> PickSeats(SeedShow show, int count, bool sweetbox)
    {
        var free = show.Auditorium.Seats
            .Where(s => !s.House && !s.Maintenance && s.Type != SeatType.Wheelchair && !show.Taken.Contains(s.Id))
            .ToList();
        List<SeedSeat> chosen = new();
        if (sweetbox)
        {
            var pairs = free.Where(s => s.Type == SeatType.Sweetbox && s.Pair != null)
                .GroupBy(s => s.Pair!.Value).Where(g => g.Count() == 2).ToList();
            if (pairs.Count > 0) chosen = pairs[_rnd.Next(pairs.Count)].OrderBy(s => s.Col).ToList();
        }
        if (chosen.Count == 0)
        {
            var normal = free.Where(s => s.Type != SeatType.Sweetbox).ToList();
            var rows = normal.GroupBy(s => s.Row).OrderByDescending(g => g.Average(x => x.Score) + _rnd.NextDouble() * 30).ToList();
            foreach (var row in rows)
            {
                var cols = row.OrderBy(s => s.Col).ToList();
                for (int i = 0; i + count <= cols.Count && chosen.Count == 0; i++)
                {
                    bool contiguous = true;
                    for (int j = 1; j < count; j++) contiguous &= cols[i + j].Col == cols[i].Col + j;
                    if (contiguous) chosen = cols.Skip(i).Take(count).ToList();
                }
                if (chosen.Count > 0) break;
            }
            if (chosen.Count == 0) chosen = normal.OrderByDescending(s => s.Score + _rnd.NextDouble() * 20).Take(count).ToList();
        }
        foreach (var s in chosen) show.Taken.Add(s.Id);
        return chosen;
    }

    private static void ReleaseTaken(SeedShow show, IEnumerable<SeedSeat> seats)
    {
        foreach (var s in seats) show.Taken.Remove(s.Id);
    }

    /// <summary>Đối tượng vé tại quầy, theo đúng luật độ tuổi, giờ chiếu cho trẻ em và người giám hộ (SPEC 3.1).</summary>
    private List<string> PickAudiencePos(SeedShow show, int count)
    {
        var rating = show.Movie.AgeRating;
        var opDate = show.Plan.OperatingDate;
        bool night = show.Start.TimeOfDay < PricingCalculator.NightCutoff;
        bool u13Ok = (rating == AgeRating.P || rating == AgeRating.K) && !night && SchedulePlanner.EndsBefore(show.End, opDate, _rules.ChildU13LatestEnd);
        bool u16Ok = (rating == AgeRating.P || rating == AgeRating.K || rating == AgeRating.T13) && !night && SchedulePlanner.EndsBefore(show.End, opDate, _rules.ChildU16LatestEnd);
        var list = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var r = _rnd.NextDouble();
            string t = r < 0.70 ? AudienceType.Adult
                : r < 0.80 ? AudienceType.U22
                : r < 0.86 ? (u13Ok ? AudienceType.ChildU13 : AudienceType.Adult)
                : r < 0.89 ? (u16Ok ? AudienceType.Child13To15 : AudienceType.Adult)
                : r < 0.95 ? AudienceType.Senior
                : r < 0.965 ? AudienceType.Merit
                : r < 0.98 ? AudienceType.Hardship
                : r < 0.99 ? AudienceType.DisabledSevere
                : r < 0.995 ? AudienceType.DisabledExtreme
                : AudienceType.Adult;
            list.Add(t);
        }
        // Phim K: trẻ dưới 13 tuổi phải đi cùng người lớn (ADULT hoặc SENIOR)
        if (rating == AgeRating.K && list.Contains(AudienceType.ChildU13) && !list.Any(t => t == AudienceType.Adult || t == AudienceType.Senior))
            list[0] = AudienceType.Adult;
        return list;
    }

    /// <summary>Online chỉ bán vé ADULT và U22 (các loại giảm theo luật chỉ bán tại quầy).</summary>
    private List<string> PickAudienceOnline(int count) =>
        Enumerable.Range(0, count).Select(_ => _rnd.NextDouble() < 0.88 ? AudienceType.Adult : AudienceType.U22).ToList();

    private List<(SeedDrink Drink, int Qty)> PickFnb(double probability)
    {
        var lines = new List<(SeedDrink, int)>();
        if (_rnd.NextDouble() >= probability) return lines;
        int n = _rnd.NextDouble() < 0.7 ? 1 : 2;
        var combos = _drinks.Where(d => d.Spec.IsCombo).ToList();
        var singles = _drinks.Where(d => !d.Spec.IsCombo).ToList();
        for (int i = 0; i < n; i++)
        {
            var pool = _rnd.NextDouble() < 0.55 ? combos : singles;
            var d = pool[_rnd.Next(pool.Count)];
            if (lines.Any(l => l.Item1.Id == d.Id)) continue;
            lines.Add((d, d.Spec.IsCombo ? 1 : 1 + (_rnd.NextDouble() < 0.25 ? 1 : 0)));
        }
        return lines;
    }

    /// <summary>Khách ngẫu nhiên, trừ khách mẫu khach@ (khách mẫu chỉ nhận hóa đơn qua các bước có kiểm soát để giữ đúng hạng VIP).</summary>
    private int RandomCustomer()
    {
        int id;
        do id = _customerIds[_rnd.Next(_customerIds.Count)]; while (id == _khachId);
        return id;
    }

    private int PickOnlineCustomer()
    {
        _customerSpend.TryGetValue(_khachId, out var spend);
        if (spend < KhachVipTargetSpend && _rnd.NextDouble() < 0.03) return _khachId;
        return RandomCustomer();
    }

    private string? PickVoucher()
    {
        var r = _rnd.NextDouble();
        return r < 0.06 ? "GIAM10" : r < 0.09 ? "FPOLY50K" : null;
    }

    // ---------------------------------------------------------------- tạo một hóa đơn đã thanh toán
    private long TicketPrice(SeedShow show, SeedSeat seat, string audience)
    {
        var input = new PriceInput(show.Start, show.Feature, show.Auditorium.Spec.Format, seat.Type, show.Movie.IsBlockbuster, _discounts[audience]);
        return PricingCalculator.Calculate(input, _priceTables[show.CinemaIndex], _holidays, _rules).ListPrice;
    }

    private long VoucherDiscount(string? code, long subtotal, DateTime at, out Voucher? voucher)
    {
        voucher = null;
        if (code == null || !_vouchers.TryGetValue(code, out var v)) return 0;
        if (at < v.ValidFrom || at > v.ValidUntil || subtotal < v.MinBillAmount || v.UsedCount >= v.UsageLimit) return 0;
        long discount = v.Type == VoucherType.Percent ? subtotal * v.Value / 100 / 1000 * 1000 : v.Value;
        discount = Math.Min(discount, subtotal);
        if (discount <= 0) return 0;
        voucher = v;
        return discount;
    }

    /// <summary>Phân bổ giảm giá theo tỷ lệ giá trị từng dòng, phần dư dồn vào dòng cuối.</summary>
    private static long[] Allocate(long discount, IReadOnlyList<long> amounts)
    {
        var result = new long[amounts.Count];
        long total = amounts.Sum();
        if (discount <= 0 || total <= 0) return result;
        long used = 0;
        for (int i = 0; i < amounts.Count - 1; i++)
        {
            result[i] = discount * amounts[i] / total;
            used += result[i];
        }
        result[^1] = Math.Min(discount - used, amounts[^1]);
        return result;
    }

    private SaleResult? CreateSale(SqlConnection conn, SqlTransaction tx, SaleRequest req)
    {
        bool pos = req.Channel == BillChannel.Pos;
        int cinema = req.CinemaIndex;
        SeedShift? shift = null;
        if (pos && !_shifts.TryGetValue((cinema, PricingCalculator.OperatingDate(req.PaidAt)), out shift)) return null;

        var show = req.Show;
        var listPrices = show == null ? new List<long>() : req.Seats.Select((s, i) => TicketPrice(show, s, req.Audience[i])).ToList();
        var lineAmounts = req.Fnb.Select(l => l.Drink.Spec.Price * l.Qty).ToList();
        long ticketSub = listPrices.Sum(), fnbSub = lineAmounts.Sum();
        long discount = VoucherDiscount(req.VoucherCode, ticketSub + fnbSub, req.PaidAt, out var voucher);
        var alloc = Allocate(discount, listPrices.Concat(lineAmounts).ToList());
        long total = ticketSub + fnbSub - alloc.Sum();

        var created = req.PaidAt.AddMinutes(-(pos ? 1 + _rnd.Next(3) : 2 + _rnd.Next(5)));
        bool hasTickets = req.Seats.Count > 0;
        var checkedIn = new bool[req.Seats.Count];
        if (req.CheckIn == CheckInMode.Full) Array.Fill(checkedIn, true);
        else if (req.CheckIn == CheckInMode.Partial) for (int i = 0; i < checkedIn.Length - 1; i++) checkedIn[i] = true;
        int checkedCount = checkedIn.Count(x => x);

        string fnbStatus = Domain.Constants.FnbStatus.None;
        int? pickup = null;
        DateTime? prepAt = null;
        if (req.Fnb.Count > 0)
        {
            if (pos)
            {
                fnbStatus = req.PaidAt <= _now.AddMinutes(-10) ? Domain.Constants.FnbStatus.Fulfilled : Domain.Constants.FnbStatus.Preparing;
                pickup = NextPickup(cinema, PricingCalculator.OperatingDate(req.PaidAt));
            }
            else if (req.FnbPrepared && show != null)
            {
                var prep = Trim(show.Start.AddMinutes(-(5 + _rnd.Next(20))));
                if (prep > req.PaidAt && prep <= _now)
                {
                    prepAt = prep;
                    fnbStatus = Domain.Constants.FnbStatus.Fulfilled;
                    pickup = NextPickup(cinema, PricingCalculator.OperatingDate(prep));
                }
                else fnbStatus = Domain.Constants.FnbStatus.PendingPickup;
            }
            else fnbStatus = Domain.Constants.FnbStatus.PendingPickup;
        }

        var bill = new Bill
        {
            Code = NewBillCode(created),
            Channel = req.Channel,
            CinemaId = _cinemaIds[cinema],
            ShowtimeId = show?.Id,
            UserId = pos ? (_rnd.NextDouble() < 0.9 ? _cashierOf[cinema] : _shiftManagerOf[cinema]) : req.CustomerId,
            CustomerId = req.CustomerId,
            PosShiftId = shift?.Id,
            TerminalCode = pos ? Terminal : null,
            CreatedAt = created,
            TicketSubtotal = ticketSub,
            FnbSubtotal = fnbSub,
            DiscountTotal = alloc.Sum(),
            Total = total,
            VoucherId = voucher?.Id,
            PointsUsed = 0,
            HoldExpiresAt = hasTickets ? created.AddMinutes(_rules.SeatHoldMinutes) : null,
            CheckInStatus = !hasTickets ? BillCheckInStatus.None
                : checkedCount == 0 ? BillCheckInStatus.NotCheckedIn
                : checkedCount == req.Seats.Count ? BillCheckInStatus.FullyCheckedIn
                : BillCheckInStatus.PartiallyCheckedIn,
            FnbStatus = fnbStatus,
            PickupNumber = pickup,
            GuardianConfirmed = false,
            AgeConfirmed = !pos && show != null && show.Movie.AgeRating is AgeRating.T13 or AgeRating.T16 or AgeRating.T18,
        };
        int billId = BillDAL.Insert(bill, conn, tx);

        for (int i = 0; i < req.Seats.Count; i++)
        {
            DateTime? checkedAt = null;
            if (checkedIn[i] && show != null)
            {
                checkedAt = Min(Trim(show.Start.AddMinutes(-10 + _rnd.Next(22))), _now.AddMinutes(-1));
                if (checkedAt < req.PaidAt) checkedAt = req.PaidAt.AddMinutes(1);
            }
            int ticketId = TicketDAL.Insert(new Ticket
            {
                BillId = billId,
                ShowtimeId = show!.Id,
                SeatId = req.Seats[i].Id,
                AudienceType = req.Audience[i],
                ListPrice = listPrices[i],
                FinalPrice = listPrices[i] - alloc[i],
                VatRate = _rules.TicketVatPercent,
                QrCode = NewQrCode(),
                CheckInStatus = checkedIn[i] ? TicketCheckInStatus.CheckedIn : TicketCheckInStatus.NotCheckedIn,
                CheckedInAt = checkedAt,
                CheckedInBy = checkedIn[i] ? _gateOf[cinema] : null,
                CreatedAt = req.PaidAt,
            }, conn, tx);
            // Ghế của vé vừa tạo chuyển BOOKED, gắn TicketId (cập nhật theo khóa chính ShowtimeId, SeatId)
            ShowtimeSeatDAL.Update(new ShowtimeSeat { ShowtimeId = show.Id, SeatId = req.Seats[i].Id, Status = ShowtimeSeatStatus.Booked, TicketId = ticketId }, conn, tx);
        }

        for (int i = 0; i < req.Fnb.Count; i++)
        {
            var (drink, qty) = req.Fnb[i];
            BillDetailDAL.Insert(new BillDetail
            {
                BillId = billId,
                DrinkId = drink.Id,
                Quantity = qty,
                Price = drink.Spec.Price,
                VatRate = _rules.FnbVatPercent,
                LineFinalAmount = lineAmounts[i] - alloc[listPrices.Count + i],
            }, conn, tx);
        }

        if (total > 0)
        {
            PaymentDAL.Insert(new Payment
            {
                BillId = billId,
                PosShiftId = shift?.Id,
                Method = req.PaymentMethod,
                Direction = PaymentDirection.In,
                Amount = total,
                Status = PaymentStatus.Succeeded,
                ProviderTxnId = req.PaymentMethod == PaymentMethod.VietQrSim ? "SIM" + Guid.NewGuid().ToString("N")[..17].ToUpperInvariant() : null,
                RawPayload = null,
                CreatedAt = req.PaidAt,
            }, conn, tx);
        }

        // Đổi trạng thái qua BillStateMachine: quầy do thu ngân xác nhận, online do webhook (hệ thống)
        _status.ChangeStatus(billId, BillStatus.Finish, null, pos ? Roles.POSCashier : Actors.System, req.PaidAt, conn, tx);

        if (req.Fnb.Count > 0)
        {
            if (pos)
            {
                InventoryTransactionDAL.InsertForBill(billId, _cinemaIds[cinema], InventoryTxnType.Consume, "Bán tại quầy", bill.UserId, req.PaidAt, conn, tx);
            }
            else
            {
                InventoryTransactionDAL.InsertForBill(billId, _cinemaIds[cinema], InventoryTxnType.Reserve, "Giữ nguyên liệu cho đơn online", null, req.PaidAt, conn, tx);
                if (prepAt != null)
                {
                    InventoryTransactionDAL.InsertForBill(billId, _cinemaIds[cinema], InventoryTxnType.Release, "Khách đã tới rạp", null, prepAt.Value, conn, tx);
                    InventoryTransactionDAL.InsertForBill(billId, _cinemaIds[cinema], InventoryTxnType.Consume, "Làm món cho đơn online", null, prepAt.Value, conn, tx);
                }
            }
        }

        if (req.CustomerId is int cid)
        {
            _customerBills.Add(new CustomerBill(cid, billId, bill.Code, req.PaidAt, total, null));
            _customerSpend[cid] = (_customerSpend.TryGetValue(cid, out var s) ? s : 0) + total;
        }
        if (voucher != null) voucher.UsedCount++;
        if (shift != null)
        {
            if (req.PaymentMethod == PaymentMethod.Cash) shift.CashIn += total;
            if (req.PaidAt > shift.LastActivity) shift.LastActivity = req.PaidAt;
        }
        return new SaleResult(billId, bill.Code, total, req.PaidAt, cinema, shift);
    }

    private string NewQrCode()
    {
        string qr;
        do qr = CodeGenerator.TicketQrCode(); while (!_qrCodes.Add(qr));
        return qr;
    }

    // ---------------------------------------------------------------- hủy hóa đơn đã thanh toán tại quầy (quản lý ca duyệt bằng PIN)
    private void VoidPosBill(SqlConnection conn, SqlTransaction tx, SaleRequest req, SaleResult sale)
    {
        int cinema = req.CinemaIndex;
        var requestedAt = sale.PaidAt.AddMinutes(3);
        var decidedAt = sale.PaidAt.AddMinutes(5);
        SupervisorApprovalDAL.Insert(new SupervisorApproval
        {
            Type = ApprovalType.VoidBill,
            BillId = sale.BillId,
            RequestedBy = _cashierOf[cinema],
            Reason = "Khách đổi ý trước giờ chiếu, xin hoàn tiền",
            Status = ApprovalStatus.Approved,
            Method = ApprovalMethod.Pin,
            DecidedBy = _shiftManagerOf[cinema],
            DecidedAt = decidedAt,
            ExpiresAt = requestedAt.AddMinutes(_rules.ApprovalRequestExpireMinutes),
            CreatedAt = requestedAt,
        }, conn, tx);

        _status.ChangeStatus(sale.BillId, BillStatus.Cancel, CancelReason.VoidedByPos, Roles.ShiftManager, decidedAt, conn, tx);
        ShowtimeSeatDAL.ReleaseForBill(sale.BillId, conn, tx);
        TicketDAL.SetCheckInStatusForBill(sale.BillId, TicketCheckInStatus.Voided, conn, tx);
        var bill = BillDAL.GetById(sale.BillId, conn, tx)!;
        bill.CheckInStatus = BillCheckInStatus.Voided;
        BillDAL.Update(bill, conn, tx);
        if (req.Show != null) ReleaseTaken(req.Show, req.Seats);

        if (sale.Total > 0)
        {
            PaymentDAL.Insert(new Payment
            {
                BillId = sale.BillId,
                PosShiftId = sale.Shift?.Id,
                Method = req.PaymentMethod,
                Direction = PaymentDirection.Refund,
                Amount = sale.Total,
                Status = PaymentStatus.Succeeded,
                ProviderTxnId = req.PaymentMethod == PaymentMethod.VietQrSim ? "SIM" + Guid.NewGuid().ToString("N")[..17].ToUpperInvariant() : null,
                CreatedAt = decidedAt,
            }, conn, tx);
            if (sale.Shift != null && req.PaymentMethod == PaymentMethod.Cash) sale.Shift.CashRefund += sale.Total;
        }
        AuditLogDAL.Insert(new AuditLog
        {
            UserId = _shiftManagerOf[cinema],
            Action = AuditAction.VoidBill,
            EntityType = "Bill",
            EntityId = sale.BillId.ToString(),
            Detail = $"Hủy hóa đơn {sale.Code} đã thanh toán, duyệt bằng PIN",
            CreatedAt = decidedAt,
        }, conn, tx);
    }

    // ---------------------------------------------------------------- bắp nước mua riêng tại quầy
    private void SellFnbOnly(SqlConnection conn, SqlTransaction tx)
    {
        for (int c = 0; c < _cinemaIds.Count; c++)
        {
            for (int d = -HistoryDays; d <= 0; d++)
            {
                var date = _today.AddDays(d);
                if (!_shifts.TryGetValue((c, date), out var shift)) continue;
                bool weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                int count = 3 + _rnd.Next(4) + (weekend ? 2 : 0);
                for (int i = 0; i < count; i++)
                {
                    var at = Trim(date.AddHours(9.5).AddSeconds(_rnd.NextDouble() * 12.5 * 3600));
                    if (at < shift.OpenedAt || at > _now.AddMinutes(-1)) continue;
                    var fnb = PickFnb(1.0);
                    if (fnb.Count == 0) continue;
                    int? customer = _rnd.NextDouble() < 0.1 ? RandomCustomer() : null;
                    CreateSale(conn, tx, new SaleRequest(c, null, BillChannel.Pos, customer, new List<SeedSeat>(), new List<string>(), fnb, at,
                        _rnd.NextDouble() < 0.7 ? PaymentMethod.Cash : PaymentMethod.VietQrSim, null, CheckInMode.None, true));
                }
            }
        }
    }

    // ---------------------------------------------------------------- khách mẫu đạt hạng VIP
    private void TopUpKhachToVip(SqlConnection conn, SqlTransaction tx)
    {
        var candidates = _shows.Where(s => s.Start < _now.AddHours(-3) && s.Start > _today.AddDays(-25)).OrderBy(_ => _rnd.Next()).ToList();
        foreach (var show in candidates)
        {
            _customerSpend.TryGetValue(_khachId, out var spend);
            if (spend >= KhachVipTargetSpend - 200_000) break;
            var seats = PickSeats(show, 2, false);
            if (seats.Count < 2) { ReleaseTaken(show, seats); continue; }
            var paidAt = OnlinePaidAt(show);
            if (paidAt == null) { ReleaseTaken(show, seats); continue; }
            var combo = _drinks.First(d => d.Spec.Name == "Combo Couple");
            CreateSale(conn, tx, new SaleRequest(show.CinemaIndex, show, BillChannel.Online, _khachId, seats, PickAudienceOnline(2),
                new List<(SeedDrink, int)> { (combo, 1) }, paidAt.Value, PaymentMethod.VietQrSim, null, CheckInMode.Full, true));
        }
    }

    // ---------------------------------------------------------------- suất sắp chiếu đã có vé (bị khóa)
    private void SellUpcoming(SqlConnection conn, SqlTransaction tx, SeedShow show)
    {
        int bills = 1 + _rnd.Next(3);
        for (int b = 0; b < bills; b++)
        {
            var seats = PickSeats(show, GroupSize(), false);
            if (seats.Count == 0) return;
            var opDate = show.Plan.OperatingDate;
            bool pos = opDate == _today && _rnd.NextDouble() < 0.3;
            DateTime paidAt;
            if (pos)
            {
                var shift = _shifts[(show.CinemaIndex, _today)];
                var span = (_now.AddMinutes(-1) - shift.OpenedAt).TotalSeconds;
                if (span <= 60) pos = false;
                paidAt = pos ? Trim(shift.OpenedAt.AddSeconds(_rnd.NextDouble() * span)) : default;
            }
            else paidAt = default;
            if (!pos)
            {
                var from = Max(_now.AddHours(-48), _today.AddDays(-2));
                paidAt = Trim(from.AddSeconds(_rnd.NextDouble() * Math.Max(60, (_now.AddMinutes(-2) - from).TotalSeconds)));
            }
            var req = new SaleRequest(show.CinemaIndex, show, pos ? BillChannel.Pos : BillChannel.Online,
                pos ? null : PickOnlineCustomer(), seats, pos ? PickAudiencePos(show, seats.Count) : PickAudienceOnline(seats.Count),
                pos ? new List<(SeedDrink, int)>() : PickFnb(0.35), paidAt,
                pos ? PaymentMethod.Cash : PaymentMethod.VietQrSim, pos ? null : PickVoucher(), CheckInMode.None, false);
            if (CreateSale(conn, tx, req) == null) ReleaseTaken(show, seats);
        }
    }

    /// <summary>Khách mẫu có vé sắp chiếu: 2 ghế VIP suất tối tại rạp 1, kèm Combo Couple chờ nhận.</summary>
    private void SellKhachUpcoming(SqlConnection conn, SqlTransaction tx)
    {
        var show = _shows
            .Where(s => s.CinemaIndex == 0 && s.Start >= _now.AddHours(3) && s.Feature.TimeOfDay >= new TimeSpan(17, 0, 0) && s.Feature.TimeOfDay < new TimeSpan(22, 0, 0))
            .OrderBy(s => s.Start)
            .FirstOrDefault(s => s.Auditorium.Seats.Count(x => x.Type == SeatType.Vip && !x.House && !x.Maintenance && !s.Taken.Contains(x.Id)) >= 2)
            ?? _shows.Where(s => s.Start >= _now.AddHours(3)).OrderBy(s => s.Start).First();
        var vip = show.Auditorium.Seats
            .Where(x => x.Type == SeatType.Vip && !x.House && !x.Maintenance && !show.Taken.Contains(x.Id))
            .GroupBy(x => x.Row)
            .SelectMany(g => g.OrderBy(x => x.Col).Zip(g.OrderBy(x => x.Col).Skip(1), (a, b) => (a, b)))
            .Where(p => p.b.Col == p.a.Col + 1)
            .OrderByDescending(p => p.a.Score + p.b.Score)
            .Select(p => new List<SeedSeat> { p.a, p.b })
            .FirstOrDefault();
        var seats = vip ?? PickSeats(show, 2, false);
        foreach (var s in seats) show.Taken.Add(s.Id);
        var combo = _drinks.First(d => d.Spec.Name == "Combo Couple");
        CreateSale(conn, tx, new SaleRequest(show.CinemaIndex, show, BillChannel.Online, _khachId, seats,
            new List<string> { AudienceType.Adult, AudienceType.Adult }, new List<(SeedDrink, int)> { (combo, 1) },
            Trim(_now.AddHours(-26)), PaymentMethod.VietQrSim, null, CheckInMode.None, false));
    }

    // ---------------------------------------------------------------- hóa đơn hủy với các lý do khác nhau
    private void SeedCancellations(SqlConnection conn, SqlTransaction tx)
    {
        // Hết hạn giữ ghế mà chưa thanh toán (online, hệ thống tự hủy)
        var pastShows = _shows.Where(s => s.Start < _now && s.Start > _today.AddDays(-HistoryDays + 1)).ToList();
        for (int i = 0; i < 14; i++)
        {
            var show = pastShows[_rnd.Next(pastShows.Count)];
            var created = OnlinePaidAt(show);
            if (created == null) continue;
            int seatsWanted = 1 + _rnd.Next(3);
            var seats = PickSeats(show, seatsWanted, false);
            long sub = seats.Sum(s => TicketPrice(show, s, AudienceType.Adult));
            ReleaseTaken(show, seats);
            int customer = PickOnlineCustomer();
            int id = InsertWaitingBill(conn, tx, show, BillChannel.Online, customer, customer, null, created.Value, sub);
            _status.ChangeStatus(id, BillStatus.Cancel, CancelReason.Expired, Actors.System, created.Value.AddMinutes(_rules.SeatHoldMinutes), conn, tx);
        }

        // Hủy trước khi thanh toán: thu ngân hủy tại quầy, hoặc khách tự hủy hóa đơn online của mình
        for (int i = 0; i < 10; i++)
        {
            var show = pastShows[_rnd.Next(pastShows.Count)];
            bool pos = i < 7;
            DateTime? created = pos ? PosPaidAt(show) : OnlinePaidAt(show);
            if (created == null) continue;
            var seats = PickSeats(show, 1 + _rnd.Next(2), false);
            long sub = seats.Sum(s => TicketPrice(show, s, AudienceType.Adult));
            ReleaseTaken(show, seats);
            int? customer = pos ? null : PickOnlineCustomer();
            int? shiftId = pos ? _shifts[(show.CinemaIndex, PricingCalculator.OperatingDate(created.Value))].Id : null;
            int id = InsertWaitingBill(conn, tx, show, pos ? BillChannel.Pos : BillChannel.Online, pos ? _cashierOf[show.CinemaIndex] : customer, customer, shiftId, created.Value, sub);
            _status.ChangeStatus(id, BillStatus.Cancel, CancelReason.CancelledBeforePayment, pos ? Roles.POSCashier : Roles.Customer, created.Value.AddMinutes(2), conn, tx);
        }

        // Khách tự hủy vé đã thanh toán (đủ điều kiện SPEC 3.9): hoàn 100% vào ví, thu hồi điểm
        SelfCancel(conn, tx, _khachId, 12);
        SelfCancel(conn, tx, _khach2Id, 8);
    }

    private int InsertWaitingBill(SqlConnection conn, SqlTransaction tx, SeedShow show, string channel, int? userId, int? customerId, int? shiftId, DateTime created, long ticketSubtotal)
    {
        return BillDAL.Insert(new Bill
        {
            Code = NewBillCode(created),
            Channel = channel,
            CinemaId = _cinemaIds[show.CinemaIndex],
            ShowtimeId = show.Id,
            UserId = userId,
            CustomerId = customerId,
            PosShiftId = shiftId,
            TerminalCode = channel == BillChannel.Pos ? Terminal : null,
            CreatedAt = created,
            TicketSubtotal = ticketSubtotal,
            Total = ticketSubtotal,
            HoldExpiresAt = created.AddMinutes(_rules.SeatHoldMinutes),
            CheckInStatus = BillCheckInStatus.None,
            FnbStatus = Domain.Constants.FnbStatus.None,
        }, conn, tx);
    }

    private void SelfCancel(SqlConnection conn, SqlTransaction tx, int customerId, int daysAgo)
    {
        var show = _shows.Where(s => s.CinemaIndex == 0 && s.Plan.OperatingDate == _today.AddDays(-daysAgo) && s.Feature.TimeOfDay >= new TimeSpan(18, 0, 0))
            .OrderBy(s => s.Start).FirstOrDefault()
            ?? _shows.First(s => s.Start < _now.AddDays(-2));
        var seats = PickSeats(show, 2, false);
        if (seats.Count == 0) return;
        var paidAt = Trim(show.Start.AddDays(-3).AddHours(-2));
        var cancelAt = Trim(show.Start.AddDays(-1));
        var combo = _drinks.First(d => d.Spec.Name == "Combo Solo");
        var req = new SaleRequest(show.CinemaIndex, show, BillChannel.Online, customerId, seats, PickAudienceOnline(seats.Count),
            new List<(SeedDrink, int)> { (combo, 1) }, paidAt, PaymentMethod.VietQrSim, null, CheckInMode.None, false);
        var sale = CreateSale(conn, tx, req);
        if (sale == null) return;

        _status.ChangeStatus(sale.BillId, BillStatus.Cancel, CancelReason.CancelledByUser, Roles.Customer, cancelAt, conn, tx);
        ShowtimeSeatDAL.ReleaseForBill(sale.BillId, conn, tx);
        TicketDAL.SetCheckInStatusForBill(sale.BillId, TicketCheckInStatus.Voided, conn, tx);
        ReleaseTaken(show, seats);
        var bill = BillDAL.GetById(sale.BillId, conn, tx)!;
        bill.CheckInStatus = BillCheckInStatus.Voided;
        bill.FnbStatus = Domain.Constants.FnbStatus.Voided;
        BillDAL.Update(bill, conn, tx);
        InventoryTransactionDAL.InsertForBill(sale.BillId, _cinemaIds[show.CinemaIndex], InventoryTxnType.Release, "Khách tự hủy vé", null, cancelAt, conn, tx);

        PaymentDAL.Insert(new Payment
        {
            BillId = sale.BillId,
            Method = PaymentMethod.Wallet,
            Direction = PaymentDirection.Refund,
            Amount = sale.Total,
            Status = PaymentStatus.Succeeded,
            CreatedAt = cancelAt,
        }, conn, tx);
        var user = UserDAL.GetById(customerId, conn, tx)!;
        user.WalletBalance += sale.Total;
        UserDAL.Update(user, conn, tx);
        WalletTransactionDAL.Insert(new WalletTransaction
        {
            UserId = customerId,
            Amount = sale.Total,
            BalanceAfter = user.WalletBalance,
            Reason = $"Hoàn tiền tự hủy vé hóa đơn {sale.Code}",
            BillId = sale.BillId,
            CreatedAt = cancelAt,
        }, conn, tx);

        int idx = _customerBills.FindIndex(b => b.BillId == sale.BillId);
        if (idx >= 0) _customerBills[idx] = _customerBills[idx] with { CancelledAt = cancelAt };
        _customerSpend[customerId] -= sale.Total;
    }
}
