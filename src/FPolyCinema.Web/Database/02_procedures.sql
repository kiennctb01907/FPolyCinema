/*
  FPolyCinema – 02_procedures.sql
  Bốn stored procedure thống kê (CONTRACTS mục 10, SPEC 3.12). Viết bằng CREATE OR ALTER nên
  DbInitializer chạy lại file này mỗi lần khởi động.
  Quy tắc chung (SPEC 3.12):
  - Chỉ tính hóa đơn Status = 'finish'; vé SWAPPED và VOIDED không tính.
  - Doanh thu dùng Tickets.FinalPrice và BillDetails.LineFinalAmount (đã gồm VAT, đã phân bổ giảm giá).
  - Ngày tính theo Bills.PaidAt; khoảng [@StartDate, @EndDate] gồm cả hai đầu.
  - Không gọi hàm lấy giờ của SQL Server: khoảng thời gian luôn là tham số.
*/

CREATE OR ALTER PROCEDURE sp_top_5_best_selling_drinks
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 5
        d.Id AS DrinkId,
        d.Name AS DrinkName,
        SUM(bd.Quantity) AS TotalQuantitySold,
        CAST(SUM(bd.LineFinalAmount) AS BIGINT) AS TotalRevenue
    FROM BillDetails bd
    JOIN Bills b ON b.Id = bd.BillId
    JOIN Drinks d ON d.Id = bd.DrinkId
    WHERE b.Status = 'finish'
      AND b.PaidAt >= CAST(@StartDate AS DATETIME2(0))
      AND b.PaidAt < DATEADD(DAY, 1, CAST(@EndDate AS DATETIME2(0)))
    GROUP BY d.Id, d.Name
    ORDER BY TotalQuantitySold DESC, TotalRevenue DESC, d.Id;
END
GO

CREATE OR ALTER PROCEDURE sp_top_5_best_selling_movies
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 5
        m.Id AS MovieId,
        m.Title,
        COUNT(*) AS TicketsSold,
        CAST(SUM(t.FinalPrice) AS BIGINT) AS TicketRevenue
    FROM Tickets t
    JOIN Bills b ON b.Id = t.BillId
    JOIN Showtimes s ON s.Id = t.ShowtimeId
    JOIN Movies m ON m.Id = s.MovieId
    WHERE b.Status = 'finish'
      AND t.CheckInStatus NOT IN ('SWAPPED', 'VOIDED')
      AND b.PaidAt >= CAST(@StartDate AS DATETIME2(0))
      AND b.PaidAt < DATEADD(DAY, 1, CAST(@EndDate AS DATETIME2(0)))
    GROUP BY m.Id, m.Title
    ORDER BY TicketRevenue DESC, TicketsSold DESC, m.Id;
END
GO

CREATE OR ALTER PROCEDURE sp_revenue_by_day
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Ngày nào trong khoảng cũng có một dòng, ngày không bán ghi 0
    WITH Days AS (
        SELECT @StartDate AS RevenueDate
        WHERE @StartDate <= @EndDate
        UNION ALL
        SELECT DATEADD(DAY, 1, RevenueDate) FROM Days WHERE RevenueDate < @EndDate
    ),
    PaidBills AS (
        SELECT b.Id, CAST(b.PaidAt AS DATE) AS PaidDate
        FROM Bills b
        WHERE b.Status = 'finish'
          AND b.PaidAt >= CAST(@StartDate AS DATETIME2(0))
          AND b.PaidAt < DATEADD(DAY, 1, CAST(@EndDate AS DATETIME2(0)))
    ),
    TicketSums AS (
        SELECT t.BillId, SUM(t.FinalPrice) AS Amount
        FROM Tickets t
        JOIN PaidBills pb ON pb.Id = t.BillId
        WHERE t.CheckInStatus NOT IN ('SWAPPED', 'VOIDED')
        GROUP BY t.BillId
    ),
    FnbSums AS (
        SELECT bd.BillId, SUM(bd.LineFinalAmount) AS Amount
        FROM BillDetails bd
        JOIN PaidBills pb ON pb.Id = bd.BillId
        GROUP BY bd.BillId
    )
    SELECT
        d.RevenueDate,
        COUNT(pb.Id) AS TotalBills,
        CAST(COALESCE(SUM(ts.Amount), 0) AS BIGINT) AS TicketRevenue,
        CAST(COALESCE(SUM(fs.Amount), 0) AS BIGINT) AS FnbRevenue,
        CAST(COALESCE(SUM(ts.Amount), 0) + COALESCE(SUM(fs.Amount), 0) AS BIGINT) AS TotalRevenue
    FROM Days d
    LEFT JOIN PaidBills pb ON pb.PaidDate = d.RevenueDate
    LEFT JOIN TicketSums ts ON ts.BillId = pb.Id
    LEFT JOIN FnbSums fs ON fs.BillId = pb.Id
    GROUP BY d.RevenueDate
    ORDER BY d.RevenueDate
    OPTION (MAXRECURSION 0);
END
GO

