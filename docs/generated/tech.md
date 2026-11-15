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

- src/FPolyCinema.Web/Hubs/CinemaHub.cs: JoinShowtime, LeaveShowtime, JoinBill, LeaveBill, JoinTerminal, LeaveTerminal, JoinAuditorium, LeaveAuditorium, JoinKds, LeaveKds, JoinOps, LeaveOps, JoinManagerAlerts, LeaveManagerAlerts, JoinCinema, LeaveCinema
- src/FPolyCinema.Web/Hubs/HubErrorFilter.cs: 

## Kiểm thử

366 test trong 33 file.

- tests/FPolyCinema.Tests/ArchitectureTests.cs: 8
- tests/FPolyCinema.Tests/BillStateMachineTests.cs: 14
- tests/FPolyCinema.Tests/BookingAgentTests.cs: 13
- tests/FPolyCinema.Tests/ConventionTests.cs: 4
- tests/FPolyCinema.Tests/DatabaseTests.cs: 14
- tests/FPolyCinema.Tests/FileUtilTests.cs: 7
- tests/FPolyCinema.Tests/HubErrorFilterTests.cs: 3
- tests/FPolyCinema.Tests/InfrastructureTests.cs: 12
- tests/FPolyCinema.Tests/NlpParserTests.cs: 25
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
- tests/FPolyCinema.Tests/Phase6RulesTests.cs: 23
- tests/FPolyCinema.Tests/Phase6ServiceTests.cs: 5
- tests/FPolyCinema.Tests/Phase7RulesTests.cs: 11
- tests/FPolyCinema.Tests/Phase7ServiceTests.cs: 5
- tests/FPolyCinema.Tests/Phase8Tests.cs: 14
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
      ShowtimeClockService.cs
    Controllers/
      (27 file: .gitkeep, AccountController.cs, AppControllerBase.cs, AssistantController.cs, AuditoriumsController.cs, BillsController.cs, …)
    DAL/
      (47 file: AccountDAL.Phase2.cs, AudienceDiscountDAL.cs, AuditLogDAL.cs, AuditoriumDAL.cs, BillDAL.cs, BillDetailDAL.cs, …)
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
        (38 file: AgeRules.cs, AiDemandScore.cs, AiScheduler.cs, ApprovalRules.cs, AuditoriumLayout.cs, BillStateMachine.cs, …)
      BusinessRuleException.cs
      FieldErrors.cs
    Entities/
      (39 file: AudienceDiscount.cs, AuditLog.cs, Auditorium.cs, Bill.cs, BillDetail.cs, Category.cs, …)
    Hubs/
      .gitkeep
      CinemaHub.cs
      HubErrorFilter.cs
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
      (15 file: AppOptions.cs, CinemaRules.cs, DbInitializer.cs, DbSeeder.cs, DbSeeder.Finalize.cs, DbSeeder.Sales.cs, …)
    Properties/
      launchSettings.json
    Services/
      (35 file: AiSchedulerService.cs, ApprovalService.cs, AuditoriumService.cs, AuthService.cs, BillService.cs, BillStatusService.cs, …)
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
      Phase6Vms.cs
      Phase7Vms.cs
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
      Corporate/
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
      Demo/
        MultiScreen.cshtml
        Stage.cshtml
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
      Gate/
        Index.cshtml
      Home/
        Cancellation.cshtml
        Error.cshtml
        Index.cshtml
        Terms.cshtml
      Kitchen/
        _KdsCard.cshtml
        Kds.cshtml
        Wastage.cshtml
      Lobby/
        _DisplaySeatMap.cshtml
        _SignageRows.cshtml
        CustomerDisplay.cshtml
        DigitalSignage.cshtml
        PickupBoard.cshtml
      Login/
        _ChangePasswordFields.cshtml
        _FormErrors.cshtml
        ChangePassword.cshtml
        ForgotPassword.cshtml
        Login.cshtml
        Me.cshtml
        Profile.cshtml
        Register.cshtml
      Materials/
        _Table.cshtml
        Form.cshtml
        Import.cshtml
        Index.cshtml
      Movies/
        _Table.cshtml
        Details.cshtml
        Form.cshtml
        Index.cshtml
      Ops/
        _OpsBody.cshtml
        _OpsLive.cshtml
        Incident.cshtml
        Index.cshtml
        Wastage.cshtml
        WastageDetail.cshtml
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
        SwapSeat.cshtml
      Pricing/
        _Tabs.cshtml
        Audiences.cshtml
        Holidays.cshtml
        Index.cshtml
        Try.cshtml
      Scheduler/
        _AiModal.cshtml
        _Gantt.cshtml
        Form.cshtml
        Index.cshtml
      Shared/
        (36 file: _AdminSidebar.cshtml, _AgeBadge.cshtml, _ApprovalBell.cshtml, _ApprovalList.cshtml, _AssistantWidget.cshtml, _AuthArt.cshtml, …)
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
        approvals.js
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
    (35 file: ArchitectureTests.cs, BillStateMachineTests.cs, BookingAgentTests.cs, ConventionTests.cs, DatabaseTests.cs, FileUtilTests.cs, …)
tools/
  spec-check/
    check.mjs
    scope.json
  ui-snapshots/
    routes.json
    snap.mjs
```
