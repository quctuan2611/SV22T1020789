using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Models.Catalog;
using SV22T1020789.Models.Common;
using System; 
using System.IO; 
using System.Threading.Tasks; 

namespace SV22T1020789.Admin.Controllers
{
    /// <summary>
    /// Các chức năng liên quan đến quản lý mặt hàng (Products), bao gồm cả thuộc tính và hình ảnh
    /// </summary>
    [Authorize(Roles = "product,admin")]
    public class ProductController : Controller
    {
        private const string PRODUCT_SEARCH_INPUT = "ProductSearchInput";

        /// <summary>
        /// Hàm hỗ trợ: Chuẩn bị dữ liệu danh sách Loại hàng và Nhà cung cấp để nạp vào ViewBag dùng cho các DropdownList
        /// </summary>
        private async Task SetupViewBagsAsync()
        {
            var categoryInput = new PaginationSearchInput { Page = 1, PageSize = 999, SearchValue = "" };
            var categoryResult = await CatalogDataService.ListCategoriesAsync(categoryInput);
            ViewBag.Categories = categoryResult.DataItems;

            var supplierInput = new PaginationSearchInput { Page = 1, PageSize = 999, SearchValue = "" };
            var supplierResult = await PartnerDataService.ListSuppliersAsync(supplierInput);
            ViewBag.Suppliers = supplierResult.DataItems;
        }

       

        /// <summary>
        /// Giao diện tìm kiếm và hiển thị danh sách mặt hàng (Lấy trạng thái từ Session)
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var input = ApplicationContext.GetSessionData<ProductSearchInput>(PRODUCT_SEARCH_INPUT);
            if (input == null)
            {
                input = new ProductSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = "",
                    CategoryID = 0,
                    SupplierID = 0,
                    MinPrice = 0,
                    MaxPrice = 0
                };
            }

