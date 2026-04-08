using Microsoft.AspNetCore.Mvc;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Models.Catalog;
using SV22T1020789.Models.Common;


namespace SV22T1020789.Admin.Controllers
{
    /// <summary>
    /// Cung cấp các chức năng liên quan đến loại hàng
    /// </summary>
    public class CategoryController : Controller
    {
        /// <summary>
        /// Tên biến session lưu điều kiện tìm kiếm loại hàng
        /// </summary>
        private const string CATEGORY_SEARCH_INPUT = "CategorySearchInput";

        /// <summary>
        /// Giao diện để nhập đầu vào tìm kiếm và hiển thị kết quả tìm kiếm
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            // Lấy điều kiện tìm kiếm từ session
            var input = ApplicationContext.GetSessionData<PaginationSearchInput>(CATEGORY_SEARCH_INPUT);

            // Nếu không có trong session thì tạo mới
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
        /// Tìm kiếm loại hàng và trả về kết quả dưới dạng phân trang
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<IActionResult> Search(PaginationSearchInput input)
        {
            // Lưu lại điều kiện tìm kiếm vào session
            ApplicationContext.SetSessionData(CATEGORY_SEARCH_INPUT, input);

            // Gọi tầng Service để lấy dữ liệu 
            var result = await CatalogDataService.ListCategoriesAsync(input);

            return View(result);
        }

        /// <summary>
        /// Bổ sung Loại hàng mới
        /// </summary>
        /// <returns></returns>
        public IActionResult Create()
        {
            ViewBag.Title = "Bổ sung loại hàng";
            var model = new Category()
            {
                CategoryID = 0
            };
            return View("Edit", model);
        }

        /// <summary>
        /// Cập nhật thông tin loại hàng
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin loại hàng";
            var model = await CatalogDataService.GetCategoryAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            return View(model);
        }

        /// <summary>
        /// Xử lý lưu dữ liệu thêm mới/cập nhật loại hàng
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SaveData(Category data)
        {
            try
            {
                ViewBag.Title = data.CategoryID == 0 ? "Bổ sung loại hàng" : "Cập nhật thông tin loại hàng";

                // Kiểm tra dữ liệu đầu vào
                if (string.IsNullOrWhiteSpace(data.CategoryName))
                    ModelState.AddModelError(nameof(data.CategoryName), "Vui lòng nhập tên loại hàng");

                // Tiền xử lý dữ liệu trước khi lưu
                if (string.IsNullOrEmpty(data.Description)) data.Description = "";

                // Trả về view nếu có lỗi validation
                if (!ModelState.IsValid)
                {
                    return View("Edit", data);
                }

                // Lưu dữ liệu
                if (data.CategoryID == 0)
                {
                    await CatalogDataService.AddCategoryAsync(data);
                }
                else
                {
                    await CatalogDataService.UpdateCategoryAsync(data);
                }
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                // Lưu log lỗi ở đây nếu cần thiết
                ModelState.AddModelError("Error", "Hệ thống đang bận, vui lòng thử lại sau");
                return View("Edit", data);
            }
        }

        /// <summary>
        /// Xóa loại hàng
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> Delete(int id)
        {
            // 1. Khi người dùng bấm nút Xóa xác nhận trên Form (Submit bằng POST)
            if (Request.Method == "POST")
            {
                await CatalogDataService.DeleteCategoryAsync(id);
                return RedirectToAction("Index");
            }

            // 2. Khi load giao diện để xác nhận xóa (GET)
            var model = await CatalogDataService.GetCategoryAsync(id);
            if (model == null)
                return RedirectToAction("Index");

            // Kiểm tra xem loại hàng này đã có sản phẩm (dữ liệu liên quan) hay chưa
            ViewBag.AllowDelete = !(await CatalogDataService.IsUsedCategoryAsync(id));

            return View(model);
        }
    }
}