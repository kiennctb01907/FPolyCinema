/*
  FPolyCinema – 01_schema.sql
  Tạo toàn bộ bảng theo SPEC mục 6 và quy ước CONTRACTS mục 9:
  - Bảng số nhiều PascalCase, khóa chính Id INT IDENTITY, khóa ngoại {Tên}Id
    (ngoại lệ: ShowtimeSeats khóa (ShowtimeId, SeatId), Holidays khóa HolidayDate, AudienceDiscounts khóa AudienceType).
  - Tiền BIGINT, phần trăm DECIMAL(5,2), tồn kho và định mức DECIMAL(18,3).
  - Thời điểm DATETIME2(0) giờ Việt Nam, ngày DATE. Không dùng hàm lấy giờ của SQL Server:
    mọi giá trị thời gian do ứng dụng truyền vào từ IClock.
  - Cột trạng thái có CHECK liệt kê đúng giá trị ở CONTRACTS mục 3–4.
  DbInitializer tách batch theo dòng chỉ có chữ GO và chạy file này một lần cho mỗi DB.
*/

CREATE TABLE SchemaVersions (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SchemaVersions PRIMARY KEY,
    Version NVARCHAR(100) NOT NULL CONSTRAINT UQ_SchemaVersions_Version UNIQUE,
    AppliedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE Cinemas (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cinemas PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    Address NVARCHAR(300) NOT NULL,
    Phone VARCHAR(20) NULL,
    Active BIT NOT NULL CONSTRAINT DF_Cinemas_Active DEFAULT 1
);
GO

CREATE TABLE Users (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email NVARCHAR(256) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
    PasswordHash NVARCHAR(500) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    Phone VARCHAR(20) NULL,
    BirthDate DATE NULL,
    Role VARCHAR(20) NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('Customer', 'POSCashier', 'TicketInspector', 'ShiftManager', 'Manager')),
    CinemaId INT NULL CONSTRAINT FK_Users_Cinemas REFERENCES Cinemas(Id),
    Active BIT NOT NULL CONSTRAINT DF_Users_Active DEFAULT 1,
    MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0,
    MemberCode VARCHAR(10) NULL,
    LoyaltyPoints INT NOT NULL CONSTRAINT DF_Users_LoyaltyPoints DEFAULT 0,
    Tier VARCHAR(10) NOT NULL CONSTRAINT DF_Users_Tier DEFAULT 'MEMBER' CONSTRAINT CK_Users_Tier CHECK (Tier IN ('MEMBER', 'VIP', 'VVIP')),
    WalletBalance BIGINT NOT NULL CONSTRAINT DF_Users_WalletBalance DEFAULT 0 CONSTRAINT CK_Users_WalletBalance CHECK (WalletBalance >= 0),
    ManagerPinHash NVARCHAR(500) NULL,
    PinFailedCount INT NOT NULL CONSTRAINT DF_Users_PinFailedCount DEFAULT 0,
    PinLockedUntil DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL
);
GO
-- Mã thành viên chỉ có ở khách hàng: duy nhất khi khác NULL
CREATE UNIQUE INDEX UX_Users_MemberCode ON Users (MemberCode) WHERE MemberCode IS NOT NULL;
GO

CREATE TABLE PasswordResetOtps (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordResetOtps PRIMARY KEY,
    UserId INT NOT NULL CONSTRAINT FK_PasswordResetOtps_Users REFERENCES Users(Id),
    OtpHash NVARCHAR(500) NOT NULL,
    ExpiresAt DATETIME2(0) NOT NULL,
    FailedAttempts INT NOT NULL CONSTRAINT DF_PasswordResetOtps_FailedAttempts DEFAULT 0,
    UsedAt DATETIME2(0) NULL
);
GO

