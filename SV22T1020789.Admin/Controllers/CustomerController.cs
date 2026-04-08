using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Partner;
using SV22T1020789.BusinessLayers;
using System;
using System.Threading.Tasks;

namespace SV22T1020789.Admin.Controllers
{
    /// <summary>
    /// Cung cấp các chức năng liên quan đến khách hàng
    /// </summary>
    [Authorize(Roles = "customer,admin")]
    public class CustomerController : Controller
    {
        /// <summary>
        /// Tên biến session lưu điều kiện tìm kiếm khách hàng
        /// </summary>
        private const string CUSTOMER_SEARCH_INPUT = "CustomerSearchInput";

        /// <summary>
        /// Giao diện để nhập đầu vào tìm kiếm và hiển thị kết quả tìm kiếm
        /// </summary>
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(CUSTOMER_SEARCH_INPUT);
            if (input == null)
            {
                input = new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = ""
                };
            }
            return View(input);
        }

        /// <summary>
        /// Tìm kiếm khách hàng và trả về kết quả dưới dạng phân trang
        /// </summary>
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {
            var result = await PartnerDataService.ListCustomersAsync(input);
            ApplicationContext.SetSessionData(CUSTOMER_SEARCH_INPUT, input);
            return View(result);
        }

        /// <summary>
        /// Bổ sung khách hàng mới vào hệ thống
        /// </summary>
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung khách hàng";
            var model = new Customer()
            {
                CustomerID = 0
            };
            return View("Edit", model);
        }

        /// <summary>
        /// Cập nhật thông tin khách hàng đã có trong hệ thống
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin khách hàng";
            var model = await PartnerDataService.GetCustomerAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }

        /// <summary>
        /// Lưu dữ liệu khách hàng (Thêm mới hoặc Cập nhật)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveData(Customer data)
        {
            try
            {
                ViewBag.Title = data.CustomerID == 0 ? "Bổ sung khách hàng" : "Cập nhật thông tin khách hàng";

                // Kiểm tra tính hợp lệ của dữ liệu
                if (string.IsNullOrWhiteSpace(data.CustomerName))
                    ModelState.AddModelError(nameof(data.CustomerName), "Vui lòng nhập tên khách hàng");

                if (string.IsNullOrWhiteSpace(data.Email))
                    ModelState.AddModelError(nameof(data.Email), "Vui lòng cho biết Email của khách hàng");
                else if (!(await PartnerDataService.ValidateCustomerEmailAsync(data.Email, data.CustomerID)))
                    ModelState.AddModelError(nameof(data.Email), "Email này đã có người sử dụng");

                if (string.IsNullOrWhiteSpace(data.Province))
                    ModelState.AddModelError(nameof(data.Province), "Vui lòng chọn tỉnh/thành");

                // Điều chỉnh lại các giá trị null/empty
                data.ContactName ??= "";
                data.Phone ??= "";
                data.Address ??= "";

                if (!ModelState.IsValid)
                {
                    return View("Edit", data);
                }

                // Thực hiện lưu
                if (data.CustomerID == 0)
                    await PartnerDataService.AddCustomerAsync(data);
                else
                    await PartnerDataService.UpdateCustomerAsync(data);

                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                ModelState.AddModelError("Error", "Hệ thống đang bận, vui lòng thử lại sau");
                return View("Edit", data);
            }
        }

        /// <summary>
        /// Xóa khách hàng ra khỏi hệ thống
        /// </summary>
        public async Task<IActionResult> Delete(int id)
        {
            if (Request.Method == "POST")
            {
                await PartnerDataService.DeleteCustomerAsync(id);
                return RedirectToAction("Index");
            }

            var model = await PartnerDataService.GetCustomerAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await PartnerDataService.IsUsedCustomerAsync(id));
            return View(model);
        }

        /// <summary>
        /// Giao diện đổi mật khẩu khách hàng (GET)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ChangePassword(int id)
        {
            ViewBag.Title = "Đổi mật khẩu khách hàng";
            var model = await PartnerDataService.GetCustomerAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }

        /// <summary>
        /// Xử lý cập nhật mật khẩu khách hàng (POST)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ChangePassword(int id, string newPassword, string confirmPassword)
        {
            var customer = await PartnerDataService.GetCustomerAsync(id);
            if (customer == null)
                return RedirectToAction("Index");

            // 1. Kiểm tra tính hợp lệ mật khẩu
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                ModelState.AddModelError("Error", "Mật khẩu mới phải có ít nhất 6 ký tự.");

            if (newPassword != confirmPassword)
                ModelState.AddModelError("Error", "Mật khẩu xác nhận không khớp.");

            if (!ModelState.IsValid)
                return View(customer);

            try
            {
                // 2. Mã hóa mật khẩu (Sử dụng MD5 như bên Employee)
                string hashedNewPassword = CryptHelper.HashMD5(newPassword);

                // 3. Gọi SecurityDataService để Reset mật khẩu khách hàng
                // Lưu ý: Sử dụng hàm Reset riêng cho Customer đã tạo trong SecurityDataService
                bool isSuccess = await SecurityDataService.ResetCustomerPasswordAsync(customer.Email, hashedNewPassword);

                if (isSuccess)
                {
                    TempData["Message"] = $"Đã cấp lại mật khẩu cho khách hàng {customer.CustomerName} thành công!";
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError("Error", "Cập nhật mật khẩu không thành công.");
                return View(customer);
            }
            catch (Exception)
            {
                ModelState.AddModelError("Error", "Hệ thống đang bận, vui lòng thử lại sau.");
                return View(customer);
            }
        }
    }
}