CREATE OR ALTER PROCEDURE sp_box_office_settlement
    @MovieId INT,
    @WeekNumber INT,
    @CinemaId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @WeekNumber IS NULL OR @WeekNumber < 1
        THROW 50001, N'Số tuần đối soát phải từ 1 trở lên.', 1;

    DECLARE @Title NVARCHAR(200), @ReleaseDate DATE, @MinimumTicketPrice BIGINT;
    SELECT @Title = Title, @ReleaseDate = ReleaseDate, @MinimumTicketPrice = MinimumTicketPrice
    FROM Movies
    WHERE Id = @MovieId;

    IF @ReleaseDate IS NULL
        THROW 50002, N'Không tìm thấy phim cần đối soát.', 1;

    -- Tuần n là khoảng [ReleaseDate + 7(n - 1), ReleaseDate + 7n), theo ngày vận hành của suất
    DECLARE @WeekStart DATE = DATEADD(DAY, 7 * (@WeekNumber - 1), @ReleaseDate);
    DECLARE @WeekEndExclusive DATE = DATEADD(DAY, 7 * @WeekNumber, @ReleaseDate);

    -- Tỷ lệ của nhà phát hành: quy tắc của tuần n; vượt quá quy tắc cuối cùng thì dùng quy tắc cuối cùng
    DECLARE @SharePercent DECIMAL(5,2);
    SELECT TOP 1 @SharePercent = DistributorSharePercent
    FROM FilmRentalRules
    WHERE MovieId = @MovieId AND WeekNumber <= @WeekNumber
    ORDER BY WeekNumber DESC;
    IF @SharePercent IS NULL
        SELECT TOP 1 @SharePercent = DistributorSharePercent
        FROM FilmRentalRules
        WHERE MovieId = @MovieId
        ORDER BY WeekNumber ASC;
    SET @SharePercent = COALESCE(@SharePercent, 0);

    WITH SoldTickets AS (
        SELECT
            t.FinalPrice,
            -- Net = FinalPrice × 100 / (100 + VatRate); FloorNet = MinimumTicketPrice × 100 / (100 + VatRate)
            CAST(t.FinalPrice AS DECIMAL(19,4)) * 100 / (100 + t.VatRate) AS NetAmount,
            CAST(@MinimumTicketPrice AS DECIMAL(19,4)) * 100 / (100 + t.VatRate) AS FloorNet
        FROM Tickets t
        JOIN Bills b ON b.Id = t.BillId
        JOIN Showtimes s ON s.Id = t.ShowtimeId
        JOIN Auditoriums a ON a.Id = s.AuditoriumId
        WHERE s.MovieId = @MovieId
          AND b.Status = 'finish'
          AND t.CheckInStatus NOT IN ('SWAPPED', 'VOIDED')
          AND (@CinemaId IS NULL OR a.CinemaId = @CinemaId)
          -- Ngày vận hành: suất bắt đầu trước 06:00 thuộc ngày hôm trước
          AND CAST(DATEADD(HOUR, -6, s.StartTime) AS DATE) >= @WeekStart
          AND CAST(DATEADD(HOUR, -6, s.StartTime) AS DATE) < @WeekEndExclusive
    ),
    WithBase AS (
        -- Base = max(Net, FloorNet)
        SELECT FinalPrice, NetAmount, FloorNet,
               CASE WHEN NetAmount < FloorNet THEN FloorNet ELSE NetAmount END AS BaseAmount
        FROM SoldTickets
    ),
    Totals AS (
        SELECT
            COUNT(*) AS TicketCount,
            COALESCE(SUM(FinalPrice), 0) AS GrossRevenue,
            COALESCE(SUM(NetAmount), 0) AS NetRevenue,
            COALESCE(SUM(CASE WHEN NetAmount < FloorNet THEN 1 ELSE 0 END), 0) AS BelowFloorTickets,
            COALESCE(SUM(CASE WHEN NetAmount < FloorNet THEN FloorNet - NetAmount ELSE 0 END), 0) AS FloorAbsorbed,
            COALESCE(SUM(BaseAmount), 0) AS BaseTotal
        FROM WithBase
    )
    SELECT
        @MovieId AS MovieId,
        @Title AS Title,
        @WeekNumber AS WeekNumber,
        @CinemaId AS CinemaId,
        @WeekStart AS WeekStartDate,
        DATEADD(DAY, -1, @WeekEndExclusive) AS WeekEndDate,
        @SharePercent AS DistributorSharePercent,
        TicketCount,
        CAST(GrossRevenue AS BIGINT) AS GrossRevenue,
        CAST(ROUND(NetRevenue, 0) AS BIGINT) AS NetRevenue,
        BelowFloorTickets,
        CAST(ROUND(FloorAbsorbed, 0) AS BIGINT) AS FloorAbsorbedAmount,
        CAST(ROUND(BaseTotal * @SharePercent / 100, 0) AS BIGINT) AS DistributorAmount,
        CAST(ROUND(NetRevenue, 0) - ROUND(BaseTotal * @SharePercent / 100, 0) AS BIGINT) AS CinemaRetainedAmount
    FROM Totals;
END
GO
