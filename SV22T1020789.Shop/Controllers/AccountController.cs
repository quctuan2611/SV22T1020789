using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Models.Partner;
using Newtonsoft.Json;
using SV22T1020789.Shop.Models;
using SV22T1020789.Shop;

namespace SV22T1020789.Shop.Controllers
{
    /// <summary>
    /// Các chức năng liên quan đến quản lý tài khoản của khách hàng (Đăng nhập, đăng ký, hồ sơ, đổi mật khẩu...)
    /// </summary>
    [Authorize]
    public class AccountController : Controller
    {
        private const string SHOPPING_CART = "ShoppingCart";

        /// <summary>
        /// Hàm hỗ trợ: Tạo tên Cookie lưu trữ giỏ hàng riêng biệt cho từng tài khoản dựa trên Email
        /// </summary>
        /// <param name="email">Email của khách hàng</param>
        /// <returns>Chuỗi tên Cookie (ví dụ: SavedCart_nguyenvana_gmail_com)</returns>
        private string GetCartCookieName(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            return $"SavedCart_{email.Replace("@", "_").Replace(".", "_")}";
        }

        /// <summary>
        /// Giao diện trang Đăng nhập
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
            ViewBag.SavedEmail = Request.Cookies["SavedEmail"] ?? "";
            return View();
        }

