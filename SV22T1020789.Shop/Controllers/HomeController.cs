using Microsoft.AspNetCore.Mvc;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Models;
using SV22T1020789.Models.Catalog;
using SV22T1020789.Models.Common;

namespace SV22T1020789.Shop.Controllers
{
    public class HomeController : Controller
    {
        /// <summary>
        /// Hàm này vừa hiển thị trang chủ, vừa nhận tìm kiếm, phân trang, lọc theo loại hàng VÀ lọc theo khoảng giá
        /// </summary>
        public async Task<IActionResult> Index(int page = 1, string searchValue = "", int categoryId = 0, decimal minPrice = 0, decimal maxPrice = 0)
        {
            int pageSize = 12;

            var input = new ProductSearchInput()
            {
                Page = page,
                PageSize = pageSize,
                SearchValue = searchValue ?? "",
                CategoryID = categoryId,
                SupplierID = 0,
                MinPrice = minPrice,
                MaxPrice = maxPrice
            };

            var model = await CatalogDataService.ListProductsAsync(input);

            var categoryInput = new PaginationSearchInput { Page = 1, PageSize = 100, SearchValue = "" };
            var categoryResult = await CatalogDataService.ListCategoriesAsync(categoryInput);
            ViewBag.Categories = categoryResult.DataItems;

            ViewBag.SearchValue = input.SearchValue;
            ViewBag.CategoryID = input.CategoryID;
            ViewBag.MinPrice = input.MinPrice;
            ViewBag.MaxPrice = input.MaxPrice;

            return View(model);
        }

        /// <summary>
        /// Lấy và hiển thị thông tin chi tiết của một sản phẩm.
        /// </summary>
        public async Task<IActionResult> Details(int id = 0)
        {
            if (id <= 0)
            {
                return RedirectToAction("Index");
            }

            var product = await CatalogDataService.GetProductAsync(id);

            if (product == null)
            {
                return RedirectToAction("Index");
            }

            if (product.CategoryID.HasValue && product.CategoryID > 0)
            {
                var category = await CatalogDataService.GetCategoryAsync(product.CategoryID.Value);
                ViewBag.CategoryName = category != null ? category.CategoryName : "Chưa xác định";
            }
            else
            {
                ViewBag.CategoryName = "Chưa xác định";
            }

            ViewBag.Photos = await CatalogDataService.ListPhotosAsync(id);
            ViewBag.Attributes = await CatalogDataService.ListAttributesAsync(id);

            return View(product);
        }
    }
}