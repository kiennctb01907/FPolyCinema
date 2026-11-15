# FPolyCinema

Website quản lý chuỗi rạp chiếu phim – đồ án môn Dự án .NET 1 (FPT Polytechnic).
ASP.NET Core MVC .NET 8, Razor View, ADO.NET (Utils.DBUtil và các lớp DAL), SQL Server, SignalR.

Tài liệu: [docs/README.md](docs/README.md) (spec, giao diện, CONTRACTS), tiến độ ở [PROGRESS.md](PROGRESS.md),
kịch bản kiểm thử ở [docs/TEST-SCENARIOS.md](docs/TEST-SCENARIOS.md).

## 1. Yêu cầu cài đặt

| Thành phần | Phiên bản | Ghi chú |
|---|---|---|
| .NET SDK | 8.0.x | `global.json` ghim SDK 8.0 (cho phép bản vá mới hơn). Chỉ chạy web thì cần ASP.NET Core Runtime 8.0 |
| SQL Server | 2019 trở lên | Bản Express hoặc Developer đều được; trên Linux dùng SQL Server for Linux hoặc máy SQL Server khác |
| Node.js | LTS (20 trở lên) | Chỉ cần cho công cụ kiểm tra spec (`tools/spec-check`) và chụp giao diện (`tools/ui-snapshots`) |
| Trình duyệt | Chrome hoặc Edge bản mới | Trung tâm demo dùng tên miền phụ `*.localhost`, hai trình duyệt này tự trỏ về máy |
| Git | Git for Windows | Có Git Bash (hook của Claude Code chạy bằng Git Bash) |

Không cần cài thêm gói nào: thư viện giao diện (Bootstrap, Bootstrap Icons, Chart.js, SignalR client, html5-qrcode) đã nằm trong `wwwroot/lib`,
ảnh mẫu nằm trong `wwwroot/uploads/seed`. Ứng dụng không tải gì từ internet khi chạy (trừ trợ lý AI khi có khóa API).

## 2. Cấu hình chuỗi kết nối

Chuỗi kết nối đọc từ khóa `ConnectionStrings:Default`. `appsettings.json` để trống khóa này; giá trị chạy thật đặt ở một trong hai chỗ:

- Máy phát triển: `src/FPolyCinema.Web/appsettings.Development.json`
  ```json
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=FPolyCinema;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False"
  }
  ```
  SQL Server Express thường có tên `localhost\SQLEXPRESS`; dùng tài khoản SQL thì thay `Trusted_Connection=True` bằng `User Id=…;Password=…`.
- Biến môi trường (máy chủ, CI): `ConnectionStrings__Default` (dấu hai chấm đổi thành hai dấu gạch dưới). Biến môi trường ghi đè file cấu hình.
  ```powershell
  $env:ConnectionStrings__Default = "Server=localhost;Database=FPolyCinema;Trusted_Connection=True;TrustServerCertificate=True"
  ```

Tên CSDL lấy từ `Database=` của chuỗi kết nối; lần chạy đầu ứng dụng tự tạo CSDL, bảng, stored procedure và nạp dữ liệu mẫu nếu CSDL còn trống.

Các khóa cấu hình khác (đầy đủ ở [docs/CONTRACTS.md](docs/CONTRACTS.md) mục 7). Khóa bí mật chỉ đặt bằng biến môi trường, không ghi vào file:

| Khóa | Biến môi trường | Ý nghĩa |
|---|---|---|
| `Demo:EnableTools` | `Demo__EnableTools` | `true` chỉ trong Development: thanh đổi vai trò, trung tâm demo, style guide, hộp thư thử. Tắt thì mọi đường dẫn `/demo/…`, `/dev/…` trả 404 |
| `Demo:ClockOffsetMinutes` | `Demo__ClockOffsetMinutes` | Lệch đồng hồ ứng dụng (phút) để demo ngoài giờ chiếu, chỉ có tác dụng khi bật công cụ demo |
| `Email:SmtpHost`, `SmtpPort`, `SmtpUser`, `From` | `Email__SmtpHost`… | Để trống thì thư (OTP, mật khẩu mới) lưu vào bảng EmailOutbox, xem ở `/dev/mailbox` |
| `Email:SmtpPassword` | `Email__SmtpPassword` | Chỉ đặt bằng biến môi trường |
| `Payment:WebhookSecret` | `Payment__WebhookSecret` | Khóa ký HMAC webhook của trang mô phỏng thanh toán; để trống thì sinh ngẫu nhiên mỗi lần chạy |
| `Llm:BaseUrl`, `Llm:Model` | `Llm__BaseUrl`, `Llm__Model` | Máy chủ và mô hình của trợ lý đặt vé AI (API kiểu OpenAI Chat Completions) |
| `Llm:ApiKey` | `Llm__ApiKey` | Chỉ đặt bằng biến môi trường; không có khóa thì trợ lý dùng LocalAgent (bộ phân tích câu tiếng Việt chạy cục bộ) |
| `Uploads:Path` | `Uploads__Path` | Thư mục lưu ảnh tải lên, mặc định `wwwroot/uploads` |
| `CinemaRules:*` | `CinemaRules__SeatHoldMinutes`… | Tham số nghiệp vụ (giữ ghế, giờ bán, VAT, điểm…), mặc định ở CONTRACTS mục 7 |

