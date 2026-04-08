using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Shop.Models;

namespace SV22T1020789.Shop.Controllers
{
    /// <summary>
    /// Điều khiển các chức năng liên quan đến Giỏ hàng và Thanh toán.
    /// </summary>
    public class CartController : Controller
    {
        private const string SHOPPING_CART = "ShoppingCart";

        #region HÀM HỖ TRỢ (PRIVATE)

        /// <summary>
        /// Tạo tên Cookie dựa trên Email để đồng bộ giỏ hàng theo từng User.
        /// </summary>
        private string GetCartCookieName()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return "";
            return $"SavedCart_{email.Replace("@", "_").Replace(".", "_")}";
        }

        /// <summary>
        /// Lấy danh sách sản phẩm trong giỏ hàng từ Session hoặc khôi phục từ Cookie.
        /// </summary>
        private List<CartItem> GetCart()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>(SHOPPING_CART);

            // Nếu Session mất nhưng User đã đăng nhập, hồi phục từ Cookie
            if (cart == null && User.Identity?.IsAuthenticated == true)
            {
                string cookieName = GetCartCookieName();
                if (!string.IsNullOrEmpty(cookieName) && Request.Cookies.TryGetValue(cookieName, out string? cartJson))
                {
                    if (!string.IsNullOrEmpty(cartJson) && cartJson != "[]")
                    {
                        cart = JsonConvert.DeserializeObject<List<CartItem>>(cartJson);
                        if (cart != null) HttpContext.Session.Set(SHOPPING_CART, cart);
                    }
                }
            }
            return cart ?? new List<CartItem>();
        }

        /// <summary>
        /// Lưu giỏ hàng vào cả Session và Cookie (thời hạn 30 ngày).
        /// </summary>
        private void SaveCartSession(List<CartItem> cart)
        {
            string cookieName = GetCartCookieName();

            if (cart == null || cart.Count == 0)
            {
                HttpContext.Session.Remove(SHOPPING_CART);
                if (!string.IsNullOrEmpty(cookieName))
                {
                    Response.Cookies.Delete(cookieName, new CookieOptions { Path = "/" });
                }
            }
            else
            {
                HttpContext.Session.Set(SHOPPING_CART, cart);
                if (!string.IsNullOrEmpty(cookieName))
                {
                    Response.Cookies.Append(cookieName, JsonConvert.SerializeObject(cart), new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(30),
                        HttpOnly = true,
                        IsEssential = true,
                        Path = "/"
                    });
                }
            }
        }

        /// <summary>
        /// Xóa sạch giỏ hàng (dùng khi đặt hàng xong hoặc nhấn Xóa tất cả).
        /// </summary>
        private void ClearCart()
        {
            SaveCartSession(new List<CartItem>());
        }

        #endregion

        #region QUẢN LÝ GIỎ HÀNG

        /// <summary>
        /// Trang danh sách giỏ hàng.
        /// </summary>
        public IActionResult Index() => View(GetCart());

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng (Dùng Ajax).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Add(int id, int quantity = 1)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductID == id);

            if (item != null)
            {
                item.Quantity += quantity;
            }
            else
            {
                var product = await CatalogDataService.GetProductAsync(id);
                if (product == null || !product.IsSelling)
                    return Json(new { success = false, message = "Sản phẩm không khả dụng!" });

                cart.Add(new CartItem
                {
                    ProductID = product.ProductID,
                    ProductName = product.ProductName,
                    Photo = product.Photo ?? "",
                    Price = product.Price,
                    Quantity = quantity
                });
            }

            SaveCartSession(cart);
            return Json(new { success = true, totalCount = cart.Sum(x => x.Quantity) });
        }

        /// <summary>
        /// Xóa một sản phẩm khỏi giỏ.
        /// </summary>
        public IActionResult Remove(int id)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductID == id);
            if (item != null)
            {
                cart.Remove(item);
                SaveCartSession(cart);
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng.
        /// </summary>
        public IActionResult ClearAll()
        {
            ClearCart();
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Cập nhật số lượng sản phẩm.
        /// </summary>
        [HttpPost]
        public IActionResult Update(int id, int quantity)
        {
            if (quantity <= 0) return RedirectToAction("Remove", new { id = id });
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductID == id);
            if (item != null)
            {
                item.Quantity = quantity;
                SaveCartSession(cart);
            }
            return RedirectToAction("Index");
        }

        #endregion

        #region THANH TOÁN & ĐẶT HÀNG

        /// <summary>
        /// Trang điền thông tin thanh toán.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCart();
            if (cart.Count == 0) return RedirectToAction("Index");

            string? userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int customerId))
                return RedirectToAction("Login", "Account");

            ViewBag.Cart = cart;
            ViewBag.Provinces = await DictionaryDataService.ListProvincesAsync();

            var customer = await PartnerDataService.GetCustomerAsync(customerId);
            return View(customer);
        }

        /// <summary>
        /// Xử lý lưu đơn hàng vào Database.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitOrder(string deliveryName, string deliveryPhone, string deliveryProvince, string deliveryAddress)
        {
            var cart = GetCart();
            if (cart.Count == 0) return RedirectToAction("Index");

            // Kiểm tra dữ liệu đầu vào (Server-side validation)
            if (string.IsNullOrWhiteSpace(deliveryProvince) ||
                string.IsNullOrWhiteSpace(deliveryAddress) ||
                string.IsNullOrWhiteSpace(deliveryPhone))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin giao hàng!";
                return RedirectToAction("Checkout");
            }

            string? userIdStr = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            int customerId = int.Parse(userIdStr);

            // 1. Tạo đơn hàng mới trong DB
            int orderId = await SalesDataService.AddOrderAsync(customerId, deliveryProvince, deliveryAddress);

            if (orderId > 0)
            {
                // 2. Lưu chi tiết từng sản phẩm trong đơn hàng
                foreach (var item in cart)
                {
                    await SalesDataService.SaveOrderDetailAsync(orderId, item.ProductID, item.Quantity, item.Price);
                }

                // 3. Đặt hàng thành công -> Xóa sạch giỏ hàng
                ClearCart();

                // 4. Chuyển sang trang thông báo thành công
                return RedirectToAction("Finish", new { id = orderId });
            }

            TempData["ErrorMessage"] = "Đã xảy ra lỗi hệ thống khi tạo đơn hàng. Vui lòng thử lại sau!";
            return RedirectToAction("Checkout");
        }

        /// <summary>
        /// Trang hiển thị thông báo "Đặt hàng thành công".
        /// </summary>
        [Authorize]
        public IActionResult Finish(int id)
        {
            if (id <= 0) return RedirectToAction("Index", "Home");

            ViewBag.OrderID = id;
            return View();
        }

        #endregion
    }
}