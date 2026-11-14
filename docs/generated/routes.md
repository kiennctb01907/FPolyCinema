# Controller, route và quyền (sinh từ Controllers/*.cs)

28 controller, 209 route.

| Route | HTTP | Controller.Action | Quyền | File |
|---|---|---|---|---|
| / | GET | Home.Index | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:30 |
| /account | GET | Account.Index | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/AccountController.cs:37 |
| /account/notifications/read | POST | Account.ReadAll | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/AccountController.cs:95 |
| /account/profile | POST | Account.Profile | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/AccountController.cs:46 |
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
| /admin/corporate | GET | Corporate.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CorporateController.cs:33 |
| /admin/corporate/{} | GET | Corporate.Details | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CorporateController.cs:68 |
| /admin/corporate/{}/vouchers.csv | GET | Corporate.Export | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CorporateController.cs:77 |
| /admin/corporate/create | GET | Corporate.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CorporateController.cs:43 |
| /admin/corporate/create | POST | Corporate.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CorporateController.cs:50 |
| /admin/distributors | GET | Distributors.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:168 |
| /admin/distributors/{}/delete | POST | Distributors.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:220 |
| /admin/distributors/{}/delete-check | GET | Distributors.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:213 |
| /admin/distributors/{}/edit | GET | Distributors.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:181 |
| /admin/distributors/{}/edit | POST | Distributors.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:192 |
| /admin/distributors/{}/set-active | POST | Distributors.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:227 |
| /admin/distributors/create | GET | Distributors.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:175 |
| /admin/distributors/create | POST | Distributors.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MoviesController.cs:178 |
| /admin/drinks | GET | Drinks.Index | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/DrinksController.cs:39 |
| /admin/drinks/{} | GET | Drinks.Details | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/DrinksController.cs:52 |
| /admin/drinks/{}/delete | POST | Drinks.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:146 |
| /admin/drinks/{}/delete-check | GET | Drinks.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:137 |
| /admin/drinks/{}/edit | GET | Drinks.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:80 |
| /admin/drinks/{}/edit | POST | Drinks.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:98 |
| /admin/drinks/{}/set-active | POST | Drinks.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:154 |
| /admin/drinks/create | GET | Drinks.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:72 |
| /admin/drinks/create | POST | Drinks.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:76 |
| /admin/materials | GET | Materials.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:35 |
| /admin/materials/{}/edit | GET | Materials.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:56 |
| /admin/materials/{}/edit | POST | Materials.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:67 |
| /admin/materials/{}/set-active | POST | Materials.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:98 |
| /admin/materials/create | GET | Materials.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:53 |
| /admin/materials/create | POST | Materials.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:64 |
| /admin/materials/import | GET | Materials.Import | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:108 |
| /admin/materials/import | POST | Materials.Import | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/MaterialsController.cs:122 |
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
| /admin/reports/export | GET | Statistics.Export | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:110 |
| /admin/reports/settlement | GET | Statistics.Settlement | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:156 |
| /admin/reports/settlement/export | GET | Statistics.SettlementExport | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/StatisticsController.cs:175 |
| /admin/users | GET | Users.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:29 |
| /admin/users/{} | GET | Users.Details | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:42 |
| /admin/users/{}/delete | POST | Users.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:120 |
| /admin/users/{}/delete-check | GET | Users.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:113 |
| /admin/users/{}/edit | GET | Users.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:65 |
| /admin/users/{}/edit | POST | Users.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:76 |
| /admin/users/{}/set-active | POST | Users.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:128 |
| /admin/users/create | GET | Users.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:59 |
| /admin/users/create | POST | Users.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/UsersController.cs:62 |
| /booking/{} | GET | Booking.Index | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:101 |
| /booking/{}/cancel | POST | Booking.Cancel | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:190 |
| /booking/{}/cart | GET | Booking.Cart | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:158 |
| /booking/{}/checkout | POST | Booking.Checkout | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:186 |
| /booking/{}/confirm | POST | Booking.Confirm | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:181 |
| /booking/{}/hold | POST | Booking.Hold | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:162 |
| /booking/{}/points | POST | Booking.Points | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:177 |
| /booking/{}/quantity | POST | Booking.Quantity | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:169 |
| /booking/{}/seat-map | GET | Booking.SeatMap | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:145 |
| /booking/{}/suggest | GET | Booking.Suggest | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:136 |
| /booking/{}/voucher | POST | Booking.Voucher | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:173 |
| /booking/pay/{} | GET | Booking.Pay | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:198 |
| /booking/pay/{}/status | GET | Booking.PayStatus | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:213 |
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
| /home/accessdenied | GET | Home.AccessDenied | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:34 |
| /home/error | GET | Home.Error | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:53 |
| /lobby/digital-signage | GET | Lobby.DigitalSignage | AllowAnonymous | src/FPolyCinema.Web/Controllers/LobbyController.cs:56 |
| /lobby/digital-signage/rows | GET | Lobby.SignageRows | AllowAnonymous | src/FPolyCinema.Web/Controllers/LobbyController.cs:69 |
| /lobby/pickup-board | GET | Lobby.PickupBoard | AllowAnonymous | src/FPolyCinema.Web/Controllers/LobbyController.cs:38 |
| /lobby/pickup-board/data | GET | Lobby.PickupData | AllowAnonymous | src/FPolyCinema.Web/Controllers/LobbyController.cs:45 |
| /login/changepassword | GET | Login.ChangePassword | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:142 |
| /login/changepassword | POST | Login.ChangePassword | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:151 |
| /login/forgotpassword | GET | Login.ForgotPassword | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:119 |
| /login/forgotpassword | POST | Login.ForgotPassword | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:124 |
| /login/login | GET | Login.Login | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:51 |
| /login/login | POST | Login.Login | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:59 |
| /login/logout | POST | Login.Logout | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:79 |
| /login/profile | GET | Login.Profile | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:181 |
| /login/profile | POST | Login.Profile | Authorize | src/FPolyCinema.Web/Controllers/LoginController.cs:201 |
| /login/register | GET | Login.Register | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:89 |
| /login/register | POST | Login.Register | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:98 |
| /login/verifyotp | POST | Login.VerifyOtp | AllowAnonymous | src/FPolyCinema.Web/Controllers/LoginController.cs:133 |
| /me | GET | Login.Me | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/LoginController.cs:193 |
| /menu | GET | Customer.Menu | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:62 |
| /movies | GET | Customer.Movies | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:29 |
| /movies/{} | GET | Customer.Movie | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:42 |
| /ops | GET | Ops.Index | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:49 |
| /ops/approvals/{}/approve | POST | Ops.Approve | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:61 |
| /ops/approvals/{}/reject | POST | Ops.Reject | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:65 |
| /ops/approvals/pending | GET | Ops.Pending | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:69 |
| /ops/house-seats/{}/release | POST | Ops.ReleaseHouseSeats | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:73 |
| /ops/incident | GET | Ops.Incident | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:122 |
| /ops/incident | POST | Ops.Incident | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:125 |
| /ops/live | GET | Ops.Live | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:57 |
| /ops/wastage | GET | Ops.Wastage | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:82 |
| /ops/wastage/{} | GET | Ops.WastageDetail | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:89 |
| /ops/wastage/{}/approve | POST | Ops.ApproveWastage | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:98 |
| /ops/wastage/{}/reject | POST | Ops.RejectWastage | Policy Policies.SupervisorOrManager | src/FPolyCinema.Web/Controllers/OpsController.cs:101 |
| /payment-sim/pay | GET | Payment.Sim | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:233 |
| /payments/webhook | POST | Payment.Webhook | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:252 |
| /policies/cancellation | GET | Home.Cancellation | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:98 |
| /policies/terms | GET | Home.Terms | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:95 |
| /pos | GET,GET | Pos.Index | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:84 |
| /pos/apply-points | POST | Pos.ApplyPoints | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:281 |
| /pos/apply-voucher | POST | Pos.ApplyVoucher | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:277 |
| /pos/cancel | POST | Pos.Cancel | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:310 |
| /pos/cart | GET | Pos.CartState | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:199 |
| /pos/checkout | POST | Pos.Checkout | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:298 |
| /pos/close-shift | GET | Pos.CloseShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:156 |
| /pos/close-shift | POST | Pos.CloseShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:166 |
| /pos/customer-display | GET | Lobby.CustomerDisplay | AllowAnonymous | src/FPolyCinema.Web/Controllers/LobbyController.cs:75 |
| /pos/customer-display/seat-map | GET | Lobby.CustomerDisplaySeatMap | AllowAnonymous | src/FPolyCinema.Web/Controllers/LobbyController.cs:94 |
| /pos/customer-display/state | GET | Lobby.CustomerDisplayState | AllowAnonymous | src/FPolyCinema.Web/Controllers/LobbyController.cs:90 |
| /pos/drink-stock | GET | Pos.DrinkStock | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:264 |
| /pos/extend-hold | POST | Pos.ExtendHold | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:248 |
| /pos/fnb-kds | GET | Kitchen.Kds | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/KitchenController.cs:41 |
| /pos/fnb-kds/{}/ready | POST | Kitchen.Ready | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/KitchenController.cs:49 |
| /pos/fnb-kds/{}/served | POST | Kitchen.Served | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/KitchenController.cs:53 |
| /pos/fnb-kds/orders | GET | Kitchen.Orders | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/KitchenController.cs:45 |
| /pos/fnb-kds/pickup | POST | Kitchen.Pickup | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/KitchenController.cs:57 |
| /pos/gate-inspector | GET | Gate.Index | Policy Policies.GateStaff | src/FPolyCinema.Web/Controllers/GateController.cs:33 |
| /pos/gate-inspector/admit | POST | Gate.Admit | Policy Policies.GateStaff | src/FPolyCinema.Web/Controllers/GateController.cs:50 |
| /pos/gate-inspector/cleaned | POST | Gate.Cleaned | Policy Policies.GateStaff | src/FPolyCinema.Web/Controllers/GateController.cs:55 |
| /pos/gate-inspector/cleaning | GET | Gate.Cleaning | Policy Policies.GateStaff | src/FPolyCinema.Web/Controllers/GateController.cs:68 |
| /pos/gate-inspector/scan | POST | Gate.Scan | Policy Policies.GateStaff | src/FPolyCinema.Web/Controllers/GateController.cs:46 |
| /pos/hold-seats | POST | Pos.HoldSeats | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:235 |
| /pos/index | GET,GET | Pos.Index | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:84 |
| /pos/init | POST | Pos.Init | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:190 |
| /pos/lookup-member | POST | Pos.LookupMember | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:273 |
| /pos/my-bills | GET | Pos.MyBills | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:384 |
| /pos/open-shift | GET | Pos.OpenShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:114 |
| /pos/open-shift | POST | Pos.OpenShift | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:139 |
| /pos/payment-qr | GET | Pos.PaymentQr | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:285 |
| /pos/print | GET | Pos.Print | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:368 |
| /pos/request-void | POST | Pos.RequestVoid | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:336 |
| /pos/request-void-remote | POST | Pos.RequestVoidRemote | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:403 |
| /pos/scan | POST | Pos.Scan | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:355 |
| /pos/seat-map | GET | Pos.SeatMap | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:223 |
| /pos/select-showtime | POST | Pos.SelectShowtime | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:205 |
| /pos/set-audience | POST | Pos.SetAudience | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:252 |
| /pos/set-guardian | POST | Pos.SetGuardian | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:256 |
| /pos/shift-report/{} | GET | Pos.ShiftReport | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:177 |
| /pos/swap-seat | GET | Pos.SwapSeat | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:421 |
| /pos/swap-seat | POST | Pos.SwapSeat | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:471 |
| /pos/swap-seat/find | POST | Pos.SwapFind | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:430 |
| /pos/swap-seat/quote | POST | Pos.SwapQuote | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:458 |
| /pos/swap-seat/seat-map | GET | Pos.SwapSeatMap | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:448 |
| /pos/update-quantity | POST | Pos.UpdateQuantity | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/PosController.cs:260 |
| /pos/wastage | GET | Kitchen.Wastage | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/KitchenController.cs:69 |
| /pos/wastage | POST | Kitchen.Wastage | Policy Policies.PosStaff | src/FPolyCinema.Web/Controllers/KitchenController.cs:78 |
| /quick-buy | GET | Customer.QuickBuy | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:66 |
| /scheduler | GET | Scheduler.Index | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:43 |
| /scheduler/{}/delete | POST | Scheduler.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:126 |
| /scheduler/{}/delete-check | GET | Scheduler.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:118 |
| /scheduler/{}/edit | GET | Scheduler.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:61 |
| /scheduler/check | POST | Scheduler.Check | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:102 |
| /scheduler/create | GET | Scheduler.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:51 |
| /scheduler/save | POST | Scheduler.Save | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/SchedulerController.cs:107 |
| /showtimes | GET | Customer.Showtimes | AllowAnonymous | src/FPolyCinema.Web/Controllers/CustomerController.cs:54 |
| /tickets/{} | GET | Tickets.Show | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:282 |
| /tickets/{}/cancel | POST | Tickets.Cancel | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:301 |
| /tickets/{}/im-here | POST | Tickets.ImHere | Policy Policies.CustomerOnly | src/FPolyCinema.Web/Controllers/CustomerController.cs:312 |

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
| C2 | 5 | /movies/{id} | có |
| C3 | 5 | /booking/{showtimeId} | có |
| C4 | 5, 6 | /tickets/{billCode} | có |
| C5 | 5 | /account | có |
| C6 | 2 |  |  |
| C7 | 7 |  |  |
| D1 | 2 | /dev/style-guide | có |
| D2 | 2 | /dev/mailbox | có |
| D3 | 2 | /Home/AccessDenied, /Home/Error | có, có |
| D4 | 8 | /demo/multi-screen | chưa |
| P1 | 4, 6 | /pos/index | có |
| P2 | 6 | /pos/customer-display?terminal=POS-01 | có |
| P3 | 6 | /pos/gate-inspector | có |
| P4 | 6 | /pos/fnb-kds | có |
| P5 | 6 | /lobby/pickup-board | có |
| P6 | 6 | /lobby/digital-signage | có |
| P7 | 4 |  |  |