            await SetupViewBagsAsync();
            return View(input);
        }

        /// <summary>
        /// Xử lý tìm kiếm mặt hàng và trả về kết quả (dùng cho AJAX)
        /// </summary>
        public async Task<IActionResult> Search(ProductSearchInput input)
        {
            ApplicationContext.SetSessionData(PRODUCT_SEARCH_INPUT, input);
            var result = await CatalogDataService.ListProductsAsync(input);
            return View(result);
        }

        /// <summary>
        /// Giao diện bổ sung mặt hàng mới
        /// </summary>
        public async Task<IActionResult> Create()
        {
            ViewBag.Title = "Bổ sung mặt hàng";
            await SetupViewBagsAsync();

            var model = new Product()
            {
                ProductID = 0,
                IsSelling = true, // Mặc định tạo mới là đang bán
                Photo = "nophoto.png"
            };
            return View("Edit", model);
        }

        /// <summary>
        /// Giao diện cập nhật thông tin mặt hàng hiện có
        /// </summary>
        /// <param name="id">Mã mặt hàng cần sửa</param>
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Title = "Cập nhật thông tin mặt hàng";
            var model = await CatalogDataService.GetProductAsync(id);

            if (model == null)
                return RedirectToAction("Index");

            await SetupViewBagsAsync();
            return View(model);
        }

        /// <summary>
        /// Xử lý lưu dữ liệu mặt hàng (dùng chung cho cả Thêm mới và Cập nhật)
        /// </summary>
        /// <param name="data">Dữ liệu mặt hàng từ Form</param>
        /// <param name="uploadPhoto">File ảnh tải lên (nếu có)</param>
        [HttpPost]
        public async Task<IActionResult> Save(Product data, IFormFile? uploadPhoto)
        {
            try
            {
                ViewBag.Title = data.ProductID == 0 ? "Bổ sung mặt hàng" : "Cập nhật thông tin mặt hàng";

                // Kiểm tra Validation
                if (string.IsNullOrWhiteSpace(data.ProductName))
                    ModelState.AddModelError(nameof(data.ProductName), "Vui lòng nhập tên mặt hàng");

                if (data.CategoryID == 0)
                    ModelState.AddModelError(nameof(data.CategoryID), "Vui lòng chọn loại hàng");

                if (data.SupplierID == 0)
                    ModelState.AddModelError(nameof(data.SupplierID), "Vui lòng chọn nhà cung cấp");

                if (string.IsNullOrWhiteSpace(data.Unit))
                    ModelState.AddModelError(nameof(data.Unit), "Vui lòng nhập đơn vị tính");

                if (data.Price <= 0)
                    ModelState.AddModelError(nameof(data.Price), "Giá bán phải lớn hơn 0");

                if (!ModelState.IsValid)
                {
                    await SetupViewBagsAsync();
                    return View("Edit", data);
                }

                // Xử lý Upload file ảnh
                if (uploadPhoto != null)
                {
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(uploadPhoto.FileName)}";
                    var filePath = Path.Combine(ApplicationContext.WWWRootPath, "images/products", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadPhoto.CopyToAsync(stream);
                    }
                    data.Photo = fileName;
                }

                // Xử lý dữ liệu rỗng trước khi nạp vào DB
                if (string.IsNullOrEmpty(data.ProductDescription)) data.ProductDescription = "";
                if (string.IsNullOrEmpty(data.Photo)) data.Photo = "nophoto.png";

                if (data.ProductID == 0)
                {
                    await CatalogDataService.AddProductAsync(data);
                }
                else
                {
                    await CatalogDataService.UpdateProductAsync(data);
                }

                return RedirectToAction("Index");
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Hệ thống đang bận hoặc dữ liệu không hợp lệ. Vui lòng kiểm tra dữ liệu hoặc thử lại sau");
                await SetupViewBagsAsync();
                return View("Edit", data);
            }
        }

        /// <summary>
        /// Xóa mặt hàng (chỉ cho phép xóa khi mặt hàng không bán và chưa phát sinh giao dịch)
        /// </summary>
        /// <param name="id">Mã mặt hàng cần xóa</param>
        public async Task<IActionResult> Delete(int id)
        {
            if (Request.Method == "POST")
            {
                var product = await CatalogDataService.GetProductAsync(id);

                if (product == null || product.IsSelling)
                {
                    return RedirectToAction("Index");
                }

                await CatalogDataService.DeleteProductAsync(id);
                return RedirectToAction("Index");
            }

            var model = await CatalogDataService.GetProductAsync(id);
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            ViewBag.AllowDelete = !model.IsSelling && !await CatalogDataService.IsUsedProductAsync(id);

            return View(model);
        }

        

        /// <summary>
        /// Hiển thị danh sách các thuộc tính của một mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng</param>
        public async Task<IActionResult> ListAttributes(int id)
        {
            ViewBag.Title = "Danh sách thuộc tính của sản phẩm";

            var product = await CatalogDataService.GetProductAsync(id);
            if (product == null) return RedirectToAction("Index");

            ViewBag.Product = product;
            ViewBag.ProductID = id; 

            var model = await CatalogDataService.ListAttributesAsync(id);
            return View(model);
        }

        /// <summary>
        /// Giao diện bổ sung thuộc tính mới cho mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng</param>
        public IActionResult CreateAttribute(int id)
        {
            ViewBag.Title = "Bổ sung thuộc tính cho sản phẩm";
            var model = new ProductAttribute()
            {
                AttributeID = 0,
                ProductID = id
            };
            return View("EditAttribute", model);
        }

        /// <summary>
        /// Giao diện cập nhật thuộc tính hiện có của mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng</param>
        /// <param name="attributeId">Mã thuộc tính cần sửa</param>
        public async Task<IActionResult> EditAttribute(int id, int attributeId)
        {
            ViewBag.Title = "Cập nhật thông tin thuộc tính của sản phẩm";

            var model = await CatalogDataService.GetAttributeAsync(attributeId);
            if (model == null) return RedirectToAction("Edit", new { id = id });

            return View(model);
        }

        /// <summary>
        /// Xử lý lưu dữ liệu thuộc tính (dùng chung cho Thêm mới và Cập nhật)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveAttribute(ProductAttribute data)
        {
            try
            {
                ViewBag.Title = data.AttributeID == 0 ? "Bổ sung thuộc tính" : "Cập nhật thuộc tính";

                if (string.IsNullOrWhiteSpace(data.AttributeName))
                    ModelState.AddModelError(nameof(data.AttributeName), "Vui lòng nhập tên thuộc tính");
                if (string.IsNullOrWhiteSpace(data.AttributeValue))
                    ModelState.AddModelError(nameof(data.AttributeValue), "Vui lòng nhập giá trị thuộc tính");

                if (!ModelState.IsValid)
                {
                    return View("EditAttribute", data);
                }

                if (data.AttributeID == 0)
                    await CatalogDataService.AddAttributeAsync(data);
                else
                    await CatalogDataService.UpdateAttributeAsync(data);

                // Lưu xong thì quay về trang Edit mặt hàng
                return RedirectToAction("Edit", new { id = data.ProductID });
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Hệ thống đang bận, vui lòng thử lại sau!");
                return View("EditAttribute", data);
            }
        }

        /// <summary>
        /// Xóa thuộc tính của mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng (để quay lại trang chi tiết)</param>
        /// <param name="attributeId">Mã thuộc tính cần xóa</param>
        public async Task<IActionResult> DeleteAttribute(int id, int attributeId)
        {
            if (Request.Method == "POST")
            {
                await CatalogDataService.DeleteAttributeAsync(attributeId);
                return RedirectToAction("Edit", new { id = id });
            }

            var model = await CatalogDataService.GetAttributeAsync(attributeId);
            if (model == null) return RedirectToAction("Edit", new { id = id });

            ViewBag.Title = "Xóa thuộc tính của sản phẩm";
            return View(model);
        }

        

        /// <summary>
        /// Hiển thị danh sách các hình ảnh của một mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng</param>
        public async Task<IActionResult> ListPhotos(int id)
        {
            ViewBag.Title = "Danh sách hình ảnh của sản phẩm";

            var product = await CatalogDataService.GetProductAsync(id);
            if (product == null) return RedirectToAction("Index");
            ViewBag.Product = product;

            var model = await CatalogDataService.ListPhotosAsync(id);
            return View(model);
        }

        /// <summary>
        /// Giao diện bổ sung hình ảnh mới cho mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng</param>
        public IActionResult CreatePhoto(int id)
        {
            ViewBag.Title = "Bổ sung hình ảnh cho sản phẩm";
            var model = new ProductPhoto()
            {
                PhotoID = 0,
                ProductID = id,
                IsHidden = false,
                Photo = "nophoto.png"
            };
            return View("EditPhoto", model);
        }

        /// <summary>
        /// Giao diện cập nhật thông tin hình ảnh hiện có của mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng</param>
        /// <param name="photoId">Mã hình ảnh cần sửa</param>
        public async Task<IActionResult> EditPhoto(int id, int photoId)
        {
            ViewBag.Title = "Cập nhật thông tin hình ảnh của sản phẩm";
            var model = await CatalogDataService.GetPhotoAsync(photoId);
            if (model == null) return RedirectToAction("Edit", new { id = id });

            return View(model);
        }

        /// <summary>
        /// Xử lý lưu thông tin hình ảnh (dùng chung cho Thêm mới và Cập nhật, có xử lý file ảnh)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SavePhoto(ProductPhoto data, IFormFile? uploadPhoto)
        {
            try
            {
                ViewBag.Title = data.PhotoID == 0 ? "Bổ sung hình ảnh" : "Cập nhật hình ảnh";

                if (uploadPhoto != null)
                {
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(uploadPhoto.FileName)}";
                    var filePath = Path.Combine(ApplicationContext.WWWRootPath, "images/products", fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadPhoto.CopyToAsync(stream);
                    }
                    data.Photo = fileName;
                }

                if (string.IsNullOrWhiteSpace(data.Photo))
                    data.Photo = "nophoto.png";

                if (string.IsNullOrWhiteSpace(data.Description))
                    ModelState.AddModelError(nameof(data.Description), "Vui lòng nhập mô tả ảnh");
                if (data.DisplayOrder <= 0)
                    ModelState.AddModelError(nameof(data.DisplayOrder), "Thứ tự hiển thị phải lớn hơn 0");

                if (!ModelState.IsValid)
                {
                    return View("EditPhoto", data);
                }

                if (data.PhotoID == 0)
                    await CatalogDataService.AddPhotoAsync(data);
                else
                    await CatalogDataService.UpdatePhotoAsync(data);

                // Lưu xong thì quay về trang Edit mặt hàng
                return RedirectToAction("Edit", new { id = data.ProductID });
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Hệ thống đang bận, vui lòng thử lại sau!");
                return View("EditPhoto", data);
            }
        }

        /// <summary>
        /// Xóa hình ảnh của mặt hàng
        /// </summary>
        /// <param name="id">Mã mặt hàng (để quay lại trang chi tiết)</param>
        /// <param name="photoId">Mã hình ảnh cần xóa</param>
        public async Task<IActionResult> DeletePhoto(int id, int photoId)
        {
            if (Request.Method == "POST")
            {
                await CatalogDataService.DeletePhotoAsync(photoId);
                return RedirectToAction("Edit", new { id = id });
            }

            var model = await CatalogDataService.GetPhotoAsync(photoId);
            if (model == null) return RedirectToAction("Edit", new { id = id });

            ViewBag.Title = "Xóa hình ảnh của sản phẩm";
            return View(model);
        }
    }
}