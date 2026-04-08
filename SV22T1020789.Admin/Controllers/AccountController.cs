using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Models.Security;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SV22T1020789.Admin.Controllers
{
    /// <summary>
    /// Các chức năng liên quan đến tài khoản
    /// </summary>
    [Authorize]
    public class AccountController : Controller
    {
        #region ĐĂNG NHẬP - ĐĂNG XUẤT

        /// <summary>
        /// Giao diện Đăng nhập
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        /// <summary>
        /// Xử lí đăng nhập
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string username, string password)
        {
            ViewBag.Username = username;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("Error", "Nhập đủ tên và mật khẩu");
                return View();
            }

            password = CryptHelper.HashMD5(password);

            var userAccount = await SecurityDataService.EmployeeAuthorizeAsync(username, password);

            if (userAccount == null)
            {
                ModelState.AddModelError("Error", "Tên đăng nhập hoặc mật khẩu không đúng!");
                return View();
            }

            int empId = Convert.ToInt32(userAccount.UserId);
            var employee = await HRDataService.GetEmployeeAsync(empId);

            if (employee != null && employee.IsWorking == false)
            {
                ModelState.AddModelError("Error", "Truy cập bị từ chối: Tài khoản đã bị vô hiệu hóa do Nghỉ việc!");
                return View();
            }

            var userData = new WebUserData()
            {
                UserId = userAccount.UserId,
                UserName = userAccount.UserName,
                DisplayName = userAccount.DisplayName,
                Email = userAccount.Email,
                Photo = userAccount.Photo,
                Roles = userAccount.RoleNames.Split(',').ToList()
            };

            await HttpContext.SignInAsync
            (
                CookieAuthenticationDefaults.AuthenticationScheme,
                userData.CreatePrincipal()
            );

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Đăng xuất
        /// </summary>
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }

        #endregion

        #region ĐỔI MẬT KHẨU

        /// <summary>
        /// Giao diện đổi mật khẩu
        /// </summary>
        [HttpGet]
        public IActionResult ChangePassword()
        {
            ViewBag.IsSuccess = false; // Mặc định chưa thành công
            return View();
        }

        /// <summary>
        /// Xử lý dữ liệu khi bấm nút Đổi mật khẩu
        /// </summary>
        [HttpPost]
        public IActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            // 1. Kiểm tra dữ liệu rỗng
            if (string.IsNullOrWhiteSpace(oldPassword) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError("Error", "Vui lòng nhập đầy đủ thông tin!");
                return View();
            }

            // 2. Kiểm tra mật khẩu mới và xác nhận có khớp không
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("Error", "Mật khẩu xác nhận không khớp với mật khẩu mới!");
                return View();
            }

            // 3. Lấy tên đăng nhập (Email) của người dùng đang đăng nhập
            string userName = User.GetUserData()?.UserName ?? "";

            // 4. Mã hóa mật khẩu cũ và mới
            string hashedOldPassword = CryptHelper.HashMD5(oldPassword);
            string hashedNewPassword = CryptHelper.HashMD5(newPassword);

            // 5. Gọi Service thực hiện đổi
            bool result = SecurityDataService.ChangePassword(userName, hashedOldPassword, hashedNewPassword);

            // 6. Xử lý kết quả trả về
            if (!result)
            {
                ModelState.AddModelError("Error", "Mật khẩu cũ không chính xác!");
                return View();
            }

            ViewBag.IsSuccess = true;
            return View();
        }

        #endregion

        /// <summary>
        /// Truy cập bị từ chối
        /// </summary>
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}