# Use case và bằng chứng trong code (sinh từ docs/trace)

| Mã | Use case | Tác nhân | Quan hệ | Màn hình | Phase | Trạng thái | Bằng chứng |
|---|---|---|---|---|---|---|---|
| UC01 | Đăng nhập, đăng xuất | Khách hàng, mọi nhân viên |  | C6 | 2 | Xong | src/FPolyCinema.Web/Controllers/LoginController.cs#public async Task<IActionResult> Login(LoginVm vm); src/FPolyCinema.Web/Controllers/LoginController.cs#public async Task<IActionResult> Logout(); test:Login_FormHasTokenAndRedirectsToRoleHome_LockedAccountRejected; ui:C6 |
| UC02 | Quên mật khẩu (OTP, cấp mật khẩu mới qua email) | Khách hàng, mọi nhân viên; Dịch vụ email | extend UC01 | C6 | 2 | Xong | src/FPolyCinema.Web/Services/PasswordResetService.cs#public class PasswordResetService; test:ForgotPassword_OtpFlow_WrongThenRightSendsNewPasswordAndForcesChange; ui:C6 |
| UC03 | Đổi mật khẩu | Khách hàng, mọi nhân viên | include UC01 | C5, A9 | 2, 5 | Chưa |  |
| UC04 | Cập nhật thông tin cá nhân | Khách hàng, mọi nhân viên | include UC01 | C5, A9 | 2, 5 | Chưa |  |
| UC05 | Đăng ký tài khoản | Khách vãng lai |  | C6 | 2 | Xong | src/FPolyCinema.Web/Services/AuthService.cs#public User Register(RegisterInput input); test:Register_CreatesMemberCustomerAndRejectsDuplicateEmail; ui:C6 |
| UC06 | Xem phim, lịch chiếu, thực đơn bắp nước | Khách vãng lai |  | C1, C2 | 5 | Chưa |  |
| UC07 | Đặt vé online | Khách hàng | include UC08, UC09 | C3 | 5 | Chưa |  |
| UC08 | Chọn ghế với gợi ý AI | Khách hàng, Thu ngân |  | C3, P1 | 4, 5 | Chưa |  |
| UC09 | Thanh toán online | Khách hàng; Cổng thanh toán |  | C3 | 5 | Chưa |  |
| UC10 | Xem vé điện tử, báo "Tôi đã tới rạp" | Khách hàng |  | C4 | 5, 6 | Chưa |  |
| UC11 | Tự hủy vé | Khách hàng |  | C4, C5 | 5 | Chưa |  |
| UC12 | Quản lý ví, điểm, voucher, thông báo | Khách hàng |  | C5 | 5 | Chưa |  |
| UC13 | Đặt vé bằng trợ lý AI | Khách hàng | extend UC07 | C7 | 7 | Chưa |  |
| UC14 | Mở ca, đóng ca mù | Thu ngân |  | P7 | 4 | Xong | src/FPolyCinema.Web/Controllers/PosController.cs#OpenShift; src/FPolyCinema.Web/Controllers/PosController.cs#CloseShift; test:CloseShift_BlockedByWaitingBill_ThenBlindCountComputesExpectedAndDiscrepancy |
| UC15 | Bán vé và bắp nước tại quầy | Thu ngân | include UC08 | P1, P2 | 4 | Xong | src/FPolyCinema.Web/Controllers/PosController.cs#Checkout; src/FPolyCinema.Web/Views/Pos/Index.cshtml#Hóa đơn tạm tính; test:Checkout_Cash_CreatesTicketsBooksSeatsConsumesStockEarnsPoints_ThenVoidWithPin |
| UC16 | In vé, nhận đơn online tại quầy | Thu ngân |  | P1 | 4, 6 | Chưa |  |
| UC17 | Đổi, nâng hạng ghế | Thu ngân |  | P1 | 6 | Chưa |  |
| UC18 | Yêu cầu hủy hóa đơn đã thanh toán | Thu ngân | include UC24 | P1 | 4, 6 | Chưa |  |
| UC19 | Xử lý đơn bắp nước tại bếp, gọi số | Thu ngân |  | P4, P5 | 6 | Chưa |  |
| UC20 | Lập phiếu hủy hàng hỏng | Thu ngân |  | P4 | 6 | Chưa |  |
| UC21 | Xem hóa đơn của tôi | Thu ngân |  | P1 | 4 | Xong | src/FPolyCinema.Web/Controllers/PosController.cs#MyBills; src/FPolyCinema.Web/Views/Pos/MyBills.cshtml#Hóa đơn của tôi; test:Pages_RoleAccess_PosForPosStaff_AdminForManager |
| UC22 | Soát vé, check-in từng phần | Nhân viên soát vé |  | P3 | 6 | Chưa |  |
| UC23 | Xác nhận dọn phòng xong | Nhân viên soát vé |  | P3, P6 | 6 | Chưa |  |
| UC24 | Duyệt hủy hóa đơn (PIN hoặc từ xa) | Quản lý ca |  | P1, A7 | 4, 6 | Chưa |  |
| UC25 | Giải phóng ghế nội bộ | Quản lý ca |  | A7 | 6 | Chưa |  |
| UC26 | Duyệt phiếu hủy hàng | Quản lý ca |  | A7 | 6 | Chưa |  |
| UC27 | Hủy suất do sự cố | Quản lý ca |  | A5 | 6 | Chưa |  |
| UC28 | Quản lý loại đồ uống | Quản lý rạp |  | A2, A3 | 2 | Xong | src/FPolyCinema.Web/Services/CatalogServices.cs#public class CategoryService; src/FPolyCinema.Web/Controllers/CategoriesController.cs#public class CategoriesController; test:Category_DeleteBlockedWhenHasDrinks_AllowedWhenEmpty; ui:A2 |
| UC29 | Quản lý đồ uống và định mức | Quản lý rạp |  | A2, A3 | 2 | Xong | src/FPolyCinema.Web/Services/CatalogServices.cs#public class DrinkService; src/FPolyCinema.Web/Controllers/DrinksController.cs#public class DrinksController; test:Drink_SaveWithRecipesPerCinema_DeleteOnlyWhenNeverSold; ui:A3 |
| UC30 | Quản lý nguyên liệu, nhập kho | Quản lý rạp |  | A2, A3 | 6 | Chưa |  |
| UC31 | Quản lý phim, nhà phát hành, KDM, chia doanh thu | Quản lý rạp |  | A2, A3 | 3 | Xong | src/FPolyCinema.Web/Controllers/MoviesController.cs#public class MoviesController; src/FPolyCinema.Web/Controllers/MoviesController.cs#public class DistributorsController; src/FPolyCinema.Web/Services/MovieService.cs#KdmLicenseDAL.Insert; route:/admin/movies; test:Movie_SaveWithRentalRulesAndKdm_DeleteBlockedWhenHasShowtimes |
| UC32 | Quản lý phòng chiếu và sơ đồ ghế | Quản lý rạp |  | A4 | 3 | Xong | src/FPolyCinema.Web/Controllers/AuditoriumsController.cs#public class AuditoriumsController; src/FPolyCinema.Web/Services/AuditoriumService.cs#public bool SaveLayout; route:/admin/auditoriums; test:Auditorium_LayoutRescoresAndLocksStructureWhenFutureShowtimeSold |
| UC33 | Quản lý bảng giá, đối tượng ưu đãi, ngày lễ | Quản lý rạp |  | A2, A3 | 3 | Xong | src/FPolyCinema.Web/Controllers/PricingController.cs#public class PricingController; route:/admin/pricing; test:Pricing_SaveWritesAuditLog_DiscountBelowLegalMinimumRejected_TryPrice |
| UC34 | Quản lý suất chiếu | Quản lý rạp |  | A5 | 3 | Xong | src/FPolyCinema.Web/Controllers/SchedulerController.cs#public class SchedulerController; route:/scheduler; test:Schedule_SaveCreatesShowtimeSeats_BlocksOverlapAndLockedChanges |
| UC35 | Xếp lịch tự động bằng AI | Quản lý rạp | extend UC34 | A5 | 7 | Chưa |  |
| UC36 | Quản lý nhân viên | Quản lý rạp |  | A2, A3 | 2 | Xong | src/FPolyCinema.Web/Services/StaffService.cs#public class StaffService; src/FPolyCinema.Web/Controllers/UsersController.cs#public class UsersController; test:Staff_CreateEmailsPassword_SetActiveWritesAudit_DeleteRules; ui:A2 |
| UC37 | Quản lý hóa đơn | Quản lý rạp |  | A8 | 4 | Xong | src/FPolyCinema.Web/Controllers/BillsController.cs#Details; src/FPolyCinema.Web/Views/Bills/Index.cshtml#Xuất CSV; test:Bills_SearchFiltersByStatusChannelStaffAndCode |
| UC38 | Thống kê, báo cáo | Quản lý rạp |  | A1, A6 | 4 | Xong | src/FPolyCinema.Web/Controllers/StatisticsController.cs#Reports; src/FPolyCinema.Web/Views/Statistics/Dashboard.cshtml#Bảng điều khiển; test:Statistics_DashboardSharesOccupancyAndSeatsNeedingChange |
| UC39 | Đối soát nhà phát hành | Quản lý rạp |  | A6 | 4 | Xong | src/FPolyCinema.Web/Controllers/StatisticsController.cs#Settlement; src/FPolyCinema.Web/Views/Statistics/Settlement.cshtml#Phần rạp tự chịu do giá sàn; test:Statistics_SettlementCalculatorMatchesStoredProcedure |
| UC40 | Bán vé doanh nghiệp | Quản lý rạp |  | A2, A3 | 6 | Chưa |  |

## Tiến độ yêu cầu theo phase

| Phase | Nội dung | Đã xong | Tự động |
|---|---|---|---|
| 1 | Nền tảng dữ liệu | 92/92 | 81 |
| 2 | Giao diện nền, đăng nhập, CRUD cơ bản | 177/177 | 9 |
| 3 | Phim, phòng chiếu, giá, suất chiếu | 53/53 | 1 |
| 4 | POS, hóa đơn, thống kê | 142/142 | 2 |
| 5 | Web khách và đặt vé online | 0/86 | 3 |
| 6 | Vận hành thời gian thực | 0/95 | 7 |
| 7 | AI xếp lịch và trợ lý đặt vé | 0/53 | 1 |
| 8 | Hoàn thiện, demo, kiểm tra tổng thể | 0/15 | 0 |
