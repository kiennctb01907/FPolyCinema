using FPolyCinema.Web.Services;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Phần thêm của A5 ở Phase 7: chế độ xem (Bảng hoặc Gantt), lựa chọn cho hộp "AI tự động xếp lịch", dữ liệu khối suất cho Gantt.</summary>
public class SchedulerPhase7Vm
{
    /// <summary>"table" hoặc "gantt".</summary>
    public string View { get; set; } = "table";
    public AiSchedulerChoices Ai { get; set; } = new(new(), new());
    /// <summary>Dữ liệu Gantt gửi cho gantt.js (Json.Serialize).</summary>
    public object Gantt { get; set; } = new { };
}
