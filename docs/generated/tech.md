# Công nghệ, thư viện, cấu trúc mã nguồn (sinh từ code)

## Gói NuGet

| Project | Gói | Phiên bản |
|---|---|---|
| src/FPolyCinema.Web/FPolyCinema.Web.csproj (net8.0) | | |
| | Microsoft.Data.SqlClient | 5.2.2 |
| | Microsoft.Extensions.Identity.Core | 8.0.11 |
| | QRCoder | 1.6.0 |
| tests/FPolyCinema.Tests/FPolyCinema.Tests.csproj (net8.0) | | |
| | Microsoft.AspNetCore.Mvc.Testing | 8.0.11 |
| | Microsoft.NET.Test.Sdk | 17.11.1 |
| | xunit | 2.9.2 |
| | xunit.runner.visualstudio | 2.8.2 |

## Thư viện giao diện (libman.json)

- bootstrap@5.3.3 → wwwroot/lib/bootstrap/
- bootstrap-icons@1.11.3 → wwwroot/lib/bootstrap-icons/
- chart.js@4.4.9 → wwwroot/lib/chart.js/
- html5-qrcode@2.3.8 → wwwroot/lib/html5-qrcode/
- @fontsource/be-vietnam-pro@5.2.5 → wwwroot/lib/be-vietnam-pro/
- @microsoft/signalr@8.0.7 → wwwroot/lib/signalr/
- jquery@3.7.1 → wwwroot/lib/jquery/
- jquery-validation@1.21.0 → wwwroot/lib/jquery-validation/
- jquery-validation-unobtrusive@4.0.0 → wwwroot/lib/jquery-validation-unobtrusive/

Thư mục wwwroot/lib: be-vietnam-pro, bootstrap, bootstrap-icons, chart.js, html5-qrcode, jquery, jquery-validation, jquery-validation-unobtrusive, signalr

## SignalR

- src/FPolyCinema.Web/Hubs/CinemaHub.cs: JoinShowtime, LeaveShowtime, JoinBill, LeaveBill

## Kiểm thử

267 test trong 25 file.