## 3. Cách chạy trên máy phát triển

```powershell
# 1. Tạo lại CSDL, bảng, stored procedure và dữ liệu mẫu rồi thoát (xóa sạch CSDL cũ)
dotnet run --project src/FPolyCinema.Web -- --reset-db --init-only

# 2. Chạy web (môi trường Development, cổng 5080)
dotnet run --project src/FPolyCinema.Web
```

Mở http://localhost:5080. Bỏ `--init-only` thì ứng dụng tạo lại DB rồi chạy web luôn. `--reset-db` chỉ dùng được trong Development.
Dữ liệu mẫu sinh theo giờ lúc chạy `--reset-db` (lịch chiếu 30 ngày trước đến 7 ngày sau, hóa đơn, vé, kho), nên mỗi lần demo nên tạo lại DB.

Các lệnh khác:

| Việc | Lệnh |
|---|---|
| Build | `dotnet build` |
| Unit test và test tích hợp (tự tạo và xóa CSDL thử) | `dotnet test` |
| Build, test, tạo lại DB một lần | `node tools/spec-check/check.mjs test --reset-db` |
| Chụp giao diện (app đang chạy) | `npm install` một lần, rồi `node tools/ui-snapshots/snap.mjs --phase N` |
| Kiểm tra truy vết yêu cầu của một phase | `node tools/spec-check/check.mjs verify N` |

Trang dành cho trình diễn (chỉ khi `Demo:EnableTools = true`):

- Thanh đổi vai trò ở góc trái dưới mọi trang: đổi sang một trong 5 tài khoản mẫu bằng một lần bấm, nút "Trung tâm demo".
- Trung tâm demo `/demo/multi-screen`: 5 kịch bản thời gian thực (hai khách giành ghế, POS và màn hình phụ, bếp và bảng gọi số, soát vé và bảng sảnh, duyệt hủy từ xa).
  Mỗi màn hình mở trong cửa sổ riêng trên `cashier.localhost`, `manager.localhost`, `gate.localhost`, `khach-a.localhost`, `khach-b.localhost` (mỗi cửa sổ một phiên đăng nhập).
  Lần đầu bấm "Mở các màn hình", Chrome có thể chặn cửa sổ bật lên: bấm biểu tượng ở cuối thanh địa chỉ, chọn luôn cho phép với localhost.
  Kịch bản bếp và soát vé cần suất đang chiếu; ngoài giờ chiếu (sau 23:30 hoặc trước 08:00) đặt `Demo__ClockOffsetMinutes`, ví dụ `-480`, rồi chạy lại với `--reset-db`.
- Style guide `/dev/style-guide`, hộp thư thử `/dev/mailbox`.

## 4. Tài khoản mẫu

Mật khẩu của mọi tài khoản: `123456`. PIN quản lý (ShiftManager, Manager): `246810`.

| Email | Vai trò | Trang chính | Ghi chú |
|---|---|---|---|
| manager@fpolycinema.vn | Manager (quản lý rạp) | /admin | Toàn chuỗi: danh mục, phim, phòng, giá, lịch chiếu, AI xếp lịch, báo cáo |
| shift@fpolycinema.vn | ShiftManager (quản lý ca) | /ops | Rạp 1: vận hành ca, duyệt hủy từ xa, phiếu hủy hàng, hủy suất do sự cố |
| cashier@fpolycinema.vn | POSCashier (thu ngân) | /pos | Rạp 1, có ca đang mở tại máy POS-01; bếp `/pos/fnb-kds` |
| gate@fpolycinema.vn | TicketInspector (soát vé) | /pos/gate-inspector | Rạp 1 |
| khach@fpolycinema.vn | Customer | / | Hạng VIP, có điểm, ví, voucher và vé sắp chiếu kèm combo |
| khach2@fpolycinema.vn | Customer | / | Khách thứ hai để thử hai khách cùng giành một ghế |

Màn hình không cần đăng nhập: màn hình phụ của quầy `/pos/customer-display?terminal=POS-01&cinemaId=1`,
bảng gọi số `/lobby/pickup-board?cinemaId=1`, bảng sảnh `/lobby/digital-signage?cinemaId=1`.

## 5. Publish và chạy sau Nginx trên Linux

Ví dụ dưới đây dùng Ubuntu 22.04/24.04, ứng dụng đặt ở `/var/www/fpolycinema`, chạy bằng người dùng `www-data`, Nginx nhận cổng 80/443 và chuyển vào Kestrel ở `127.0.0.1:5080`.
Ứng dụng đã gọi `UseForwardedHeaders` (tin `X-Forwarded-For`, `X-Forwarded-Proto` từ proxy) nên địa chỉ, cookie và chuyển hướng đúng khi chạy sau Nginx.

