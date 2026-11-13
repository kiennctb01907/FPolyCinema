# Cơ sở dữ liệu (sinh từ Database/*.sql)

35 bảng, 4 stored procedure.

## Users

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Email | NVARCHAR(256) | Không |  |  | UNIQUE |
| PasswordHash | NVARCHAR(500) | Không |  |  |  |
| FullName | NVARCHAR(150) | Không |  |  |  |
| Phone | VARCHAR(20) | Có |  |  |  |
| BirthDate | DATE | Có |  |  |  |
| Role | VARCHAR(20) | Không |  |  | CHECK: Customer, POSCashier, TicketInspector, ShiftManager, Manager |
| CinemaId | INT | Có | FK | Cinemas.Id |  |
| Active | BIT | Không |  |  | DEFAULT 1 |
| MustChangePassword | BIT | Không |  |  | DEFAULT 0 |
| MemberCode | VARCHAR(10) | Có |  |  | UNIQUE |
| LoyaltyPoints | INT | Không |  |  | DEFAULT 0 |
| Tier | VARCHAR(10) | Không |  |  | CHECK: MEMBER, VIP, VVIP; DEFAULT 'MEMBER' |
| WalletBalance | BIGINT | Không |  |  | CHECK: WalletBalance >= 0; DEFAULT 0 |
| ManagerPinHash | NVARCHAR(500) | Có |  |  |  |
| PinFailedCount | INT | Không |  |  | DEFAULT 0 |
| PinLockedUntil | DATETIME2(0) | Có |  |  |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

Ràng buộc nhiều cột: Index (MemberCode)

## PasswordResetOtps

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| UserId | INT | Không | FK | Users.Id |  |
| OtpHash | NVARCHAR(500) | Không |  |  |  |
| ExpiresAt | DATETIME2(0) | Không |  |  |  |
| FailedAttempts | INT | Không |  |  | DEFAULT 0 |
| UsedAt | DATETIME2(0) | Có |  |  |  |

## EmailOutbox

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| ToEmail | NVARCHAR(256) | Không |  |  |  |
| Subject | NVARCHAR(300) | Không |  |  |  |
| Body | NVARCHAR(MAX) | Không |  |  |  |
| SentAt | DATETIME2(0) | Có |  |  |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## Cinemas

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Name | NVARCHAR(150) | Không |  |  |  |
| Address | NVARCHAR(300) | Không |  |  |  |
| Phone | VARCHAR(20) | Có |  |  |  |
| Active | BIT | Không |  |  | DEFAULT 1 |

## Categories

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Name | NVARCHAR(100) | Không |  |  | UNIQUE |
| Active | BIT | Không |  |  | DEFAULT 1 |

## Drinks

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| CategoryId | INT | Không | FK | Categories.Id |  |
| Name | NVARCHAR(150) | Không |  |  |  |
| Price | BIGINT | Không |  |  | CHECK: Price >= 0 |
| Image | NVARCHAR(300) | Có |  |  |  |
| Description | NVARCHAR(1000) | Có |  |  |  |
| Active | BIT | Không |  |  | DEFAULT 1 |
| IsCombo | BIT | Không |  |  | DEFAULT 0 |
| IsSoldOut | BIT | Không |  |  | DEFAULT 0 |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## RawMaterials

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| CinemaId | INT | Không | FK | Cinemas.Id |  |
| Name | NVARCHAR(150) | Không |  |  |  |
| Unit | NVARCHAR(20) | Không |  |  |  |
| ActualStock | DECIMAL(18,3) | Không |  |  | CHECK: ActualStock >= 0; DEFAULT 0 |
| ReservedStock | DECIMAL(18,3) | Không |  |  | CHECK: ReservedStock >= 0; DEFAULT 0 |
| MinThreshold | DECIMAL(18,3) | Không |  |  | DEFAULT 0 |
| Active | BIT | Không |  |  | DEFAULT 1 |

