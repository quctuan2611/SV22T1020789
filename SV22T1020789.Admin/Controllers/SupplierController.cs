using Microsoft.AspNetCore.Mvc;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Partner;


namespace SV22T1020789.Admin.Controllers
{
    /// <summary>
    /// Cung cấp các chức năng liên quan đến nhà cung cấp
    /// </summary>
    public class SupplierController : Controller
    {
        /// <summary>
        /// Tên biến session lưu điều kiện tìm kiếm nhà cung cấp
        /// </summary>
        private const string SUPPLIER_SEARCH_INPUT = "SupplierSearchInput";

        /// <summary>
        /// Giao diện để nhập đầu vào tìm kiếm và hiển thị kết quả tìm kiếm
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(SUPPLIER_SEARCH_INPUT);
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
        /// Tìm kiếm nhà cung cấp và trả về kết quả dưới dạng phân trang
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {
            var result = await PartnerDataService.ListSuppliersAsync(input);
            ApplicationContext.SetSessionData(SUPPLIER_SEARCH_INPUT, input);
            return View(result);
        }

        /// <summary>
        /// Bổ sung nhà cung cấp mới vào hệ thống
        /// </summary>
        /// <returns></returns>
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung Nhà cung cấp";
            // Giả sử model của bạn tên là Supplier. Nếu tên khác, bạn hãy điều chỉnh lại nhé.
            var model = new Supplier()
            {
                SupplierID = 0
            };
            return View("Edit", model);
        }

        /// <summary>
        /// Cập nhật thông tin nhà cung cấp đã có trong hệ thống
        /// </summary>
        /// <param name="id">Mã nhà cung cấp cần cập nhật thông tin</param>
        /// <returns></returns>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin nhà cung cấp";
            var model = await PartnerDataService.GetSupplierAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }

        /// <summary>
        /// Lưu dữ liệu nhà cung cấp (Thêm mới hoặc Cập nhật)
        /// </summary>
        /// <param name="data">Dữ liệu nhà cung cấp</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SaveData(Supplier data)
        {
            try
            {
                ViewBag.Title = data.SupplierID == 0 ? "Bổ sung Nhà cung cấp" : "Cập nhật thông tin nhà cung cấp";

                // Kiểm tra tính hợp lệ của dữ liệu
                if (string.IsNullOrWhiteSpace(data.SupplierName))
                    ModelState.AddModelError(nameof(data.SupplierName), "Vui lòng nhập tên nhà cung cấp");

                if (string.IsNullOrWhiteSpace(data.Email))
                    ModelState.AddModelError(nameof(data.Email), "Vui lòng cho biết Email của nhà cung cấp");

                if (string.IsNullOrWhiteSpace(data.Province))
                    ModelState.AddModelError(nameof(data.Province), "Vui lòng chọn tỉnh/thành");

                // Điều chỉnh lại các giá trị dữ liệu khác nếu null để tránh lỗi CSDL
                if (string.IsNullOrEmpty(data.ContactName)) data.ContactName = "";
                if (string.IsNullOrEmpty(data.Phone)) data.Phone = "";
                if (string.IsNullOrEmpty(data.Address)) data.Address = "";

                if (!ModelState.IsValid)
                {
                    return View("Edit", data);
                }

                // Yêu cầu lưu dữ liệu vào CSDL
                if (data.SupplierID == 0)
                {
                    await PartnerDataService.AddSupplierAsync(data);
                }
                else
                {
                    await PartnerDataService.UpdateSupplierAsync(data);
                }

                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                // Lưu log lỗi trong ex
                ModelState.AddModelError("Error", "Hệ thống đang bận, vui lòng thử lại sau");
                return View("Edit", data);
            }
        }

        /// <summary>
        /// Xóa nhà cung cấp ra khỏi hệ thống
        /// </summary>
        /// <param name="id">Mã nhà cung cấp cần xóa</param>
        /// <returns></returns>
        public async Task<IActionResult> Delete(int id)
        {
            if (Request.Method == "POST")
            {
                await PartnerDataService.DeleteSupplierAsync(id);
                return RedirectToAction("Index");
            }

            var model = await PartnerDataService.GetSupplierAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            // Kiểm tra xem nhà cung cấp này đã có dữ liệu liên quan (sản phẩm, đơn hàng...) chưa
            ViewBag.AllowDelete = !(await PartnerDataService.IsUsedSupplierAsync(id));

            return View(model);
        }
    }
}