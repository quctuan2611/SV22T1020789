using Microsoft.AspNetCore.Mvc;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Partner;


namespace SV22T1020789.Admin.Controllers
{
    /// <summary>
    /// Cung cấp các chức năng liên quan đến người giao hàng
    /// </summary>
    public class ShipperController : Controller
    {
        /// <summary>
        /// Tên biến session lưu điều kiện tìm kiếm người giao hàng
        /// </summary>
        private const string SHIPPER_SEARCH_INPUT = "ShipperSearchInput";

        /// <summary>
        /// Giao diện để nhập đầu vào tìm kiếm và hiển thị kết quả tìm kiếm
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(SHIPPER_SEARCH_INPUT);
            if (input == null)
                input = new PaginationSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = ""
                };
            return View(input);
        }

        /// <summary>
        /// tìm kiếm người giao hàng và trả về kết quả dưới dạng phân trang
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {
            //await Task.Delay(1000);
            var result = await PartnerDataService.ListShippersAsync(input);
            ApplicationContext.SetSessionData(SHIPPER_SEARCH_INPUT, input);
            return View(result);
        }

        /// <summary>
        /// Bổ sung người giao hàng mới vào hệ thống
        /// </summary>
        /// <returns></returns>
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung người giao hàng";
            var model = new Shipper()
            {
                ShipperID = 0
            };
            return View("Edit", model);
        }

        /// <summary>
        /// Cập nhật thông tin người giao hàng đã có trong hệ thống
        /// </summary>
        /// <param name="id">Mã người giao hàng cần cập nhật thông tin</param>
        /// <returns></returns>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin người giao hàng";
            var model = await PartnerDataService.GetShipperAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveData(Shipper data)
        {
            try
            {
                ViewBag.Title = data.ShipperID == 0 ? "Bổ sung người giao hàng" : "Cập nhật thông tin người giao hàng";

                //TODO: Kiểm tra tính hợp lệ của dữ liệu và thông báo lỗi nếu dữ liệu không hợp lệ
                //Sử dụng ModelState để kiểm soát thông báo lỗi và gửi thông báo lỗi cho View

                if (string.IsNullOrWhiteSpace(data.ShipperName))
                    ModelState.AddModelError(nameof(data.ShipperName), "Vui lòng nhập tên người giao hàng");

                if (string.IsNullOrWhiteSpace(data.Phone))
                    ModelState.AddModelError(nameof(data.Phone), "Vui lòng cho biết số điện thoại của người giao hàng");

                // Điều chỉnh lại các giá trị dữ liệu khác theo qui định/qui ước của App
                if (string.IsNullOrEmpty(data.Phone)) data.Phone = "";

                if (!ModelState.IsValid)
                {
                    return View("Edit", data);
                }

                //Yêu cầu lưu dữ liệu vào CSDL
                if (data.ShipperID == 0)
                {
                    await PartnerDataService.AddShipperAsync(data);
                }
                else
                {
                    await PartnerDataService.UpdateShipperAsync(data);
                }
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                //Lưu log lỗi trong ex
                ModelState.AddModelError("Error", "Hệ thống đang bận, vui lòng thử lại sau");
                return View("Edit", data);
            }
        }

        /// <summary>
        /// Xóa người giao hàng ra khỏi hệ thống
        /// </summary>
        /// <param name="id">Mã người giao hàng cần xóa</param>
        /// <returns></returns>
        public async Task<IActionResult> Delete(int id)
        {
            if (Request.Method == "POST")
            {
                await PartnerDataService.DeleteShipperAsync(id);
                return RedirectToAction("Index");
            }

            var model = await PartnerDataService.GetShipperAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            ViewBag.AllowDelete = !(await PartnerDataService.IsUsedShipperAsync(id));

            return View(model);
        }
    }
}