Ràng buộc nhiều cột: UNIQUE(CinemaId, Name)

## ProductRecipes

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| DrinkId | INT | Không | FK | Drinks.Id |  |
| RawMaterialId | INT | Không | FK | RawMaterials.Id |  |
| Quantity | DECIMAL(18,3) | Không |  |  | CHECK: Quantity > 0 |

Ràng buộc nhiều cột: UNIQUE(DrinkId, RawMaterialId)

## InventoryTransactions

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| RawMaterialId | INT | Không | FK | RawMaterials.Id |  |
| Type | VARCHAR(20) | Không |  |  | CHECK: IMPORT, RESERVE, RELEASE, CONSUME, WASTAGE, ADJUST |
| Quantity | DECIMAL(18,3) | Không |  |  |  |
| BillId | INT | Có | FK | Bills.Id |  |
| WastageLogId | INT | Có | FK | WastageLogs.Id |  |
| Note | NVARCHAR(300) | Có |  |  |  |
| CreatedBy | INT | Có | FK | Users.Id |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

Ràng buộc nhiều cột: Index (RawMaterialId, CreatedAt)

## WastageLogs

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| CinemaId | INT | Không | FK | Cinemas.Id |  |
| Status | VARCHAR(20) | Không |  |  | CHECK: PENDING, APPROVED, REJECTED; DEFAULT 'PENDING' |
| Note | NVARCHAR(500) | Có |  |  |  |
| CreatedBy | INT | Không | FK | Users.Id |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |
| DecidedBy | INT | Có | FK | Users.Id |  |
| DecidedAt | DATETIME2(0) | Có |  |  |  |

## WastageLogItems

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| WastageLogId | INT | Không | FK | WastageLogs.Id |  |
| RawMaterialId | INT | Không | FK | RawMaterials.Id |  |
| Quantity | DECIMAL(18,3) | Không |  |  | CHECK: Quantity > 0 |
| Reason | NVARCHAR(300) | Có |  |  |  |

## Distributors

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Name | NVARCHAR(150) | Không |  |  |  |
| Email | NVARCHAR(256) | Có |  |  |  |
| Phone | VARCHAR(20) | Có |  |  |  |
| Active | BIT | Không |  |  | DEFAULT 1 |

## Movies

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Title | NVARCHAR(200) | Không |  |  |  |
| OriginalTitle | NVARCHAR(200) | Có |  |  |  |
| Genres | VARCHAR(200) | Không |  |  |  |
| DurationMinutes | INT | Không |  |  | CHECK: DurationMinutes > 0 |
| AgeRating | VARCHAR(5) | Không |  |  | CHECK: P, K, T13, T16, T18, C |
| ClassificationLicenseNo | NVARCHAR(50) | Có |  |  |  |
| ContentWarnings | VARCHAR(200) | Có |  |  |  |
| IsVietnameseFilm | BIT | Không |  |  | DEFAULT 0 |
| LanguageFormats | VARCHAR(100) | Không |  |  |  |
| ReleaseDate | DATE | Không |  |  |  |
| DistributorId | INT | Có | FK | Distributors.Id |  |
| MinimumTicketPrice | BIGINT | Không |  |  | CHECK: MinimumTicketPrice >= 0; DEFAULT 0 |
| IsBlockbuster | BIT | Không |  |  | DEFAULT 0 |
| PosterUrl | NVARCHAR(300) | Có |  |  |  |
| TrailerUrl | NVARCHAR(300) | Có |  |  |  |
| Description | NVARCHAR(2000) | Có |  |  |  |
| AiDemandScore | DECIMAL(4,2) | Có |  |  | CHECK: AiDemandScore IS NULL OR AiDemandScore BETWEEN 1 AND 10 |
| Active | BIT | Không |  |  | DEFAULT 1 |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## FilmRentalRules

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| MovieId | INT | Không | FK | Movies.Id |  |
| WeekNumber | INT | Không |  |  | CHECK: WeekNumber >= 1 |
| DistributorSharePercent | DECIMAL(5,2) | Không |  |  | CHECK: DistributorSharePercent BETWEEN 0 AND 100 |

