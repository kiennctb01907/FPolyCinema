# Công nghệ, thư viện, cấu trúc mã nguồn (sinh từ code)

## Gói NuGet

| Project | Gói | Phiên bản |
|---|---|---|
| src/FPolyCinema.Web/FPolyCinema.Web.csproj (net8.0) | | |
| | Microsoft.Data.SqlClient | 5.2.2 |
| | Microsoft.Extensions.Identity.Core | 8.0.11 |
| tests/FPolyCinema.Tests/FPolyCinema.Tests.csproj (net8.0) | | |
| | Microsoft.NET.Test.Sdk | 17.11.1 |
| | xunit | 2.9.2 |
| | xunit.runner.visualstudio | 2.8.2 |

## Thư viện giao diện (libman.json)

- (không có libman.json)

## SignalR

- (chưa có hub)

## Kiểm thử

128 test trong 13 file.

- tests/FPolyCinema.Tests/ArchitectureTests.cs: 8
- tests/FPolyCinema.Tests/BillStateMachineTests.cs: 14
- tests/FPolyCinema.Tests/ConventionTests.cs: 4
- tests/FPolyCinema.Tests/DatabaseTests.cs: 14
- tests/FPolyCinema.Tests/FileUtilTests.cs: 7
- tests/FPolyCinema.Tests/InfrastructureTests.cs: 12
- tests/FPolyCinema.Tests/PricingCalculatorTests.cs: 17
- tests/FPolyCinema.Tests/SeatScoringTests.cs: 10
- tests/FPolyCinema.Tests/SeedDataTests.cs: 13
- tests/FPolyCinema.Tests/SeededDatabaseTests.cs: 8
- tests/FPolyCinema.Tests/ShowtimeRulesTests.cs: 9
- tests/FPolyCinema.Tests/StoredProcedureTests.cs: 2
- tests/FPolyCinema.Tests/SvgArtGeneratorTests.cs: 10

## Cây thư mục

```
src/
  FPolyCinema.Web/
    Background/
      .gitkeep
    Controllers/
      .gitkeep
    DAL/
      (37 file: AudienceDiscountDAL.cs, AuditLogDAL.cs, AuditoriumDAL.cs, BillDAL.cs, BillDetailDAL.cs, CategoryDAL.cs, …)
    Database/
      01_schema.sql
      02_procedures.sql
    Domain/
      Constants/
        AuditoriumConstants.cs
        BillConstants.cs
        ErrorCodes.cs
        LoyaltyConstants.cs
        MovieConstants.cs
        OperationConstants.cs
        PricingConstants.cs
        Roles.cs
      Rules/
        BillStateMachine.cs
        CodeGenerator.cs
        PricingCalculator.cs
        SeatLabeler.cs
        SeatScoring.cs
        ShowtimeTimeCalculator.cs
      BusinessRuleException.cs
    Entities/
      (35 file: AudienceDiscount.cs, AuditLog.cs, Auditorium.cs, Bill.cs, BillDetail.cs, Category.cs, …)
    Hubs/
      .gitkeep
    Infrastructure/
      Seed/
        SchedulePlanner.cs
        SeatLayoutPlanner.cs
        SeedCatalog.cs
      CinemaRules.cs
      DbInitializer.cs
      DbSeeder.cs
      DbSeeder.Finalize.cs
      DbSeeder.Sales.cs
      IClock.cs
      SvgArtGenerator.cs
      SystemClock.cs
    Properties/
      launchSettings.json
    Services/
      BillStatusService.cs
    Utils/
      DBUtil.cs
      FileUtil.cs
      PasswordUtil.cs
    ViewModels/
      .gitkeep
    Views/
      .gitkeep
    wwwroot/
      css/
        .gitkeep
      js/
        .gitkeep
    appsettings.Development.json
    appsettings.json
    FPolyCinema.Web.csproj
    Program.cs
tests/
  FPolyCinema.Tests/
    (15 file: ArchitectureTests.cs, BillStateMachineTests.cs, ConventionTests.cs, DatabaseTests.cs, FileUtilTests.cs, FPolyCinema.Tests.csproj, …)
tools/
  spec-check/
    check.mjs
    scope.json
```