CREATE TABLE EmailOutbox (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailOutbox PRIMARY KEY,
    ToEmail NVARCHAR(256) NOT NULL,
    Subject NVARCHAR(300) NOT NULL,
    Body NVARCHAR(MAX) NOT NULL,
    SentAt DATETIME2(0) NULL,
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE Categories (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE,
    Active BIT NOT NULL CONSTRAINT DF_Categories_Active DEFAULT 1
);
GO

CREATE TABLE Drinks (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Drinks PRIMARY KEY,
    CategoryId INT NOT NULL CONSTRAINT FK_Drinks_Categories REFERENCES Categories(Id),
    Name NVARCHAR(150) NOT NULL,
    Price BIGINT NOT NULL CONSTRAINT CK_Drinks_Price CHECK (Price >= 0),
    Image NVARCHAR(300) NULL,
    Description NVARCHAR(1000) NULL,
    Active BIT NOT NULL CONSTRAINT DF_Drinks_Active DEFAULT 1,
    IsCombo BIT NOT NULL CONSTRAINT DF_Drinks_IsCombo DEFAULT 0,
    IsSoldOut BIT NOT NULL CONSTRAINT DF_Drinks_IsSoldOut DEFAULT 0,
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE RawMaterials (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RawMaterials PRIMARY KEY,
    CinemaId INT NOT NULL CONSTRAINT FK_RawMaterials_Cinemas REFERENCES Cinemas(Id),
    Name NVARCHAR(150) NOT NULL,
    Unit NVARCHAR(20) NOT NULL,
    ActualStock DECIMAL(18,3) NOT NULL CONSTRAINT DF_RawMaterials_ActualStock DEFAULT 0 CONSTRAINT CK_RawMaterials_ActualStock CHECK (ActualStock >= 0),
    ReservedStock DECIMAL(18,3) NOT NULL CONSTRAINT DF_RawMaterials_ReservedStock DEFAULT 0 CONSTRAINT CK_RawMaterials_ReservedStock CHECK (ReservedStock >= 0),
    MinThreshold DECIMAL(18,3) NOT NULL CONSTRAINT DF_RawMaterials_MinThreshold DEFAULT 0,
    Active BIT NOT NULL CONSTRAINT DF_RawMaterials_Active DEFAULT 1,
    CONSTRAINT UQ_RawMaterials_CinemaName UNIQUE (CinemaId, Name)
);
GO

CREATE TABLE ProductRecipes (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductRecipes PRIMARY KEY,
    DrinkId INT NOT NULL CONSTRAINT FK_ProductRecipes_Drinks REFERENCES Drinks(Id),
    RawMaterialId INT NOT NULL CONSTRAINT FK_ProductRecipes_RawMaterials REFERENCES RawMaterials(Id),
    Quantity DECIMAL(18,3) NOT NULL CONSTRAINT CK_ProductRecipes_Quantity CHECK (Quantity > 0),
    CONSTRAINT UQ_ProductRecipes_DrinkMaterial UNIQUE (DrinkId, RawMaterialId)
);
GO

CREATE TABLE Distributors (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Distributors PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    Email NVARCHAR(256) NULL,
    Phone VARCHAR(20) NULL,
    Active BIT NOT NULL CONSTRAINT DF_Distributors_Active DEFAULT 1
);
GO

CREATE TABLE Movies (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Movies PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    OriginalTitle NVARCHAR(200) NULL,
    Genres VARCHAR(200) NOT NULL,
    DurationMinutes INT NOT NULL CONSTRAINT CK_Movies_DurationMinutes CHECK (DurationMinutes > 0),
    AgeRating VARCHAR(5) NOT NULL CONSTRAINT CK_Movies_AgeRating CHECK (AgeRating IN ('P', 'K', 'T13', 'T16', 'T18', 'C')),
    ClassificationLicenseNo NVARCHAR(50) NULL,
    ContentWarnings VARCHAR(200) NULL,
    IsVietnameseFilm BIT NOT NULL CONSTRAINT DF_Movies_IsVietnameseFilm DEFAULT 0,
    LanguageFormats VARCHAR(100) NOT NULL,
    ReleaseDate DATE NOT NULL,
    DistributorId INT NULL CONSTRAINT FK_Movies_Distributors REFERENCES Distributors(Id),
    MinimumTicketPrice BIGINT NOT NULL CONSTRAINT DF_Movies_MinimumTicketPrice DEFAULT 0 CONSTRAINT CK_Movies_MinimumTicketPrice CHECK (MinimumTicketPrice >= 0),
    IsBlockbuster BIT NOT NULL CONSTRAINT DF_Movies_IsBlockbuster DEFAULT 0,
    PosterUrl NVARCHAR(300) NULL,
    TrailerUrl NVARCHAR(300) NULL,
    Description NVARCHAR(2000) NULL,
    AiDemandScore DECIMAL(4,2) NULL CONSTRAINT CK_Movies_AiDemandScore CHECK (AiDemandScore IS NULL OR AiDemandScore BETWEEN 1 AND 10),
    Active BIT NOT NULL CONSTRAINT DF_Movies_Active DEFAULT 1,
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE FilmRentalRules (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FilmRentalRules PRIMARY KEY,
    MovieId INT NOT NULL CONSTRAINT FK_FilmRentalRules_Movies REFERENCES Movies(Id),
    WeekNumber INT NOT NULL CONSTRAINT CK_FilmRentalRules_WeekNumber CHECK (WeekNumber >= 1),
    DistributorSharePercent DECIMAL(5,2) NOT NULL CONSTRAINT CK_FilmRentalRules_Share CHECK (DistributorSharePercent BETWEEN 0 AND 100),
    CONSTRAINT UQ_FilmRentalRules_MovieWeek UNIQUE (MovieId, WeekNumber)
);
GO

CREATE TABLE KdmLicenses (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_KdmLicenses PRIMARY KEY,
    MovieId INT NOT NULL CONSTRAINT FK_KdmLicenses_Movies REFERENCES Movies(Id),
    CinemaId INT NOT NULL CONSTRAINT FK_KdmLicenses_Cinemas REFERENCES Cinemas(Id),
    ValidFrom DATETIME2(0) NOT NULL,
    ValidUntil DATETIME2(0) NOT NULL,
    Note NVARCHAR(300) NULL,
    CONSTRAINT CK_KdmLicenses_Range CHECK (ValidUntil > ValidFrom)
);
GO

CREATE TABLE Auditoriums (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Auditoriums PRIMARY KEY,
    CinemaId INT NOT NULL CONSTRAINT FK_Auditoriums_Cinemas REFERENCES Cinemas(Id),
    Name NVARCHAR(100) NOT NULL,
    Format VARCHAR(10) NOT NULL CONSTRAINT CK_Auditoriums_Format CHECK (Format IN ('2D', '3D', 'IMAX', '4DX')),
    TotalRows INT NOT NULL CONSTRAINT CK_Auditoriums_TotalRows CHECK (TotalRows BETWEEN 1 AND 26),
    TotalCols INT NOT NULL CONSTRAINT CK_Auditoriums_TotalCols CHECK (TotalCols BETWEEN 1 AND 40),
    IdealRowRatio DECIMAL(4,2) NOT NULL CONSTRAINT DF_Auditoriums_IdealRowRatio DEFAULT 0.65 CONSTRAINT CK_Auditoriums_IdealRowRatio CHECK (IdealRowRatio > 0 AND IdealRowRatio <= 1),
    Active BIT NOT NULL CONSTRAINT DF_Auditoriums_Active DEFAULT 1
);
GO

CREATE TABLE Seats (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Seats PRIMARY KEY,
    AuditoriumId INT NOT NULL CONSTRAINT FK_Seats_Auditoriums REFERENCES Auditoriums(Id),
    RowIndex INT NOT NULL CONSTRAINT CK_Seats_RowIndex CHECK (RowIndex >= 1),
    ColIndex INT NOT NULL CONSTRAINT CK_Seats_ColIndex CHECK (ColIndex >= 1),
    Label VARCHAR(10) NOT NULL,
    SeatType VARCHAR(20) NOT NULL CONSTRAINT CK_Seats_SeatType CHECK (SeatType IN ('STANDARD', 'VIP', 'SWEETBOX', 'WHEELCHAIR')),
    PairGroupId INT NULL,
    IsHouseSeat BIT NOT NULL CONSTRAINT DF_Seats_IsHouseSeat DEFAULT 0,
    IsMaintenance BIT NOT NULL CONSTRAINT DF_Seats_IsMaintenance DEFAULT 0,
    BaseViewScore INT NOT NULL CONSTRAINT DF_Seats_BaseViewScore DEFAULT 0 CONSTRAINT CK_Seats_BaseViewScore CHECK (BaseViewScore BETWEEN 0 AND 100),
    CONSTRAINT UQ_Seats_Position UNIQUE (AuditoriumId, RowIndex, ColIndex),
    CONSTRAINT UQ_Seats_Label UNIQUE (AuditoriumId, Label)
);
GO

CREATE TABLE Showtimes (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Showtimes PRIMARY KEY,
    MovieId INT NOT NULL CONSTRAINT FK_Showtimes_Movies REFERENCES Movies(Id),
    AuditoriumId INT NOT NULL CONSTRAINT FK_Showtimes_Auditoriums REFERENCES Auditoriums(Id),
    StartTime DATETIME2(0) NOT NULL,
    FeatureStartTime DATETIME2(0) NOT NULL,
    EndTime DATETIME2(0) NOT NULL,
    CleaningEndTime DATETIME2(0) NOT NULL,
    LanguageFormat VARCHAR(20) NOT NULL CONSTRAINT CK_Showtimes_LanguageFormat CHECK (LanguageFormat IN ('SUBTITLED', 'DUBBED', 'ORIGINAL')),
    OperationalStatus VARCHAR(30) NOT NULL CONSTRAINT DF_Showtimes_OperationalStatus DEFAULT 'SCHEDULED'
        CONSTRAINT CK_Showtimes_OperationalStatus CHECK (OperationalStatus IN ('SCHEDULED', 'BOARDING', 'ADS_PLAYING', 'FEATURE_PLAYING', 'CREDITS_ROLLING', 'CLEANING_IN_PROGRESS', 'COMPLETED', 'CANCELLED')),
    IsAiGenerated BIT NOT NULL CONSTRAINT DF_Showtimes_IsAiGenerated DEFAULT 0,
    IsPrivate BIT NOT NULL CONSTRAINT DF_Showtimes_IsPrivate DEFAULT 0,
    CancelNote NVARCHAR(500) NULL,
    CreatedAt DATETIME2(0) NOT NULL,
    CONSTRAINT CK_Showtimes_Timeline CHECK (FeatureStartTime >= StartTime AND EndTime > FeatureStartTime AND CleaningEndTime >= EndTime)
);
GO
CREATE INDEX IX_Showtimes_AuditoriumId_StartTime ON Showtimes (AuditoriumId, StartTime);
CREATE INDEX IX_Showtimes_MovieId_StartTime ON Showtimes (MovieId, StartTime);
GO

CREATE TABLE Holidays (
    HolidayDate DATE NOT NULL CONSTRAINT PK_Holidays PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL
);
GO

CREATE TABLE PricingRules (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PricingRules PRIMARY KEY,
    CinemaId INT NOT NULL CONSTRAINT FK_PricingRules_Cinemas REFERENCES Cinemas(Id),
    RuleType VARCHAR(20) NOT NULL CONSTRAINT CK_PricingRules_RuleType CHECK (RuleType IN ('BASE', 'DAY_TYPE', 'TIME_SLOT', 'SEAT_TYPE', 'FORMAT', 'BLOCKBUSTER')),
    -- Hợp của các RuleKey hợp lệ (CONTRACTS mục 3); ghép đúng RuleKey với RuleType do PricingRuleKey.IsValid kiểm ở server
    RuleKey VARCHAR(20) NOT NULL CONSTRAINT CK_PricingRules_RuleKey CHECK (RuleKey IN ('DEFAULT', 'WEEKDAY', 'WEEKEND', 'HOLIDAY', 'HAPPY_TUESDAY', 'MORNING', 'AFTERNOON', 'GOLDEN', 'LATE', 'STANDARD', 'VIP', 'SWEETBOX', 'WHEELCHAIR', '2D', '3D', 'IMAX', '4DX', 'YES')),
    Amount BIGINT NOT NULL,
    CONSTRAINT UQ_PricingRules_Key UNIQUE (CinemaId, RuleType, RuleKey)
);
GO

CREATE TABLE AudienceDiscounts (
    AudienceType VARCHAR(20) NOT NULL CONSTRAINT PK_AudienceDiscounts PRIMARY KEY
        CONSTRAINT CK_AudienceDiscounts_AudienceType CHECK (AudienceType IN ('ADULT', 'U22', 'CHILD_U13', 'CHILD_13_15', 'SENIOR', 'MERIT', 'HARDSHIP', 'DISABLED_SEVERE', 'DISABLED_EXTREME')),
    DiscountPercent DECIMAL(5,2) NOT NULL CONSTRAINT CK_AudienceDiscounts_DiscountPercent CHECK (DiscountPercent BETWEEN 0 AND 100),
    LegalMinPercent DECIMAL(5,2) NOT NULL CONSTRAINT DF_AudienceDiscounts_LegalMinPercent DEFAULT 0,
    PosOnly BIT NOT NULL,
    RequiresDocument BIT NOT NULL,
    -- Được tăng mức giảm nhưng không được thấp hơn mức tối thiểu theo luật (CONTRACTS mục 4)
    CONSTRAINT CK_AudienceDiscounts_LegalMin CHECK (DiscountPercent >= LegalMinPercent)
);
GO

CREATE TABLE PosShifts (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PosShifts PRIMARY KEY,
    UserId INT NOT NULL CONSTRAINT FK_PosShifts_Users REFERENCES Users(Id),
    CinemaId INT NOT NULL CONSTRAINT FK_PosShifts_Cinemas REFERENCES Cinemas(Id),
    TerminalCode VARCHAR(10) NOT NULL,
    OpenedAt DATETIME2(0) NOT NULL,
    OpeningCashFloat BIGINT NOT NULL CONSTRAINT DF_PosShifts_OpeningCashFloat DEFAULT 0,
    ClosedAt DATETIME2(0) NULL,
    ActualCashCounted BIGINT NULL,
    ExpectedCash BIGINT NULL,
    Discrepancy BIGINT NULL,
    Status VARCHAR(10) NOT NULL CONSTRAINT CK_PosShifts_Status CHECK (Status IN ('OPEN', 'CLOSED'))
);
GO
-- Mỗi máy POS và mỗi thu ngân chỉ có tối đa một ca đang mở
CREATE UNIQUE INDEX UX_PosShifts_OpenTerminal ON PosShifts (CinemaId, TerminalCode) WHERE Status = 'OPEN';
CREATE UNIQUE INDEX UX_PosShifts_OpenUser ON PosShifts (UserId) WHERE Status = 'OPEN';
GO

CREATE TABLE CorporateBookings (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CorporateBookings PRIMARY KEY,
    CompanyName NVARCHAR(200) NOT NULL,
    ContactName NVARCHAR(150) NOT NULL,
    Phone VARCHAR(20) NULL,
    Email NVARCHAR(256) NULL,
    Type VARCHAR(30) NOT NULL CONSTRAINT CK_CorporateBookings_Type CHECK (Type IN ('PRIVATE_SCREENING', 'VOUCHER_BATCH')),
    ShowtimeId INT NULL CONSTRAINT FK_CorporateBookings_Showtimes REFERENCES Showtimes(Id),
    VoucherCount INT NULL,
    ContractAmount BIGINT NOT NULL CONSTRAINT DF_CorporateBookings_ContractAmount DEFAULT 0,
    BillId INT NULL,
    CreatedBy INT NOT NULL CONSTRAINT FK_CorporateBookings_Users REFERENCES Users(Id),
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE Vouchers (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vouchers PRIMARY KEY,
    Code VARCHAR(30) NOT NULL CONSTRAINT UQ_Vouchers_Code UNIQUE,
    Type VARCHAR(20) NOT NULL CONSTRAINT CK_Vouchers_Type CHECK (Type IN ('AMOUNT', 'PERCENT', 'FREE_TICKET')),
    Value BIGINT NOT NULL CONSTRAINT CK_Vouchers_Value CHECK (Value >= 0),
    MinBillAmount BIGINT NOT NULL CONSTRAINT DF_Vouchers_MinBillAmount DEFAULT 0,
    ValidFrom DATETIME2(0) NOT NULL,
    ValidUntil DATETIME2(0) NOT NULL,
    UsageLimit INT NOT NULL CONSTRAINT DF_Vouchers_UsageLimit DEFAULT 1,
    UsedCount INT NOT NULL CONSTRAINT DF_Vouchers_UsedCount DEFAULT 0,
    OwnerUserId INT NULL CONSTRAINT FK_Vouchers_Users REFERENCES Users(Id),
    Source VARCHAR(20) NOT NULL CONSTRAINT CK_Vouchers_Source CHECK (Source IN ('PROMO', 'COMPENSATION', 'CORPORATE')),
    CorporateBookingId INT NULL CONSTRAINT FK_Vouchers_CorporateBookings REFERENCES CorporateBookings(Id),
    Active BIT NOT NULL CONSTRAINT DF_Vouchers_Active DEFAULT 1,
    CONSTRAINT CK_Vouchers_Usage CHECK (UsedCount >= 0 AND UsedCount <= UsageLimit),
    CONSTRAINT CK_Vouchers_Range CHECK (ValidUntil > ValidFrom),
    -- Voucher PERCENT lưu số phần trăm nguyên trong Value nên không quá 100; AMOUNT lưu số đồng
    CONSTRAINT CK_Vouchers_PercentValue CHECK (Type <> 'PERCENT' OR Value <= 100)
);
GO

CREATE TABLE Bills (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bills PRIMARY KEY,
    Code VARCHAR(30) NOT NULL CONSTRAINT UQ_Bills_Code UNIQUE,
    Channel VARCHAR(20) NOT NULL CONSTRAINT CK_Bills_Channel CHECK (Channel IN ('POS', 'ONLINE', 'CORPORATE')),
    CinemaId INT NOT NULL CONSTRAINT FK_Bills_Cinemas REFERENCES Cinemas(Id),
    ShowtimeId INT NULL CONSTRAINT FK_Bills_Showtimes REFERENCES Showtimes(Id),
    UserId INT NULL CONSTRAINT FK_Bills_Users REFERENCES Users(Id),
    CustomerId INT NULL CONSTRAINT FK_Bills_Customers REFERENCES Users(Id),
    PosShiftId INT NULL CONSTRAINT FK_Bills_PosShifts REFERENCES PosShifts(Id),
    TerminalCode VARCHAR(10) NULL,
    Status VARCHAR(10) NOT NULL CONSTRAINT DF_Bills_Status DEFAULT 'waiting' CONSTRAINT CK_Bills_Status CHECK (Status IN ('waiting', 'finish', 'cancel')),
    CancelReason VARCHAR(30) NULL CONSTRAINT CK_Bills_CancelReason CHECK (CancelReason IS NULL OR CancelReason IN ('EXPIRED', 'CANCELLED_BEFORE_PAYMENT', 'CANCELLED_BY_USER', 'VOIDED_BY_POS', 'OVERBOOKED_REFUNDED', 'INCIDENT_COMPENSATED')),
    CreatedAt DATETIME2(0) NOT NULL,
    PaidAt DATETIME2(0) NULL,
    CancelledAt DATETIME2(0) NULL,
    TicketSubtotal BIGINT NOT NULL CONSTRAINT DF_Bills_TicketSubtotal DEFAULT 0,
    FnbSubtotal BIGINT NOT NULL CONSTRAINT DF_Bills_FnbSubtotal DEFAULT 0,
    DiscountTotal BIGINT NOT NULL CONSTRAINT DF_Bills_DiscountTotal DEFAULT 0,
    Total BIGINT NOT NULL CONSTRAINT DF_Bills_Total DEFAULT 0 CONSTRAINT CK_Bills_Total CHECK (Total >= 0),
    VoucherId INT NULL CONSTRAINT FK_Bills_Vouchers REFERENCES Vouchers(Id),
    PointsUsed INT NOT NULL CONSTRAINT DF_Bills_PointsUsed DEFAULT 0,
    HoldExpiresAt DATETIME2(0) NULL,
    [CheckInStatus] VARCHAR(30) NOT NULL CONSTRAINT DF_Bills_CheckInStatus DEFAULT 'NONE'
        CONSTRAINT CK_Bills_CheckInStatus CHECK (CheckInStatus IN ('NONE', 'NOT_CHECKED_IN', 'PARTIALLY_CHECKED_IN', 'FULLY_CHECKED_IN', 'VOIDED')),
    FnbStatus VARCHAR(30) NOT NULL CONSTRAINT DF_Bills_FnbStatus DEFAULT 'NONE'
        CONSTRAINT CK_Bills_FnbStatus CHECK (FnbStatus IN ('NONE', 'PENDING_PICKUP', 'PREPARING', 'READY_FOR_PICKUP', 'FULFILLED', 'VOIDED')),
    PickupNumber INT NULL CONSTRAINT CK_Bills_PickupNumber CHECK (PickupNumber IS NULL OR PickupNumber BETWEEN 1 AND 999),
    GuardianConfirmed BIT NOT NULL CONSTRAINT DF_Bills_GuardianConfirmed DEFAULT 0,
    AgeConfirmed BIT NOT NULL CONSTRAINT DF_Bills_AgeConfirmed DEFAULT 0,
    CorporateBookingId INT NULL CONSTRAINT FK_Bills_CorporateBookings REFERENCES CorporateBookings(Id)
);
GO
CREATE INDEX IX_Bills_Status_PaidAt ON Bills (Status, PaidAt);
CREATE INDEX IX_Bills_ShowtimeId ON Bills (ShowtimeId);
CREATE INDEX IX_Bills_CustomerId ON Bills (CustomerId);
GO
-- CorporateBookings và Bills tham chiếu lẫn nhau nên khóa ngoại BillId thêm sau khi có bảng Bills
ALTER TABLE CorporateBookings ADD CONSTRAINT FK_CorporateBookings_Bills FOREIGN KEY (BillId) REFERENCES Bills(Id);
GO

CREATE TABLE BillDetails (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BillDetails PRIMARY KEY,
    BillId INT NOT NULL CONSTRAINT FK_BillDetails_Bills REFERENCES Bills(Id),
    DrinkId INT NOT NULL CONSTRAINT FK_BillDetails_Drinks REFERENCES Drinks(Id),
    Quantity INT NOT NULL CONSTRAINT CK_BillDetails_Quantity CHECK (Quantity > 0),
    Price BIGINT NOT NULL CONSTRAINT CK_BillDetails_Price CHECK (Price >= 0),
    VatRate DECIMAL(5,2) NOT NULL,
    LineFinalAmount BIGINT NOT NULL CONSTRAINT CK_BillDetails_LineFinalAmount CHECK (LineFinalAmount >= 0)
);
GO
CREATE INDEX IX_BillDetails_BillId ON BillDetails (BillId);
GO

CREATE TABLE Tickets (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tickets PRIMARY KEY,
    BillId INT NOT NULL CONSTRAINT FK_Tickets_Bills REFERENCES Bills(Id),
    ShowtimeId INT NOT NULL CONSTRAINT FK_Tickets_Showtimes REFERENCES Showtimes(Id),
    SeatId INT NOT NULL CONSTRAINT FK_Tickets_Seats REFERENCES Seats(Id),
    AudienceType VARCHAR(20) NOT NULL CONSTRAINT CK_Tickets_AudienceType CHECK (AudienceType IN ('ADULT', 'U22', 'CHILD_U13', 'CHILD_13_15', 'SENIOR', 'MERIT', 'HARDSHIP', 'DISABLED_SEVERE', 'DISABLED_EXTREME')),
    ListPrice BIGINT NOT NULL CONSTRAINT CK_Tickets_ListPrice CHECK (ListPrice >= 0),
    FinalPrice BIGINT NOT NULL CONSTRAINT CK_Tickets_FinalPrice CHECK (FinalPrice >= 0),
    VatRate DECIMAL(5,2) NOT NULL,
    QrCode VARCHAR(20) NOT NULL CONSTRAINT UQ_Tickets_QrCode UNIQUE,
    [CheckInStatus] VARCHAR(20) NOT NULL CONSTRAINT DF_Tickets_CheckInStatus DEFAULT 'NOT_CHECKED_IN'
        CONSTRAINT CK_Tickets_CheckInStatus CHECK (CheckInStatus IN ('NOT_CHECKED_IN', 'CHECKED_IN', 'SWAPPED', 'VOIDED')),
    [CheckedInAt] DATETIME2(0) NULL,
    [CheckedInBy] INT NULL CONSTRAINT FK_Tickets_CheckedInBy REFERENCES Users(Id),
    SwappedFromTicketId INT NULL CONSTRAINT FK_Tickets_SwappedFrom REFERENCES Tickets(Id),
    CreatedAt DATETIME2(0) NOT NULL
);
GO
CREATE INDEX IX_Tickets_BillId ON Tickets (BillId);
-- Một ghế của một suất chỉ có tối đa một vé còn hiệu lực
CREATE UNIQUE INDEX UX_Tickets_ActiveSeat ON Tickets (ShowtimeId, SeatId) WHERE CheckInStatus IN ('NOT_CHECKED_IN', 'CHECKED_IN');
GO

CREATE TABLE ShowtimeSeats (
    ShowtimeId INT NOT NULL CONSTRAINT FK_ShowtimeSeats_Showtimes REFERENCES Showtimes(Id),
    SeatId INT NOT NULL CONSTRAINT FK_ShowtimeSeats_Seats REFERENCES Seats(Id),
    Status VARCHAR(20) NOT NULL CONSTRAINT DF_ShowtimeSeats_Status DEFAULT 'AVAILABLE' CONSTRAINT CK_ShowtimeSeats_Status CHECK (Status IN ('AVAILABLE', 'HELD', 'BOOKED', 'HOUSE_HOLD')),
    HoldBillId INT NULL CONSTRAINT FK_ShowtimeSeats_Bills REFERENCES Bills(Id),
    HoldExpiresAt DATETIME2(0) NULL,
    TicketId INT NULL CONSTRAINT FK_ShowtimeSeats_Tickets REFERENCES Tickets(Id),
    CONSTRAINT PK_ShowtimeSeats PRIMARY KEY (ShowtimeId, SeatId)
);
GO
CREATE INDEX IX_ShowtimeSeats_HoldBillId ON ShowtimeSeats (HoldBillId);
GO

CREATE TABLE Payments (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    BillId INT NOT NULL CONSTRAINT FK_Payments_Bills REFERENCES Bills(Id),
    PosShiftId INT NULL CONSTRAINT FK_Payments_PosShifts REFERENCES PosShifts(Id),
    Method VARCHAR(20) NOT NULL CONSTRAINT CK_Payments_Method CHECK (Method IN ('CASH', 'VIETQR_SIM', 'WALLET')),
    Direction VARCHAR(10) NOT NULL CONSTRAINT CK_Payments_Direction CHECK (Direction IN ('IN', 'REFUND')),
    Amount BIGINT NOT NULL CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0),
    Status VARCHAR(20) NOT NULL CONSTRAINT CK_Payments_Status CHECK (Status IN ('PENDING', 'SUCCEEDED', 'FAILED')),
    ProviderTxnId VARCHAR(100) NULL,
    RawPayload NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2(0) NOT NULL
);
GO
CREATE INDEX IX_Payments_BillId ON Payments (BillId);
-- Mã giao dịch của cổng thanh toán duy nhất khi khác NULL (chống webhook lặp)
CREATE UNIQUE INDEX UX_Payments_ProviderTxnId ON Payments (ProviderTxnId) WHERE ProviderTxnId IS NOT NULL;
GO

CREATE TABLE WalletTransactions (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalletTransactions PRIMARY KEY,
    UserId INT NOT NULL CONSTRAINT FK_WalletTransactions_Users REFERENCES Users(Id),
    Amount BIGINT NOT NULL,
    BalanceAfter BIGINT NOT NULL CONSTRAINT CK_WalletTransactions_BalanceAfter CHECK (BalanceAfter >= 0),
    Reason NVARCHAR(300) NOT NULL,
    BillId INT NULL CONSTRAINT FK_WalletTransactions_Bills REFERENCES Bills(Id),
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE PointTransactions (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PointTransactions PRIMARY KEY,
    UserId INT NOT NULL CONSTRAINT FK_PointTransactions_Users REFERENCES Users(Id),
    Points INT NOT NULL,
    Reason NVARCHAR(300) NOT NULL,
    BillId INT NULL CONSTRAINT FK_PointTransactions_Bills REFERENCES Bills(Id),
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE SupervisorApprovals (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupervisorApprovals PRIMARY KEY,
    Type VARCHAR(20) NOT NULL CONSTRAINT CK_SupervisorApprovals_Type CHECK (Type IN ('VOID_BILL')),
    BillId INT NOT NULL CONSTRAINT FK_SupervisorApprovals_Bills REFERENCES Bills(Id),
    RequestedBy INT NOT NULL CONSTRAINT FK_SupervisorApprovals_RequestedBy REFERENCES Users(Id),
    Reason NVARCHAR(500) NOT NULL,
    Status VARCHAR(20) NOT NULL CONSTRAINT DF_SupervisorApprovals_Status DEFAULT 'PENDING' CONSTRAINT CK_SupervisorApprovals_Status CHECK (Status IN ('PENDING', 'APPROVED', 'REJECTED', 'EXPIRED')),
    Method VARCHAR(10) NULL CONSTRAINT CK_SupervisorApprovals_Method CHECK (Method IS NULL OR Method IN ('PIN', 'REMOTE')),
    DecidedBy INT NULL CONSTRAINT FK_SupervisorApprovals_DecidedBy REFERENCES Users(Id),
    DecidedAt DATETIME2(0) NULL,
    ExpiresAt DATETIME2(0) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE Notifications (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId INT NOT NULL CONSTRAINT FK_Notifications_Users REFERENCES Users(Id),
    Title NVARCHAR(200) NOT NULL,
    Body NVARCHAR(1000) NOT NULL,
    Link NVARCHAR(300) NULL,
    IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0,
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE AuditLogs (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
    UserId INT NULL CONSTRAINT FK_AuditLogs_Users REFERENCES Users(Id),
    Action VARCHAR(50) NOT NULL,
    EntityType VARCHAR(50) NOT NULL,
    EntityId NVARCHAR(50) NULL,
    Detail NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2(0) NOT NULL
);
GO

CREATE TABLE WastageLogs (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WastageLogs PRIMARY KEY,
    CinemaId INT NOT NULL CONSTRAINT FK_WastageLogs_Cinemas REFERENCES Cinemas(Id),
    Status VARCHAR(20) NOT NULL CONSTRAINT DF_WastageLogs_Status DEFAULT 'PENDING' CONSTRAINT CK_WastageLogs_Status CHECK (Status IN ('PENDING', 'APPROVED', 'REJECTED')),
    Note NVARCHAR(500) NULL,
    CreatedBy INT NOT NULL CONSTRAINT FK_WastageLogs_CreatedBy REFERENCES Users(Id),
    CreatedAt DATETIME2(0) NOT NULL,
    DecidedBy INT NULL CONSTRAINT FK_WastageLogs_DecidedBy REFERENCES Users(Id),
    DecidedAt DATETIME2(0) NULL
);
GO

CREATE TABLE WastageLogItems (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WastageLogItems PRIMARY KEY,
    WastageLogId INT NOT NULL CONSTRAINT FK_WastageLogItems_WastageLogs REFERENCES WastageLogs(Id),
    RawMaterialId INT NOT NULL CONSTRAINT FK_WastageLogItems_RawMaterials REFERENCES RawMaterials(Id),
    Quantity DECIMAL(18,3) NOT NULL CONSTRAINT CK_WastageLogItems_Quantity CHECK (Quantity > 0),
    Reason NVARCHAR(300) NULL
);
GO

CREATE TABLE InventoryTransactions (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryTransactions PRIMARY KEY,
    RawMaterialId INT NOT NULL CONSTRAINT FK_InventoryTransactions_RawMaterials REFERENCES RawMaterials(Id),
    Type VARCHAR(20) NOT NULL CONSTRAINT CK_InventoryTransactions_Type CHECK (Type IN ('IMPORT', 'RESERVE', 'RELEASE', 'CONSUME', 'WASTAGE', 'ADJUST')),
    Quantity DECIMAL(18,3) NOT NULL,
    BillId INT NULL CONSTRAINT FK_InventoryTransactions_Bills REFERENCES Bills(Id),
    WastageLogId INT NULL CONSTRAINT FK_InventoryTransactions_WastageLogs REFERENCES WastageLogs(Id),
    Note NVARCHAR(300) NULL,
    CreatedBy INT NULL CONSTRAINT FK_InventoryTransactions_Users REFERENCES Users(Id),
    CreatedAt DATETIME2(0) NOT NULL
);
GO
CREATE INDEX IX_InventoryTransactions_RawMaterialId_CreatedAt ON InventoryTransactions (RawMaterialId, CreatedAt);
GO