Ràng buộc nhiều cột: UNIQUE(MovieId, WeekNumber)

## KdmLicenses

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| MovieId | INT | Không | FK | Movies.Id |  |
| CinemaId | INT | Không | FK | Cinemas.Id |  |
| ValidFrom | DATETIME2(0) | Không |  |  |  |
| ValidUntil | DATETIME2(0) | Không |  |  |  |
| Note | NVARCHAR(300) | Có |  |  |  |

## Auditoriums

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| CinemaId | INT | Không | FK | Cinemas.Id |  |
| Name | NVARCHAR(100) | Không |  |  |  |
| Format | VARCHAR(10) | Không |  |  | CHECK: 2D, 3D, IMAX, 4DX |
| TotalRows | INT | Không |  |  | CHECK: TotalRows BETWEEN 1 AND 26 |
| TotalCols | INT | Không |  |  | CHECK: TotalCols BETWEEN 1 AND 40 |
| IdealRowRatio | DECIMAL(4,2) | Không |  |  | CHECK: IdealRowRatio > 0 AND IdealRowRatio <= 1; DEFAULT 0.65 |
| Active | BIT | Không |  |  | DEFAULT 1 |

## Seats

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| AuditoriumId | INT | Không | FK | Auditoriums.Id |  |
| RowIndex | INT | Không |  |  | CHECK: RowIndex >= 1 |
| ColIndex | INT | Không |  |  | CHECK: ColIndex >= 1 |
| Label | VARCHAR(10) | Không |  |  |  |
| SeatType | VARCHAR(20) | Không |  |  | CHECK: STANDARD, VIP, SWEETBOX, WHEELCHAIR |
| PairGroupId | INT | Có |  |  |  |
| IsHouseSeat | BIT | Không |  |  | DEFAULT 0 |
| IsMaintenance | BIT | Không |  |  | DEFAULT 0 |
| BaseViewScore | INT | Không |  |  | CHECK: BaseViewScore BETWEEN 0 AND 100; DEFAULT 0 |

Ràng buộc nhiều cột: UNIQUE(AuditoriumId, RowIndex, ColIndex); UNIQUE(AuditoriumId, Label)

## Showtimes

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| MovieId | INT | Không | FK | Movies.Id |  |
| AuditoriumId | INT | Không | FK | Auditoriums.Id |  |
| StartTime | DATETIME2(0) | Không |  |  |  |
| FeatureStartTime | DATETIME2(0) | Không |  |  |  |
| EndTime | DATETIME2(0) | Không |  |  |  |
| CleaningEndTime | DATETIME2(0) | Không |  |  |  |
| LanguageFormat | VARCHAR(20) | Không |  |  | CHECK: SUBTITLED, DUBBED, ORIGINAL |
| OperationalStatus | VARCHAR(30) | Không |  |  | CHECK: SCHEDULED, BOARDING, ADS_PLAYING, FEATURE_PLAYING, CREDITS_ROLLING, CLEANING_IN_PROGRESS, COMPLETED, CANCELLED; DEFAULT 'SCHEDULED' |
| IsAiGenerated | BIT | Không |  |  | DEFAULT 0 |
| IsPrivate | BIT | Không |  |  | DEFAULT 0 |
| CancelNote | NVARCHAR(500) | Có |  |  |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

Ràng buộc nhiều cột: Index (AuditoriumId, StartTime); Index (MovieId, StartTime)

