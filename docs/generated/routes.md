# Controller, route và quyền (sinh từ Controllers/*.cs)

9 controller, 58 route.

| Route | HTTP | Controller.Action | Quyền | File |
|---|---|---|---|---|
| / | GET | Home.Index | AllowAnonymous | src/FPolyCinema.Web/Controllers/HomeController.cs:23 |
| /admin/categories | GET | Categories.Index | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/CategoriesController.cs:31 |
| /admin/categories/{} | GET | Categories.Details | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/CategoriesController.cs:39 |
| /admin/categories/{}/delete | POST | Categories.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:103 |
| /admin/categories/{}/delete-check | GET | Categories.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:95 |
| /admin/categories/{}/edit | GET | Categories.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:60 |
| /admin/categories/{}/edit | POST | Categories.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:72 |
| /admin/categories/{}/set-active | POST | Categories.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:112 |
| /admin/categories/create | GET | Categories.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:52 |
| /admin/categories/create | POST | Categories.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/CategoriesController.cs:56 |
| /admin/drinks | GET | Drinks.Index | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/DrinksController.cs:36 |
| /admin/drinks/{} | GET | Drinks.Details | Policy Policies.EmployeeOrManager | src/FPolyCinema.Web/Controllers/DrinksController.cs:49 |
| /admin/drinks/{}/delete | POST | Drinks.Delete | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:136 |
| /admin/drinks/{}/delete-check | GET | Drinks.DeleteCheck | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:127 |
| /admin/drinks/{}/edit | GET | Drinks.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:77 |
| /admin/drinks/{}/edit | POST | Drinks.Edit | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:95 |
| /admin/drinks/{}/set-active | POST | Drinks.SetActive | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:144 |
| /admin/drinks/create | GET | Drinks.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:69 |
| /admin/drinks/create | POST | Drinks.Create | Policy Policies.ManagerOnly | src/FPolyCinema.Web/Controllers/DrinksController.cs:73 |
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
| /dev/layouts/display | GET | Dev.LayoutDisplay | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:142 |
| /dev/layouts/pos | GET | Dev.LayoutPos | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:138 |
| /dev/mailbox | GET | Dev.Mailbox | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:121 |
| /dev/mailbox/clear | POST | Dev.ClearMailbox | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:129 |
| /dev/style-guide | GET | Dev.StyleGuide | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:83 |
| /dev/style-guide/broken | GET | Dev.SampleBroken | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:118 |
| /dev/style-guide/fail | POST | Dev.SampleFail | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:107 |
| /dev/style-guide/ok | POST | Dev.SampleOk | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:104 |
| /dev/style-guide/slow | GET | Dev.SampleSlow | AllowAnonymous | src/FPolyCinema.Web/Controllers/ToolsControllers.cs:111 |
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

## Màn hình trong UI mục 7 và route tương ứng

| Màn hình | Phase | Đường dẫn trong spec | Có trong code |
|---|---|---|---|
| A1 | 4, 6 | /admin | chưa |
| A2 | 2 |  |  |
| A3 | 2, 3 |  |  |
| A4 | 3 |  |  |
| A5 | 3, 7 | /scheduler | chưa |
| A6 | 4 |  |  |
| A7 | 6 |  |  |
| A8 | 4 | /admin/bills, /admin/bills/{id} | chưa, chưa |
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
| P1 | 4, 6 | /pos/index | chưa |
| P2 | 6 | /pos/customer-display?terminal=POS-01 | chưa |
| P3 | 6 | /pos/gate-inspector | chưa |
| P4 | 6 | /pos/fnb-kds | chưa |
| P5 | 6 | /lobby/pickup-board | chưa |
| P6 | 6 | /lobby/digital-signage | chưa |
| P7 | 4 |  |  |