### 5.1. Publish

Trên máy build (Windows hoặc Linux, có .NET 8 SDK):

```bash
dotnet publish src/FPolyCinema.Web -c Release -o ./publish
```

Thư mục `publish` gồm `FPolyCinema.Web.dll`, `wwwroot` (cả ảnh mẫu) và `Database/*.sql` (script tạo bảng và stored procedure).
Chép sang máy chủ, ví dụ `scp -r publish/* user@may-chu:/var/www/fpolycinema/`.

### 5.2. Chuẩn bị máy chủ

```bash
# ASP.NET Core Runtime 8.0 (gói của Ubuntu; hoặc theo hướng dẫn của Microsoft cho bản phân phối khác)
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0 nginx

sudo mkdir -p /var/www/fpolycinema
sudo chown -R www-data:www-data /var/www/fpolycinema
# Thư mục ảnh tải lên phải ghi được
sudo chmod -R u+rw /var/www/fpolycinema/wwwroot/uploads
# Khóa Data Protection (mã hóa cookie đăng nhập, Anti-Forgery) lưu ở $HOME/.aspnet của www-data (/var/www);
# thư mục này phải ghi được, nếu không mọi người bị đăng xuất mỗi lần khởi động lại dịch vụ
sudo mkdir -p /var/www/.aspnet
sudo chown www-data:www-data /var/www/.aspnet
```

Tạo CSDL trống hoặc để ứng dụng tự tạo khi chạy lần đầu (tài khoản SQL cần quyền `dbcreator`, hoặc tạo sẵn CSDL rồi cấp `db_owner`).
Lần chạy đầu ứng dụng chạy `Database/*.sql` và nạp dữ liệu mẫu nếu CSDL trống.

### 5.3. Chuỗi kết nối và bí mật qua biến môi trường

Không sửa `appsettings.json` trên máy chủ. Ghi bí mật vào một file chỉ root đọc được, systemd nạp file này thành biến môi trường:

```bash
sudo mkdir -p /etc/fpolycinema
sudo tee /etc/fpolycinema/fpolycinema.env > /dev/null <<'EOF'
ConnectionStrings__Default=Server=127.0.0.1,1433;Database=FPolyCinema;User Id=fpolycinema;Password=DoiMatKhauNay;TrustServerCertificate=True
Payment__WebhookSecret=chuoi-ngau-nhien-dai-it-nhat-32-ky-tu
Email__SmtpHost=smtp.example.com
Email__SmtpUser=no-reply@example.com
Email__SmtpPassword=mat-khau-smtp
Llm__ApiKey=
EOF
sudo chmod 600 /etc/fpolycinema/fpolycinema.env
```

`Demo__EnableTools` để mặc định `false` trên máy chủ (không có thanh đổi vai trò và trang demo).

### 5.4. Dịch vụ systemd

`/etc/systemd/system/fpolycinema.service`:

```ini
[Unit]
Description=FPolyCinema (ASP.NET Core)
After=network.target

[Service]
WorkingDirectory=/var/www/fpolycinema
ExecStart=/usr/bin/dotnet /var/www/fpolycinema/FPolyCinema.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=fpolycinema
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5080
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
EnvironmentFile=/etc/fpolycinema/fpolycinema.env

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now fpolycinema
sudo systemctl status fpolycinema
sudo journalctl -u fpolycinema -f      # xem log, kể cả log khởi tạo CSDL
curl -I http://127.0.0.1:5080          # Kestrel trả 200
```

### 5.5. Cấu hình Nginx

`/etc/nginx/sites-available/fpolycinema`:

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 80;
    server_name fpolycinema.example.com;

    client_max_body_size 10m;           # ảnh poster, ảnh món tối đa 5MB

    location / {
        proxy_pass         http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;
        proxy_cache_bypass $http_upgrade;
    }

    # SignalR (/hubs/cinema) dùng WebSocket: giữ kết nối lâu
    location /hubs/ {
        proxy_pass         http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection $connection_upgrade;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/fpolycinema /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

HTTPS: dùng Certbot (`sudo apt-get install -y certbot python3-certbot-nginx`, rồi `sudo certbot --nginx -d fpolycinema.example.com`).
Nhờ `X-Forwarded-Proto`, ứng dụng biết yêu cầu gốc là HTTPS và bật HSTS ở môi trường Production.

### 5.6. Cập nhật phiên bản mới

```bash
dotnet publish src/FPolyCinema.Web -c Release -o ./publish        # trên máy build
rsync -a --delete --exclude 'wwwroot/uploads/' publish/ user@may-chu:/var/www/fpolycinema/
sudo systemctl restart fpolycinema                                 # trên máy chủ
```

Giữ nguyên `wwwroot/uploads` để không mất ảnh đã tải lên. Script `Database/*.sql` đã chạy được ghi trong bảng SchemaVersions nên không chạy lại;
stored procedure dùng `CREATE OR ALTER` nên luôn được cập nhật.