## ShowtimeSeats

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| ShowtimeId | INT | Không | PK, FK | Showtimes.Id |  |
| SeatId | INT | Không | PK, FK | Seats.Id |  |
| Status | VARCHAR(20) | Không |  |  | CHECK: AVAILABLE, HELD, BOOKED, HOUSE_HOLD; DEFAULT 'AVAILABLE' |
| HoldBillId | INT | Có | FK | Bills.Id |  |
| HoldExpiresAt | DATETIME2(0) | Có |  |  |  |
| HoldAudienceType | VARCHAR(20) | Có |  |  | CHECK: ADULT, U22, CHILD_U13, CHILD_13_15, SENIOR, MERIT, HARDSHIP, DISABLED_SEVERE, DISABLED_EXTREME |
| TicketId | INT | Có | FK | Tickets.Id |  |

Ràng buộc nhiều cột: Khóa chính (ShowtimeId, SeatId); Index (HoldBillId)

## Holidays

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| HolidayDate | DATE | Không | PK |  |  |
| Name | NVARCHAR(150) | Không |  |  |  |

## PricingRules

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| CinemaId | INT | Không | FK | Cinemas.Id |  |
| RuleType | VARCHAR(20) | Không |  |  | CHECK: BASE, DAY_TYPE, TIME_SLOT, SEAT_TYPE, FORMAT, BLOCKBUSTER |
| RuleKey | VARCHAR(20) | Không |  |  | CHECK: DEFAULT, WEEKDAY, WEEKEND, HOLIDAY, HAPPY_TUESDAY, MORNING, AFTERNOON, GOLDEN, LATE, STANDARD, VIP, SWEETBOX, WHEELCHAIR, 2D, 3D, IMAX, 4DX, YES |
| Amount | BIGINT | Không |  |  |  |

Ràng buộc nhiều cột: UNIQUE(CinemaId, RuleType, RuleKey)

## AudienceDiscounts

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| AudienceType | VARCHAR(20) | Không | PK |  | CHECK: ADULT, U22, CHILD_U13, CHILD_13_15, SENIOR, MERIT, HARDSHIP, DISABLED_SEVERE, DISABLED_EXTREME |
| DiscountPercent | DECIMAL(5,2) | Không |  |  | CHECK: DiscountPercent BETWEEN 0 AND 100 |
| LegalMinPercent | DECIMAL(5,2) | Không |  |  | DEFAULT 0 |
| PosOnly | BIT | Không |  |  |  |
| RequiresDocument | BIT | Không |  |  |  |

## Bills

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Code | VARCHAR(30) | Không |  |  | UNIQUE |
| Channel | VARCHAR(20) | Không |  |  | CHECK: POS, ONLINE, CORPORATE |
| CinemaId | INT | Không | FK | Cinemas.Id |  |
| ShowtimeId | INT | Có | FK | Showtimes.Id |  |
| UserId | INT | Có | FK | Users.Id |  |
| CustomerId | INT | Có | FK | Users.Id |  |
| PosShiftId | INT | Có | FK | PosShifts.Id |  |
| TerminalCode | VARCHAR(10) | Có |  |  |  |
| Status | VARCHAR(10) | Không |  |  | CHECK: waiting, finish, cancel; DEFAULT 'waiting' |
| CancelReason | VARCHAR(30) | Có |  |  | CHECK: EXPIRED, CANCELLED_BEFORE_PAYMENT, CANCELLED_BY_USER, VOIDED_BY_POS, OVERBOOKED_REFUNDED, INCIDENT_COMPENSATED |
| CreatedAt | DATETIME2(0) | Không |  |  |  |
| PaidAt | DATETIME2(0) | Có |  |  |  |
| CancelledAt | DATETIME2(0) | Có |  |  |  |
| TicketSubtotal | BIGINT | Không |  |  | DEFAULT 0 |
| FnbSubtotal | BIGINT | Không |  |  | DEFAULT 0 |
| DiscountTotal | BIGINT | Không |  |  | DEFAULT 0 |
| Total | BIGINT | Không |  |  | CHECK: Total >= 0; DEFAULT 0 |
| VoucherId | INT | Có | FK | Vouchers.Id |  |
| PointsUsed | INT | Không |  |  | DEFAULT 0 |
| HoldExpiresAt | DATETIME2(0) | Có |  |  |  |
| HoldExtended | BIT | Không |  |  | DEFAULT 0 |
| CheckInStatus | VARCHAR(30) | Không |  |  | CHECK: NONE, NOT_CHECKED_IN, PARTIALLY_CHECKED_IN, FULLY_CHECKED_IN, VOIDED; DEFAULT 'NONE' |
| FnbStatus | VARCHAR(30) | Không |  |  | CHECK: NONE, PENDING_PICKUP, PREPARING, READY_FOR_PICKUP, FULFILLED, VOIDED; DEFAULT 'NONE' |
| PickupNumber | INT | Có |  |  | CHECK: PickupNumber IS NULL OR PickupNumber BETWEEN 1 AND 999 |
| GuardianConfirmed | BIT | Không |  |  | DEFAULT 0 |
| AgeConfirmed | BIT | Không |  |  | DEFAULT 0 |
| CorporateBookingId | INT | Có | FK | CorporateBookings.Id |  |

