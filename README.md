# FPolyCinema

Website quản lý chuỗi rạp chiếu phim – đồ án môn Dự án .NET 1 (FPT Polytechnic).
ASP.NET Core MVC .NET 8, Razor View, ADO.NET (Utils.DBUtil và các lớp DAL), SQL Server, SignalR.

Tài liệu: [docs/README.md](docs/README.md) (spec, giao diện, CONTRACTS), tiến độ ở [PROGRESS.md](PROGRESS.md).
Hướng dẫn cài đặt, publish và chạy sau Nginx trên Linux được viết đầy đủ ở Phase 8.

## Chạy nhanh

1. Cài .NET 8 SDK (global.json ghim SDK 8.0.x), SQL Server 2019+ (bản Express được), Node.js LTS.
2. Sửa chuỗi kết nối ở `src/FPolyCinema.Web/appsettings.Development.json` (khóa `ConnectionStrings:Default`)
   hoặc đặt biến môi trường `ConnectionStrings__Default`.
3. Tạo DB, bảng, stored procedure và dữ liệu mẫu:
   ```
   dotnet run --project src/FPolyCinema.Web -- --reset-db --init-only
   ```
4. Chạy web: `dotnet run --project src/FPolyCinema.Web`, mở http://localhost:5080.
5. Test: `dotnet test`.

Tài khoản mẫu ở [docs/CONTRACTS.md](docs/CONTRACTS.md) mục 11 (mật khẩu 123456, PIN quản lý 246810).
