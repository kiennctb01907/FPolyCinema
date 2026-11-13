using System.Globalization;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Một ô của bảng giá: chiều (RuleType), khóa (RuleKey), số tiền.</summary>
public sealed record PriceCell(string RuleType, string RuleKey, long Amount);

/// <summary>Dữ liệu trang thử tính giá.</summary>
public sealed record PriceTryInput(int CinemaId, DateTime StartTime, string AuditoriumFormat, string SeatType, bool IsBlockbuster, string AudienceType);

/// <summary>
/// Bảng giá 6 chiều theo rạp, đối tượng ưu đãi và ngày lễ (UC33, SPEC 3.1 mục 5, 3.4):
/// - Sửa bảng giá ghi AuditLogs (PRICING_UPDATED) kèm từng dòng đổi (SPEC 2.7).
/// - Mức giảm của đối tượng không được thấp hơn mức tối thiểu theo luật (SPEC 3.1 mục 5).
/// - Trang thử tính giá dùng PricingCalculator với bảng giá và ngày lễ trong CSDL.
/// </summary>
public class PricingService
{
    public const long MaxAbsAmount = 10_000_000;
    private readonly CinemaRules _rules;

    public PricingService(CinemaRules rules) => _rules = rules;

    /// <summary>Đủ 6 chiều và mọi khóa hợp lệ của một rạp; dòng chưa có trong CSDL hiện số 0.</summary>
    public List<PriceCell> Table(int cinemaId)
    {
        var saved = PricingRuleDAL.GetByCinema(cinemaId).ToDictionary(r => (r.RuleType, r.RuleKey), r => r.Amount);
        return PricingRuleType.All.SelectMany(t => PricingRuleKey.For(t).Select(k => new PriceCell(t, k, saved.TryGetValue((t, k), out var v) ? v : 0))).ToList();
    }

    /// <summary>Lưu bảng giá của rạp và ghi AuditLogs những dòng thay đổi. Trả về số dòng đã đổi.</summary>
    public int SaveTable(int cinemaId, IReadOnlyList<PriceCell> cells, int? userId, DateTime now)
    {
        if (CinemaDAL.GetById(cinemaId) == null) throw CategoryService.NotFound();
        var errors = new Dictionary<string, string>();
        var input = new Dictionary<(string, string), long>();
        foreach (var c in cells)
        {
            var field = $"{c.RuleType}.{c.RuleKey}";
            if (!PricingRuleKey.IsValid(c.RuleType, c.RuleKey)) { errors[field] = "Dòng giá không hợp lệ. Bạn tải lại trang."; continue; }
            if (Math.Abs(c.Amount) > MaxAbsAmount) errors[field] = "Số tiền quá lớn, bạn kiểm tra lại.";
            input[(c.RuleType, c.RuleKey)] = c.Amount;
        }
        if (input.TryGetValue((PricingRuleType.Base, PricingRuleKey.Default), out var baseAmount) && baseAmount <= 0)
            errors[$"{PricingRuleType.Base}.{PricingRuleKey.Default}"] = "Giá gốc phải lớn hơn 0 đồng.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);

        return DBUtil.InTransaction((conn, tx) =>
        {
            var existing = PricingRuleDAL.GetByCinema(cinemaId, conn, tx).ToDictionary(r => (r.RuleType, r.RuleKey));
            var changes = new List<string>();
            foreach (var ((type, key), amount) in input)
            {
                if (existing.TryGetValue((type, key), out var rule))
                {
                    if (rule.Amount == amount) continue;
                    changes.Add($"{type}/{key}: {rule.Amount.ToString(CultureInfo.InvariantCulture)} → {amount.ToString(CultureInfo.InvariantCulture)}");
                    rule.Amount = amount;
                    PricingRuleDAL.Update(rule, conn, tx);
                }
                else
                {
                    changes.Add($"{type}/{key}: (chưa có) → {amount.ToString(CultureInfo.InvariantCulture)}");
                    PricingRuleDAL.Insert(new PricingRule { CinemaId = cinemaId, RuleType = type, RuleKey = key, Amount = amount }, conn, tx);
                }
            }
            if (changes.Count > 0)
                AuditLogDAL.Insert(new AuditLog
                {
                    UserId = userId, Action = AuditAction.PricingUpdated, EntityType = "PricingRules", EntityId = cinemaId.ToString(CultureInfo.InvariantCulture),
                    Detail = Truncate("Rạp " + cinemaId.ToString(CultureInfo.InvariantCulture) + ": " + string.Join("; ", changes)), CreatedAt = now,
                }, conn, tx);
            return changes.Count;
        });
    }

    public List<AudienceDiscount> Discounts() =>
        AudienceDiscountDAL.GetAll().OrderBy(d => Array.IndexOf(AudienceType.All, d.AudienceType)).ToList();