Ràng buộc nhiều cột: Index (Status, PaidAt); Index (ShowtimeId); Index (CustomerId)

## BillDetails

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| BillId | INT | Không | FK | Bills.Id |  |
| DrinkId | INT | Không | FK | Drinks.Id |  |
| Quantity | INT | Không |  |  | CHECK: Quantity > 0 |
| Price | BIGINT | Không |  |  | CHECK: Price >= 0 |
| VatRate | DECIMAL(5,2) | Không |  |  |  |
| LineFinalAmount | BIGINT | Không |  |  | CHECK: LineFinalAmount >= 0 |

Ràng buộc nhiều cột: Index (BillId)

## Tickets

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| BillId | INT | Không | FK | Bills.Id |  |
| ShowtimeId | INT | Không | FK | Showtimes.Id |  |
| SeatId | INT | Không | FK | Seats.Id |  |
| AudienceType | VARCHAR(20) | Không |  |  | CHECK: ADULT, U22, CHILD_U13, CHILD_13_15, SENIOR, MERIT, HARDSHIP, DISABLED_SEVERE, DISABLED_EXTREME |
| ListPrice | BIGINT | Không |  |  | CHECK: ListPrice >= 0 |
| FinalPrice | BIGINT | Không |  |  | CHECK: FinalPrice >= 0 |
| VatRate | DECIMAL(5,2) | Không |  |  |  |
| QrCode | VARCHAR(20) | Không |  |  | UNIQUE |
| CheckInStatus | VARCHAR(20) | Không |  |  | CHECK: NOT_CHECKED_IN, CHECKED_IN, SWAPPED, VOIDED; DEFAULT 'NOT_CHECKED_IN' |
| CheckedInAt | DATETIME2(0) | Có |  |  |  |
| CheckedInBy | INT | Có | FK | Users.Id |  |
| SwappedFromTicketId | INT | Có | FK | Tickets.Id |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

Ràng buộc nhiều cột: UNIQUE(ShowtimeId, SeatId); Index (BillId); Index (ShowtimeId, SeatId)

## Payments

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| BillId | INT | Không | FK | Bills.Id |  |
| PosShiftId | INT | Có | FK | PosShifts.Id |  |
| Method | VARCHAR(20) | Không |  |  | CHECK: CASH, VIETQR_SIM, WALLET |
| Direction | VARCHAR(10) | Không |  |  | CHECK: IN, REFUND |
| Amount | BIGINT | Không |  |  | CHECK: Amount >= 0 |
| Status | VARCHAR(20) | Không |  |  | CHECK: PENDING, SUCCEEDED, FAILED |
| ProviderTxnId | VARCHAR(100) | Có |  |  | UNIQUE |
| RawPayload | NVARCHAR(MAX) | Có |  |  |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

Ràng buộc nhiều cột: Index (BillId); Index (ProviderTxnId)

