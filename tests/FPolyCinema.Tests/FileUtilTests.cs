using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Http;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho Utils.FileUtil (SPEC 2.6): kiểm phần mở rộng, chữ ký đầu file, dung lượng; chỉ xóa trong uploads.</summary>
[Collection("FileUtil")]
public sealed class FileUtilTests : IDisposable
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3 };
    private static readonly byte[] Jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0, 1 };
    private static readonly byte[] Gif = "GIF89a\u0001\u0000\u0001\u0000\u0000\u0000"u8.ToArray();
    private static readonly byte[] Webp = "RIFF\u0010\u0000\u0000\u0000WEBPVP8 "u8.ToArray();

    private readonly string _root = Path.Combine(Path.GetTempPath(), "fpc-fileutil-" + Guid.NewGuid().ToString("N"));

    public FileUtilTests()
    {
        Directory.CreateDirectory(_root);
        FileUtil.Clock = new FakeClock(new DateTime(2026, 1, 2, 3, 4, 5));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private static IFormFile Form(string name, byte[] content, long? length = null)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, length ?? content.Length, "file", name);
    }

    [Theory]
    [InlineData("poster.png")]
    [InlineData("poster.JPG")]
    [InlineData("poster.jpeg")]
    [InlineData("poster.gif")]
    [InlineData("poster.webp")]
    public void Upload_ValidImage_SavedWithTimestampAndShortGuid(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var bytes = ext switch { ".png" => Png, ".gif" => Gif, ".webp" => Webp, _ => Jpg };
        var path = FileUtil.Upload(Form(fileName, bytes), _root);
        Assert.Matches($"^/uploads/20260102030405_[0-9a-f]{{8}}\\{ext}$", path);
        var saved = Path.Combine(_root, "uploads", path.Split('/').Last());
        Assert.True(File.Exists(saved));
        Assert.Equal(bytes, File.ReadAllBytes(saved));
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("anh.svg")]
    [InlineData("khongcoduoi")]
    public void Upload_RejectsOtherExtensions(string fileName)
    {
        var ex = Assert.Throws<FileUploadException>(() => FileUtil.Upload(Form(fileName, Png), _root));
        Assert.Contains(".jpg", ex.Message);
    }

    [Fact]
    public void Upload_RejectsSignatureMismatch()
    {
        // Đuôi .jpg nhưng nội dung là PNG; đuôi .png nhưng nội dung là chữ
        Assert.Throws<FileUploadException>(() => FileUtil.Upload(Form("anh.jpg", Png), _root));
        Assert.Throws<FileUploadException>(() => FileUtil.Upload(Form("anh.png", "<html>"u8.ToArray()), _root));
    }

    [Fact]
    public void Upload_RejectsEmptyAndOver5MB()
    {
        Assert.Throws<FileUploadException>(() => FileUtil.Upload(Form("anh.png", Array.Empty<byte>()), _root));
        var big = new byte[FileUtil.MaxBytes + 1];
        Png.CopyTo(big, 0);
        var ex = Assert.Throws<FileUploadException>(() => FileUtil.Upload(Form("anh.png", big), _root));
        Assert.Contains("5MB", ex.Message);
    }

    [Fact]
    public void Delete_RemovesUploadedFile()
    {
        var path = FileUtil.Upload(Form("anh.png", Png), _root);
        Assert.True(FileUtil.Delete(path, _root));
        Assert.False(File.Exists(Path.Combine(_root, "uploads", path.Split('/').Last())));
        Assert.False(FileUtil.Delete(path, _root));
    }

    [Fact]
    public void Delete_OnlyInsideUploadsFolder()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), "{}");
        Directory.CreateDirectory(Path.Combine(_root, "uploads", "seed"));
        File.WriteAllText(Path.Combine(_root, "uploads", "seed", "poster-01.svg"), "<svg/>");

        Assert.False(FileUtil.Delete("../appsettings.json", _root));
        Assert.False(FileUtil.Delete("/uploads/../appsettings.json", _root));
        Assert.False(FileUtil.Delete("..\\appsettings.json", _root));
        Assert.False(FileUtil.Delete("/uploads/seed/poster-01.svg", _root));
        Assert.False(FileUtil.Delete("", _root));
        Assert.True(File.Exists(Path.Combine(_root, "appsettings.json")));
        Assert.True(File.Exists(Path.Combine(_root, "uploads", "seed", "poster-01.svg")));
    }

    [Fact]
    public void SignatureMatches_KnownFormats()
    {
        Assert.True(FileUtil.SignatureMatches(".png", Png));
        Assert.True(FileUtil.SignatureMatches(".jpeg", Jpg));
        Assert.True(FileUtil.SignatureMatches(".gif", Gif));
        Assert.True(FileUtil.SignatureMatches(".webp", Webp));
        Assert.False(FileUtil.SignatureMatches(".gif", Png));
        Assert.False(FileUtil.SignatureMatches(".bmp", Png));
    }
}