- tests/FPolyCinema.Tests/ArchitectureTests.cs: 8
- tests/FPolyCinema.Tests/BillStateMachineTests.cs: 14
- tests/FPolyCinema.Tests/ConventionTests.cs: 4
- tests/FPolyCinema.Tests/DatabaseTests.cs: 14
- tests/FPolyCinema.Tests/FileUtilTests.cs: 7
- tests/FPolyCinema.Tests/InfrastructureTests.cs: 12
- tests/FPolyCinema.Tests/Phase2RulesTests.cs: 19
- tests/FPolyCinema.Tests/Phase2ServiceTests.cs: 9
- tests/FPolyCinema.Tests/Phase2WebTests.cs: 10
- tests/FPolyCinema.Tests/Phase3RulesTests.cs: 26
- tests/FPolyCinema.Tests/Phase3ServiceTests.cs: 6
- tests/FPolyCinema.Tests/Phase3WebTests.cs: 3
- tests/FPolyCinema.Tests/Phase4RulesTests.cs: 18
- tests/FPolyCinema.Tests/Phase4ServiceTests.cs: 15
- tests/FPolyCinema.Tests/Phase4WebTests.cs: 3
- tests/FPolyCinema.Tests/Phase5RulesTests.cs: 12
- tests/FPolyCinema.Tests/Phase5ServiceTests.cs: 12
- tests/FPolyCinema.Tests/Phase5WebTests.cs: 6
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
      SeatHoldSweeper.cs
    Controllers/
      (19 file: .gitkeep, AccountController.cs, AppControllerBase.cs, AuditoriumsController.cs, BillsController.cs, CategoriesController.cs, …)
    DAL/
      (45 file: AccountDAL.Phase2.cs, AudienceDiscountDAL.cs, AuditLogDAL.cs, AuditoriumDAL.cs, BillDAL.cs, BillDetailDAL.cs, …)
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
        Policies.cs
        PricingConstants.cs
        Roles.cs
      Rules/
        (23 file: AgeRules.cs, AuditoriumLayout.cs, BillStateMachine.cs, CashDropCalculator.cs, ClusterFinder.cs, CodeGenerator.cs, …)
      BusinessRuleException.cs
      FieldErrors.cs
    Entities/
      (38 file: AudienceDiscount.cs, AuditLog.cs, Auditorium.cs, Bill.cs, BillDetail.cs, Category.cs, …)
    Hubs/
      .gitkeep
      CinemaHub.cs
    Infrastructure/
      Seed/
        SchedulePlanner.cs
        SeatLayoutPlanner.cs
        SeedCatalog.cs
      Web/
        ApiResponse.cs
        Flash.cs
        Middlewares.cs
        QueryUrl.cs
      (14 file: AppOptions.cs, CinemaRules.cs, DbInitializer.cs, DbSeeder.cs, DbSeeder.Finalize.cs, DbSeeder.Sales.cs, …)
    Properties/
      launchSettings.json
    Services/
      (21 file: AuditoriumService.cs, AuthService.cs, BillService.cs, BillStatusService.cs, CatalogServices.cs, DevToolsService.cs, …)
    Utils/
      DBUtil.cs
      FileUtil.cs
      PasswordUtil.cs
    ViewModels/
      .gitkeep
      AccountVms.cs
      AdminVms.cs
      CustomerVms.cs
      Phase3Vms.cs
      Phase4AdminVms.cs
      PosVms.cs
      SharedVms.cs
      StyleGuideSamples.cs
    Views/
      Account/
        _AccountBill.cshtml
        _ProfileForm.cshtml
        Index.cshtml
      Auditoriums/
        Design.cshtml
        Form.cshtml
        Index.cshtml
      Bills/
        _Table.cshtml
        Details.cshtml
        Index.cshtml
      Booking/
        Index.cshtml
        Pay.cshtml
      Categories/
        _Table.cshtml
        Details.cshtml
        Form.cshtml
        Index.cshtml
      Customer/
        _Schedule.cshtml
        _ScheduleFilter.cshtml
        Menu.cshtml
        Movie.cshtml
        Movies.cshtml
        Showtimes.cshtml
      Dev/
        _StyleGuideBody.cshtml
        LayoutDisplay.cshtml
        LayoutPos.cshtml
        Mailbox.cshtml
        StyleGuide.cshtml
      Distributors/
        _Table.cshtml
        Form.cshtml
        Index.cshtml
      Drinks/
        _Table.cshtml
        Details.cshtml
        Form.cshtml
        Index.cshtml
      Home/
        Cancellation.cshtml
        Error.cshtml
        Index.cshtml
        Terms.cshtml
      Login/
        _ChangePasswordFields.cshtml
        _FormErrors.cshtml
        ChangePassword.cshtml
        ForgotPassword.cshtml
        Login.cshtml
        Me.cshtml
        Profile.cshtml
        Register.cshtml
      Movies/
        _Table.cshtml
        Details.cshtml
        Form.cshtml
        Index.cshtml
      Payment/
        Sim.cshtml
      Pos/
        _PosModals.cshtml
        _SeatMapRegion.cshtml
        _VoidModal.cshtml
        CloseShift.cshtml
        Index.cshtml
        MyBills.cshtml
        OpenShift.cshtml
        Print.cshtml
        ShiftReport.cshtml
      Pricing/
        _Tabs.cshtml
        Audiences.cshtml
        Holidays.cshtml
        Index.cshtml
        Try.cshtml
      Scheduler/
        Form.cshtml
        Index.cshtml
      Shared/
        (32 file: _AdminSidebar.cshtml, _AgeBadge.cshtml, _ApprovalBell.cshtml, _AuthArt.cshtml, _Chart.cshtml, _CinemaPicker.cshtml, …)
      Statistics/
        Dashboard.cshtml
        Reports.cshtml
        Settlement.cshtml
      Tickets/
        Show.cshtml
      Users/
        _Table.cshtml
        Details.cshtml
        Form.cshtml
        Index.cshtml
      _ViewImports.cshtml
      _ViewStart.cshtml
      .gitkeep
    wwwroot/
      css/
        .gitkeep
        app.css
        tokens.css
      img/
        favicon.svg
        placeholder-food.svg
        placeholder-poster.svg
      js/
        pages/
        .gitkeep
        charts.js
        realtime.js
        seatmap.js
        ui.js
    appsettings.Development.json
    appsettings.json
    FPolyCinema.Web.csproj
    libman.json
    Program.cs
tests/
  FPolyCinema.Tests/
    (27 file: ArchitectureTests.cs, BillStateMachineTests.cs, ConventionTests.cs, DatabaseTests.cs, FileUtilTests.cs, FPolyCinema.Tests.csproj, …)
tools/
  spec-check/
    check.mjs
    scope.json
  ui-snapshots/
    routes.json
    snap.mjs
```