## Vouchers

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Code | VARCHAR(30) | Không |  |  | UNIQUE |
| Type | VARCHAR(20) | Không |  |  | CHECK: AMOUNT, PERCENT, FREE_TICKET |
| Value | BIGINT | Không |  |  | CHECK: PERCENT |
| MinBillAmount | BIGINT | Không |  |  | DEFAULT 0 |
| ValidFrom | DATETIME2(0) | Không |  |  |  |
| ValidUntil | DATETIME2(0) | Không |  |  |  |
| UsageLimit | INT | Không |  |  | DEFAULT 1 |
| UsedCount | INT | Không |  |  | DEFAULT 0 |
| OwnerUserId | INT | Có | FK | Users.Id |  |
| Source | VARCHAR(20) | Không |  |  | CHECK: PROMO, COMPENSATION, CORPORATE |
| CorporateBookingId | INT | Có | FK | CorporateBookings.Id |  |
| Active | BIT | Không |  |  | DEFAULT 1 |

## WalletTransactions

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| UserId | INT | Không | FK | Users.Id |  |
| Amount | BIGINT | Không |  |  |  |
| BalanceAfter | BIGINT | Không |  |  | CHECK: BalanceAfter >= 0 |
| Reason | NVARCHAR(300) | Không |  |  |  |
| BillId | INT | Có | FK | Bills.Id |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## PointTransactions

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| UserId | INT | Không | FK | Users.Id |  |
| Points | INT | Không |  |  |  |
| Reason | NVARCHAR(300) | Không |  |  |  |
| BillId | INT | Có | FK | Bills.Id |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## CorporateBookings

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| CompanyName | NVARCHAR(200) | Không |  |  |  |
| ContactName | NVARCHAR(150) | Không |  |  |  |
| Phone | VARCHAR(20) | Có |  |  |  |
| Email | NVARCHAR(256) | Có |  |  |  |
| Type | VARCHAR(30) | Không |  |  | CHECK: PRIVATE_SCREENING, VOUCHER_BATCH |
| ShowtimeId | INT | Có | FK | Showtimes.Id |  |
| VoucherCount | INT | Có |  |  |  |
| ContractAmount | BIGINT | Không |  |  | DEFAULT 0 |
| BillId | INT | Có | FK | Bills.Id |  |
| CreatedBy | INT | Không | FK | Users.Id |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## PosShifts

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| UserId | INT | Không | FK | Users.Id | UNIQUE |
| CinemaId | INT | Không | FK | Cinemas.Id |  |
| TerminalCode | VARCHAR(10) | Không |  |  |  |
| OpenedAt | DATETIME2(0) | Không |  |  |  |
| OpeningCashFloat | BIGINT | Không |  |  | DEFAULT 0 |
| ClosedAt | DATETIME2(0) | Có |  |  |  |
| ActualCashCounted | BIGINT | Có |  |  |  |
| ExpectedCash | BIGINT | Có |  |  |  |
| Discrepancy | BIGINT | Có |  |  |  |
| Status | VARCHAR(10) | Không |  |  | CHECK: OPEN, CLOSED |

Ràng buộc nhiều cột: UNIQUE(CinemaId, TerminalCode); Index (CinemaId, TerminalCode); Index (UserId)

