# Controller, route và quyền (sinh từ Controllers/*.cs)

17 controller, 133 route.

| Route | HTTP | Controller.Action | Quyền | File |
|---|---|---|---|---|
| / | GET | Home.Index | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:23 |
| /admin | GET | Statistics.Dashboard | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:37 |
| /admin/auditoriums | GET | Auditoriums.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/AuditoriumsController.cs:36 |
| /admin/auditoriums/{}/design | GET | Auditoriums.Design | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/AuditoriumsController.cs:107 |
| /admin/auditoriums/{}/edit | GET | Auditoriums.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/AuditoriumsController.cs:67 |
| /admin/auditoriums/{}/edit | POST | Auditoriums.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/AuditoriumsController.cs:78 |
| /admin/auditoriums/create | GET | Auditoriums.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/AuditoriumsController.cs:45 |
| /admin/auditoriums/create | POST | Auditoriums.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/AuditoriumsController.cs:48 |
| /admin/bills | GET | Bills.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/BillsController.cs:56 |
| /admin/bills/{} | GET | Bills.Details | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/BillsController.cs:80 |
| /admin/bills/{}/cancel | POST | Bills.Cancel | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/BillsController.cs:105 |
| /admin/bills/{}/print | GET | Bills.Print | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/BillsController.cs:89 |
| /admin/bills/export | GET | Bills.Export | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/BillsController.cs:64 |
| /admin/categories | GET | Categories.Index | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/CategoriesController.cs:31 |
| /admin/categories/{} | GET | Categories.Details | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/CategoriesController.cs:39 |
| /admin/categories/{}/delete | POST | Categories.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:103 |
| /admin/categories/{}/delete-check | GET | Categories.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:95 |
| /admin/categories/{}/edit | GET | Categories.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:60 |
| /admin/categories/{}/edit | POST | Categories.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:72 |
| /admin/categories/{}/set-active | POST | Categories.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:112 |
| /admin/categories/create | GET | Categories.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:52 |
| /admin/categories/create | POST | Categories.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:56 |
| /admin/distributors | GET | Distributors.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:168 |
| /admin/distributors/{}/delete | POST | Distributors.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:220 |
| /admin/distributors/{}/delete-check | GET | Distributors.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:213 |
| /admin/distributors/{}/edit | GET | Distributors.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:181 |
| /admin/distributors/{}/edit | POST | Distributors.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:192 |
| /admin/distributors/{}/set-active | POST | Distributors.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:227 |
| /admin/distributors/create | GET | Distributors.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:175 |
| /admin/distributors/create | POST | Distributors.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:178 |
| /admin/drinks | GET | Drinks.Index | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/DrinksController.cs:36 |
| /admin/drinks/{} | GET | Drinks.Details | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/DrinksController.cs:49 |
| /admin/drinks/{}/delete | POST | Drinks.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:136 |
| /admin/drinks/{}/delete-check | GET | Drinks.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:127 |
| /admin/drinks/{}/edit | GET | Drinks.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:77 |
| /admin/drinks/{}/edit | POST | Drinks.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:95 |
| /admin/drinks/{}/set-active | POST | Drinks.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:144 |
| /admin/drinks/create | GET | Drinks.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:69 |
| /admin/drinks/create | POST | Drinks.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:73 |
| /admin/movies | GET | Movies.Index | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/MoviesController.cs:34 |
| /admin/movies/{} | GET | Movies.Details | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/MoviesController.cs:45 |
| /admin/movies/{}/delete | POST | Movies.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:143 |
| /admin/movies/{}/delete-check | GET | Movies.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:134 |
| /admin/movies/{}/edit | GET | Movies.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:75 |
| /admin/movies/{}/edit | POST | Movies.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:96 |
| /admin/movies/{}/set-active | POST | Movies.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:151 |
| /admin/movies/create | GET | Movies.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:62 |
| /admin/movies/create | POST | Movies.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:71 |
| /admin/pricing | GET | Pricing.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:35 |
| /admin/pricing | POST | Pricing.Save | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:43 |
| /admin/pricing/audiences | GET | Pricing.Audiences | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:72 |
| /admin/pricing/audiences | POST | Pricing.Audiences | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:76 |
| /admin/pricing/holidays | GET | Pricing.Holidays | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:92 |
| /admin/pricing/holidays | POST | Pricing.AddHoliday | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:105 |
| /admin/pricing/holidays/{}/delete | POST | Pricing.DeleteHoliday | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:125 |
| /admin/pricing/try | GET | Pricing.Try | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/PricingController.cs:135 |
| /admin/reports | GET | Statistics.Reports | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:54 |
| /admin/reports/export | GET | Statistics.Export | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:107 |
| /admin/reports/settlement | GET | Statistics.Settlement | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:148 |
| /admin/reports/settlement/export | GET | Statistics.SettlementExport | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:167 |
| /admin/users | GET | Users.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:29 |
| /admin/users/{} | GET | Users.Details | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:42 |
| /admin/users/{}/delete | POST | Users.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:120 |
| /admin/users/{}/delete-check | GET | Users.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:113 |
| /admin/users/{}/edit | GET | Users.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:65 |
| /admin/users/{}/edit | POST | Users.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:76 |
| /admin/users/{}/set-active | POST | Users.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:128 |
| /admin/users/create | GET | Users.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:59 |
| /admin/users/create | POST | Users.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:62 |
| /cinema/select | POST | Cinema.Select | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:22 |
| /demo/switch-role | GET | Demo.SwitchRole | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:45 |
| /dev/layouts/display | GET | Dev.LayoutDisplay | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:145 |
| /dev/layouts/pos | GET | Dev.LayoutPos | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:141 |
| /dev/mailbox | GET | Dev.Mailbox | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:124 |
| /dev/mailbox/clear | POST | Dev.ClearMailbox | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:132 |
| /dev/style-guide | GET | Dev.StyleGuide | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:86 |
| /dev/style-guide/broken | GET | Dev.SampleBroken | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:121 |
| /dev/style-guide/fail | POST | Dev.SampleFail | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:110 |
| /dev/style-guide/ok | POST | Dev.SampleOk | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:107 |
| /dev/style-guide/slow | GET | Dev.SampleSlow | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:114 |
| /file/delete | POST | File.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/FileController.cs:40 |
| /file/upload | POST | File.Upload | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/FileController.cs:24 |
| /home/accessdenied | GET | Home.AccessDenied | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:27 |
| /home/error | GET | Home.Error | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:46 |
| /login/changepassword | GET | Login.ChangePassword | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:141 |
| /login/changepassword | POST | Login.ChangePassword | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:150 |
| /login/forgotpassword | GET | Login.ForgotPassword | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:118 |
| /login/forgotpassword | POST | Login.ForgotPassword | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:123 |
| /login/login | GET | Login.Login | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:50 |
| /login/login | POST | Login.Login | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:58 |
| /login/logout | POST | Login.Logout | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:78 |
| /login/profile | GET | Login.Profile | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:180 |
| /login/profile | POST | Login.Profile | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:199 |
| /login/register | GET | Login.Register | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:88 |
| /login/register | POST | Login.Register | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:97 |
| /login/verifyotp | POST | Login.VerifyOtp | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:132 |
| /me | GET | Login.Me | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/LoginController.cs:191 |
| /policies/cancellation | GET | Home.Cancellation | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:91 |
| /policies/terms | GET | Home.Terms | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:88 |
| /pos | GET,GET | Pos.Index | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:66 |
| /pos/apply-points | POST | Pos.ApplyPoints | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:260 |
| /pos/apply-voucher | POST | Pos.ApplyVoucher | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:256 |
| /pos/cancel | POST | Pos.Cancel | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:278 |
| /pos/cart | GET | Pos.CartState | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:180 |
| /pos/checkout | POST | Pos.Checkout | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:274 |
| /pos/close-shift | GET | Pos.CloseShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:137 |
| /pos/close-shift | POST | Pos.CloseShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:147 |
| /pos/drink-stock | GET | Pos.DrinkStock | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:243 |
| /pos/extend-hold | POST | Pos.ExtendHold | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:227 |
| /pos/hold-seats | POST | Pos.HoldSeats | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:214 |
| /pos/index | GET,GET | Pos.Index | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:66 |
| /pos/init | POST | Pos.Init | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:171 |
| /pos/lookup-member | POST | Pos.LookupMember | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:252 |
| /pos/my-bills | GET | Pos.MyBills | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:351 |
| /pos/open-shift | GET | Pos.OpenShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:95 |
| /pos/open-shift | POST | Pos.OpenShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:120 |
| /pos/payment-qr | GET | Pos.PaymentQr | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:264 |
| /pos/print | GET | Pos.Print | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:335 |
| /pos/request-void | POST | Pos.RequestVoid | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:303 |
| /pos/scan | POST | Pos.Scan | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:322 |
| /pos/seat-map | GET | Pos.SeatMap | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:202 |
| /pos/select-showtime | POST | Pos.SelectShowtime | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:186 |
| /pos/set-audience | POST | Pos.SetAudience | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:231 |
| /pos/set-guardian | POST | Pos.SetGuardian | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:235 |
| /pos/shift-report/{} | GET | Pos.ShiftReport | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:158 |
| /pos/update-quantity | POST | Pos.UpdateQuantity | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:239 |
| /scheduler | GET | Scheduler.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:43 |
| /scheduler/{}/delete | POST | Scheduler.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:126 |
| /scheduler/{}/delete-check | GET | Scheduler.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:118 |
| /scheduler/{}/edit | GET | Scheduler.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:61 |
| /scheduler/check | POST | Scheduler.Check | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:102 |
| /scheduler/create | GET | Scheduler.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:51 |
| /scheduler/save | POST | Scheduler.Save | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:107 |

