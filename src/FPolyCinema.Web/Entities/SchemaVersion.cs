namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng SchemaVersions: phiên bản script CSDL đã chạy.</summary>
public class SchemaVersion
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
}