## SupervisorApprovals

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Type | VARCHAR(20) | Không |  |  | CHECK: VOID_BILL |
| BillId | INT | Không | FK | Bills.Id |  |
| RequestedBy | INT | Không | FK | Users.Id |  |
| Reason | NVARCHAR(500) | Không |  |  |  |
| Status | VARCHAR(20) | Không |  |  | CHECK: PENDING, APPROVED, REJECTED, EXPIRED; DEFAULT 'PENDING' |
| Method | VARCHAR(10) | Có |  |  | CHECK: PIN, REMOTE |
| DecidedBy | INT | Có | FK | Users.Id |  |
| DecidedAt | DATETIME2(0) | Có |  |  |  |
| ExpiresAt | DATETIME2(0) | Không |  |  |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## Notifications

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| UserId | INT | Không | FK | Users.Id |  |
| Title | NVARCHAR(200) | Không |  |  |  |
| Body | NVARCHAR(1000) | Không |  |  |  |
| Link | NVARCHAR(300) | Có |  |  |  |
| IsRead | BIT | Không |  |  | DEFAULT 0 |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## AuditLogs

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| UserId | INT | Có | FK | Users.Id |  |
| Action | VARCHAR(50) | Không |  |  |  |
| EntityType | VARCHAR(50) | Không |  |  |  |
| EntityId | NVARCHAR(50) | Có |  |  |  |
| Detail | NVARCHAR(MAX) | Có |  |  |  |
| CreatedAt | DATETIME2(0) | Không |  |  |  |

## SchemaVersions

| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |
|---|---|---|---|---|---|
| Id | INT | Không | PK |  | IDENTITY |
| Version | NVARCHAR(100) | Không |  |  | UNIQUE |
| AppliedAt | DATETIME2(0) | Không |  |  |  |

## Quan hệ khóa ngoại (dùng cho ERD)