    /// <summary>
    /// Lưu mức giảm theo đối tượng. Không cho lưu mức thấp hơn mức tối thiểu theo luật (SPEC 3.1 mục 5) hoặc ngoài 0–100%.
    /// Đổi mức nào thì ghi AuditLogs (PRICING_UPDATED, EntityType AudienceDiscounts).
    /// </summary>
    public int SaveDiscounts(IReadOnlyDictionary<string, decimal> percents, int? userId, DateTime now)
    {
        var current = AudienceDiscountDAL.GetAll().ToDictionary(d => d.AudienceType);
        var errors = new Dictionary<string, string>();
        foreach (var (type, value) in percents)
        {
            var field = $"Discounts[{type}]";
            if (!current.TryGetValue(type, out var d)) { errors[field] = "Đối tượng không hợp lệ. Bạn tải lại trang."; continue; }
            if (value < 0 || value > 100) errors[field] = "Mức giảm phải từ 0 đến 100%.";
            else if (value < d.LegalMinPercent)
                errors[field] = $"{StatusDisplay.Of(StatusDisplay.Audience, type).Label}: theo luật phải giảm ít nhất {Fmt.Decimal(d.LegalMinPercent)}%. Bạn nhập mức từ {Fmt.Decimal(d.LegalMinPercent)}% trở lên.";
        }
        if (errors.Count > 0) throw FieldErrors.Of(errors);

        return DBUtil.InTransaction((conn, tx) =>
        {
            var changes = new List<string>();
            foreach (var (type, value) in percents)
            {
                var d = AudienceDiscountDAL.GetById(type, conn, tx)!;
                var v = Math.Round(value, 2);
                if (d.DiscountPercent == v) continue;
                changes.Add($"{type}: {d.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture)}% → {v.ToString("0.##", CultureInfo.InvariantCulture)}%");
                d.DiscountPercent = v;
                AudienceDiscountDAL.Update(d, conn, tx);
            }
            if (changes.Count > 0)
                AuditLogDAL.Insert(new AuditLog { UserId = userId, Action = AuditAction.PricingUpdated, EntityType = "AudienceDiscounts", Detail = Truncate(string.Join("; ", changes)), CreatedAt = now }, conn, tx);
            return changes.Count;
        });
    }

    public List<Holiday> Holidays(int year) => HolidayDAL.GetByYear(year);

    public List<int> HolidayYears() => HolidayDAL.Years();

    public Holiday AddHoliday(DateTime? date, string? name)
    {
        var errors = new Dictionary<string, string>();
        var n = (name ?? string.Empty).Trim();
        if (date is null) errors["HolidayDate"] = "Bạn chọn ngày lễ.";
        else if (HolidayDAL.GetById(date.Value.Date) != null) errors["HolidayDate"] = $"Ngày {Fmt.Date(date.Value)} đã có trong danh sách ngày lễ.";
        if (n.Length == 0) errors["Name"] = "Bạn nhập tên ngày lễ, ví dụ Quốc khánh.";
        else if (n.Length > 150) errors["Name"] = "Tên ngày lễ tối đa 150 ký tự.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        var h = new Holiday { HolidayDate = date!.Value.Date, Name = n };
        HolidayDAL.Insert(h);
        return h;
    }

    public void DeleteHoliday(DateTime date)
    {
        if (HolidayDAL.Delete(date.Date) == 0) throw CategoryService.NotFound();
    }

    /// <summary>Thử tính giá một vé theo bảng giá, ngày lễ và mức giảm đối tượng hiện có (PricingCalculator).</summary>
    public PriceBreakdown Try(PriceTryInput input)
    {
        var errors = new Dictionary<string, string>();
        if (CinemaDAL.GetById(input.CinemaId) == null) errors["CinemaId"] = "Bạn chọn rạp.";
        if (!AuditoriumFormat.All.Contains(input.AuditoriumFormat)) errors["AuditoriumFormat"] = "Bạn chọn định dạng phòng.";
        if (!SeatType.All.Contains(input.SeatType)) errors["SeatType"] = "Bạn chọn loại ghế.";
        var discount = AudienceDiscountDAL.GetById(input.AudienceType);
        if (discount == null) errors["AudienceType"] = "Bạn chọn đối tượng vé.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);

        var table = new PriceTable();
        foreach (var c in Table(input.CinemaId)) table.Set(c.RuleType, c.RuleKey, c.Amount);
        var start = ShowtimeTimeCalculator.RoundToNearest(input.StartTime, _rules.ScheduleRoundMinutes);
        var holidays = HolidayDAL.GetAll().Select(h => h.HolidayDate);
        return PricingCalculator.Calculate(new PriceInput(start, start.AddMinutes(_rules.AdsMinutes), input.AuditoriumFormat, input.SeatType, input.IsBlockbuster, discount!.DiscountPercent),
            table, holidays, _rules);
    }

    private static string Truncate(string s) => s.Length <= 2000 ? s : s[..1997] + "…";
}