## Màn hình trong UI mục 7 và route tương ứng

| Màn hình | Phase | Đường dẫn trong spec | Có trong code |
|---|---|---|---|
| A1 | 4, 6 | /admin | có |
| A2 | 2 |  |  |
| A3 | 2, 3 |  |  |
| A4 | 3 |  |  |
| A5 | 3, 7 | /scheduler | có |
| A6 | 4 |  |  |
| A7 | 6 |  |  |
| A8 | 4 | /admin/bills, /admin/bills/{id} | có, có |
| A9 | 2 | /me | có |
| ART | 1 |  |  |
| C1 | 5, 7 | / | có |
| C2 | 5 | /movies/{id} | chưa |
| C3 | 5 | /booking/{showtimeId} | chưa |
| C4 | 5, 6 | /tickets/{billCode} | chưa |
| C5 | 5 | /account | chưa |
| C6 | 2 |  |  |
| C7 | 7 |  |  |
| D1 | 2 | /dev/style-guide | có |
| D2 | 2 | /dev/mailbox | có |
| D3 | 2 | /Home/AccessDenied, /Home/Error | có, có |
| D4 | 8 | /demo/multi-screen | chưa |
| P1 | 4, 6 | /pos/index | có |
| P2 | 6 | /pos/customer-display?terminal=POS-01 | chưa |
| P3 | 6 | /pos/gate-inspector | chưa |
| P4 | 6 | /pos/fnb-kds | chưa |
| P5 | 6 | /lobby/pickup-board | chưa |
| P6 | 6 | /lobby/digital-signage | chưa |
| P7 | 4 |  |  |