| Bảng con | Cột | Bảng cha | Bản số |
|---|---|---|---|
| Users | CinemaId | Cinemas | Cinemas 0..1 – 0..n Users |
| PasswordResetOtps | UserId | Users | Users 1 – 0..n PasswordResetOtps |
| Drinks | CategoryId | Categories | Categories 1 – 0..n Drinks |
| RawMaterials | CinemaId | Cinemas | Cinemas 1 – 0..n RawMaterials |
| ProductRecipes | DrinkId | Drinks | Drinks 1 – 0..n ProductRecipes |
| ProductRecipes | RawMaterialId | RawMaterials | RawMaterials 1 – 0..n ProductRecipes |
| InventoryTransactions | RawMaterialId | RawMaterials | RawMaterials 1 – 0..n InventoryTransactions |
| InventoryTransactions | BillId | Bills | Bills 0..1 – 0..n InventoryTransactions |
| InventoryTransactions | WastageLogId | WastageLogs | WastageLogs 0..1 – 0..n InventoryTransactions |
| InventoryTransactions | CreatedBy | Users | Users 0..1 – 0..n InventoryTransactions |
| WastageLogs | CinemaId | Cinemas | Cinemas 1 – 0..n WastageLogs |
| WastageLogs | CreatedBy | Users | Users 1 – 0..n WastageLogs |
| WastageLogs | DecidedBy | Users | Users 0..1 – 0..n WastageLogs |
| WastageLogItems | WastageLogId | WastageLogs | WastageLogs 1 – 0..n WastageLogItems |
| WastageLogItems | RawMaterialId | RawMaterials | RawMaterials 1 – 0..n WastageLogItems |
| Movies | DistributorId | Distributors | Distributors 0..1 – 0..n Movies |
| FilmRentalRules | MovieId | Movies | Movies 1 – 0..n FilmRentalRules |
| KdmLicenses | MovieId | Movies | Movies 1 – 0..n KdmLicenses |
| KdmLicenses | CinemaId | Cinemas | Cinemas 1 – 0..n KdmLicenses |
| Auditoriums | CinemaId | Cinemas | Cinemas 1 – 0..n Auditoriums |
| Seats | AuditoriumId | Auditoriums | Auditoriums 1 – 0..n Seats |
| Showtimes | MovieId | Movies | Movies 1 – 0..n Showtimes |
| Showtimes | AuditoriumId | Auditoriums | Auditoriums 1 – 0..n Showtimes |
| ShowtimeSeats | ShowtimeId | Showtimes | Showtimes 1 – 0..n ShowtimeSeats |
| ShowtimeSeats | SeatId | Seats | Seats 1 – 0..n ShowtimeSeats |
| ShowtimeSeats | HoldBillId | Bills | Bills 0..1 – 0..n ShowtimeSeats |
| ShowtimeSeats | TicketId | Tickets | Tickets 0..1 – 0..n ShowtimeSeats |
| PricingRules | CinemaId | Cinemas | Cinemas 1 – 0..n PricingRules |
| Bills | CinemaId | Cinemas | Cinemas 1 – 0..n Bills |
| Bills | ShowtimeId | Showtimes | Showtimes 0..1 – 0..n Bills |
| Bills | UserId | Users | Users 0..1 – 0..n Bills |
| Bills | CustomerId | Users | Users 0..1 – 0..n Bills |
| Bills | PosShiftId | PosShifts | PosShifts 0..1 – 0..n Bills |
| Bills | VoucherId | Vouchers | Vouchers 0..1 – 0..n Bills |
| Bills | CorporateBookingId | CorporateBookings | CorporateBookings 0..1 – 0..n Bills |
| BillDetails | BillId | Bills | Bills 1 – 0..n BillDetails |
| BillDetails | DrinkId | Drinks | Drinks 1 – 0..n BillDetails |
| Tickets | BillId | Bills | Bills 1 – 0..n Tickets |
| Tickets | ShowtimeId | Showtimes | Showtimes 1 – 0..n Tickets |
| Tickets | SeatId | Seats | Seats 1 – 0..n Tickets |
| Tickets | CheckedInBy | Users | Users 0..1 – 0..n Tickets |
| Tickets | SwappedFromTicketId | Tickets | Tickets 0..1 – 0..n Tickets |
| Payments | BillId | Bills | Bills 1 – 0..n Payments |
| Payments | PosShiftId | PosShifts | PosShifts 0..1 – 0..n Payments |
| Vouchers | OwnerUserId | Users | Users 0..1 – 0..n Vouchers |
| Vouchers | CorporateBookingId | CorporateBookings | CorporateBookings 0..1 – 0..n Vouchers |
| WalletTransactions | UserId | Users | Users 1 – 0..n WalletTransactions |
| WalletTransactions | BillId | Bills | Bills 0..1 – 0..n WalletTransactions |
| PointTransactions | UserId | Users | Users 1 – 0..n PointTransactions |
| PointTransactions | BillId | Bills | Bills 0..1 – 0..n PointTransactions |
| CorporateBookings | ShowtimeId | Showtimes | Showtimes 0..1 – 0..n CorporateBookings |
| CorporateBookings | CreatedBy | Users | Users 1 – 0..n CorporateBookings |
| CorporateBookings | BillId | Bills | Bills 0..1 – 0..n CorporateBookings |
| PosShifts | UserId | Users | Users 1 – 0..1 PosShifts |
| PosShifts | CinemaId | Cinemas | Cinemas 1 – 0..n PosShifts |
| SupervisorApprovals | BillId | Bills | Bills 1 – 0..n SupervisorApprovals |
| SupervisorApprovals | RequestedBy | Users | Users 1 – 0..n SupervisorApprovals |
| SupervisorApprovals | DecidedBy | Users | Users 0..1 – 0..n SupervisorApprovals |
| Notifications | UserId | Users | Users 1 – 0..n Notifications |
| AuditLogs | UserId | Users | Users 0..1 – 0..n AuditLogs |

## Stored procedure

- sp_top_5_best_selling_drinks(@StartDate, @EndDate) – src/FPolyCinema.Web/Database/02_procedures.sql
- sp_top_5_best_selling_movies(@StartDate, @EndDate) – src/FPolyCinema.Web/Database/02_procedures.sql
- sp_revenue_by_day(@StartDate, @EndDate) – src/FPolyCinema.Web/Database/02_procedures.sql
- sp_box_office_settlement(@MovieId, @WeekNumber, @CinemaId) – src/FPolyCinema.Web/Database/02_procedures.sql