        /// <summary>
        /// Xử lý quá trình đăng nhập của khách hàng
        /// Nếu thành công: Cấp quyền (Claims), xử lý gộp giỏ hàng vãng lai vào giỏ hàng tài khoản, lưu Cookie nếu có Remember Me
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ Email và Mật khẩu.");
                return View();
            }

            var userAccount = await SecurityDataService.CustomerAuthorizeAsync(email, CryptHelper.HashMD5(password));
            if (userAccount != null)
            {
                
                if (userAccount.IsLocked)
                {
                    TempData["LockedMessage"] = "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên để biết thêm chi tiết.";
                    return View();
                }
                // -----------------------------------------------------------

                var guestCart = HttpContext.Session.Get<List<CartItem>>(SHOPPING_CART) ?? new List<CartItem>();
                string cookieName = GetCartCookieName(userAccount.Email);
                List<CartItem> userCart = new List<CartItem>();

                if (Request.Cookies.TryGetValue(cookieName, out string? cartJson) && !string.IsNullOrEmpty(cartJson))
                {
                    userCart = JsonConvert.DeserializeObject<List<CartItem>>(cartJson) ?? new List<CartItem>();
                }

                foreach (var item in guestCart)
                {
                    var existingItem = userCart.FirstOrDefault(x => x.ProductID == item.ProductID);
                    if (existingItem != null) existingItem.Quantity += item.Quantity;
                    else userCart.Add(item);
                }

                var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, userAccount.DisplayName),
                    new Claim(ClaimTypes.Email, userAccount.Email),
                    new Claim("UserId", userAccount.UserId),
                    new Claim(ClaimTypes.Role, "Customer"),
                    new Claim("Photo", userAccount.Photo ?? "default-user.png")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = rememberMe });

                HttpContext.Session.Set(SHOPPING_CART, userCart);
                Response.Cookies.Append(cookieName, JsonConvert.SerializeObject(userCart), new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(30),
                    HttpOnly = true,
                    Path = "/"
                });

                if (rememberMe) Response.Cookies.Append("SavedEmail", email, new CookieOptions { Expires = DateTimeOffset.Now.AddDays(30) });
                else Response.Cookies.Delete("SavedEmail");

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác!");
            return View();
        }

        /// <summary>
        /// Xử lý Đăng xuất, xóa toàn bộ Session và chứng chỉ xác thực
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Giao diện xem hồ sơ cá nhân của khách hàng đang đăng nhập
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            string? userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int customerId))
                return RedirectToAction("Login");

            var customer = await PartnerDataService.GetCustomerAsync(customerId);
            if (customer == null) return RedirectToAction("Logout");

            ViewBag.Provinces = await DictionaryDataService.ListProvincesAsync();
            return View(customer);
        }

        /// <summary>
        /// Xử lý cập nhật hồ sơ cá nhân và làm mới lại thông tin hiển thị (Identity Claims)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(Customer data)
        {
            // 1. Lấy thông tin từ Claim để bảo mật và tránh mất dữ liệu do 'disabled' input
            string? userIdStr = User.FindFirstValue("UserId");
            string? userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int customerId))
                return RedirectToAction("Login");

            // 2. Gán ID và Email (vì form không gửi về hoặc disabled)
            data.CustomerID = customerId;
            data.Email = userEmail ?? "";

            // Nếu khách không nhập Tên giao dịch, mình tự động lấy Họ tên gán vào
            if (string.IsNullOrWhiteSpace(data.ContactName))
            {
                data.ContactName = data.CustomerName;
            }

            // 3. Kiểm tra các trường bắt buộc (Không còn kiểm tra ContactName nữa)
            if (string.IsNullOrWhiteSpace(data.CustomerName) || string.IsNullOrWhiteSpace(data.Province))
            {
                TempData["ErrorMessage"] = "Họ tên và Tỉnh/Thành không được để trống!";
                return RedirectToAction("Profile");
            }

            // 4. Cập nhật Database
            if (await PartnerDataService.UpdateCustomerAsync(data))
            {
                // 5. Cập nhật lại Identity để đổi tên hiển thị trên Header ngay lập tức
                var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, data.CustomerName),
                    new Claim(ClaimTypes.Email, data.Email),
                    new Claim("UserId", data.CustomerID.ToString()),
                    new Claim(ClaimTypes.Role, "Customer"),
                    new Claim("Photo", User.FindFirstValue("Photo") ?? "default-user.png")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = true });

                TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra trong quá trình lưu dữ liệu!";
            }

            return RedirectToAction("Profile");
        }

        /// <summary>
        /// Giao diện đổi mật khẩu tài khoản
        /// </summary>
        [HttpGet]
        public IActionResult ChangePassword() => View();

        /// <summary>
        /// Xử lý thay đổi mật khẩu (yêu cầu mật khẩu cũ phải chính xác)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            string? email = User.FindFirstValue(ClaimTypes.Email);
            if (newPassword != confirmPassword)
            {
                TempData["ErrorPassword"] = "Mật khẩu xác nhận không khớp!";
                return View();
            }

            var authorized = await SecurityDataService.CustomerAuthorizeAsync(email!, CryptHelper.HashMD5(oldPassword));
            if (authorized == null)
            {
                TempData["ErrorPassword"] = "Mật khẩu cũ không chính xác!";
                return View();
            }

            if (await SecurityDataService.ResetCustomerPasswordAsync(email!, CryptHelper.HashMD5(newPassword)))
                TempData["SuccessPassword"] = "Đổi mật khẩu thành công!";

            return View();
        }

        /// <summary>
        /// Giao diện đăng ký tài khoản mới cho khách hàng vãng lai
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            ViewBag.Provinces = await DictionaryDataService.ListProvincesAsync();
            return View();
        }

        /// <summary>
        /// Xử lý đăng ký tài khoản mới, kiểm tra trùng lặp Email và mã hóa mật khẩu trước khi lưu
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Register(Customer data, string confirmPassword)
        {
            // Kiểm tra mật khẩu xác nhận có khớp không
            if (data.Password != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Mật khẩu xác nhận không khớp!");
            }

            if (!await PartnerDataService.ValidateCustomerEmailAsync(data.Email))
                ModelState.AddModelError("Email", "Email này đã được sử dụng!");

            if (string.IsNullOrWhiteSpace(data.ContactName))
            {
                data.ContactName = data.CustomerName;
            }

            // Nếu có bất kỳ lỗi nào (sai mật khẩu, trùng email, thiếu data...) thì dừng lại và báo lỗi
            if (!ModelState.IsValid)
            {
                ViewBag.Provinces = await DictionaryDataService.ListProvincesAsync();
                return View(data);
            }

            // 1. Lưu tạm mật khẩu chưa mã hóa để lát nữa tự động đăng nhập
            string rawPassword = data.Password;

            // 2. Mã hóa mật khẩu để lưu an toàn vào DB
            data.Password = CryptHelper.HashMD5(data.Password);

            if (await PartnerDataService.AddCustomerAsync(data) > 0)
            {
                return await Login(data.Email, rawPassword, false);
            }

            return View(data);
        }
    }
}