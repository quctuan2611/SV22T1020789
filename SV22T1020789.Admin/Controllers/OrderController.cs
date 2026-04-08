using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SV22T1020789.Admin.AppCodes;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Models.Catalog;
using SV22T1020789.Models.Sales;
using SV22T1020789.Models.Common;

namespace SV22T1020789.Admin.Controllers
{
    [Authorize(Roles = "sale,admin")]
    public class OrderController : Controller
    {
        private const string ORDER_SEARCH_INPUT = "OrderSearchInput";
        private const string SEARCH_PRODUCT = "SearchProductToSale";

        /// <summary>
        /// Giao diện tìm kiếm đơn hàng 
        /// </summary>
        public IActionResult Index()
        {
            var input = ApplicationContext.GetSessionData<OrderSearchInput>(ORDER_SEARCH_INPUT);
            if (input == null)
            {
                input = new OrderSearchInput()
                {
                    Page = 1,
                    PageSize = ApplicationContext.PageSize,
                    SearchValue = "",
                    Status = 0,
                    DateFrom = null,
                    DateTo = null
                };
            }
            return View(input);
        }

        /// <summary>
        /// Tìm kiếm và hiển thị danh sách đơn hàng (AJAX)
        /// </summary>
        public async Task<IActionResult> Search(OrderSearchInput input)
        {
            ApplicationContext.SetSessionData(ORDER_SEARCH_INPUT, input);
            var result = await SalesDataService.ListOrdersAsync(input);
            return View(result); 
        }

        /// <summary>
        /// Giao diện trang lập đơn hàng mới
        /// </summary>
        public IActionResult Create()
        {
            var input = ApplicationContext.GetSessionData<ProductSearchInput>(SEARCH_PRODUCT);
            if (input == null)
            {
                input = new ProductSearchInput()
                {
                    Page = 1,
                    PageSize = 3,
                    SearchValue = "",
                    CategoryID = 0,
                    MinPrice = 0,
                    MaxPrice = 0,
                };
            }
            return View(input);
        }

        /// <summary>
        /// Tìm kiếm sản phẩm để đưa vào giỏ hàng
        /// </summary>
        public async Task<IActionResult> SearchProduct(ProductSearchInput input)
        {
            if (input.Page == 0) input.Page = 1;
            if (input.PageSize == 0) input.PageSize = 3;

            var result = await CatalogDataService.ListProductsAsync(input);
            ApplicationContext.SetSessionData(SEARCH_PRODUCT, input);

            return PartialView(result);
        }

        /// <summary>
        /// Xử lý lập đơn hàng mới từ giỏ hàng
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder(int customerID = 0, string province = "", string address = "")
        {
            var cart = ShoppingCartHelper.GetShoppingCart();
            if (cart.Count == 0)
                return Json(new ApiResult(0, "Giỏ hàng đang trống"));

            if (string.IsNullOrWhiteSpace(province))
                return Json(new ApiResult(0, "Vui lòng nhập tỉnh/thành giao hàng"));
            if (string.IsNullOrWhiteSpace(address))
                return Json(new ApiResult(0, "Vui lòng nhập địa chỉ giao hàng"));

            // Gọi Service lập đơn (customerID = 0 sẽ được xử lý thành null trong Service)
            int orderID = await SalesDataService.AddOrderAsync(customerID, province, address);

            foreach (var item in cart)
            {
                await SalesDataService.AddDetailAsync(new OrderDetail()
                {
                    OrderID = orderID,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    SalePrice = item.SalePrice
                });
            }

            ShoppingCartHelper.ClearCart();
            return Json(new ApiResult(orderID, "Lập đơn hàng thành công!"));
        }

       

        /// <summary>
        /// Hiển thị danh sách mặt hàng đang có trong giỏ
        /// </summary>
        public IActionResult ShowCart() => View(ShoppingCartHelper.GetShoppingCart());

        /// <summary>
        /// Thêm một mặt hàng vào giỏ
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddCartItem(int productId = 0, int quantity = 0, decimal price = 0)
        {
            var product = await CatalogDataService.GetProductAsync(productId);
            if (product == null) return Json(new ApiResult(0, "Mặt hàng không tồn tại"));
            if (!product.IsSelling) return Json(new ApiResult(0, "Mặt hàng này đã ngừng bán"));

            var item = new OrderDetailViewInfo()
            {
                ProductID = productId,
                ProductName = product.ProductName,
                Unit = product.Unit,
                Photo = product.Photo ?? "nophoto.png",
                Quantity = quantity,
                SalePrice = price
            };
            ShoppingCartHelper.AddItemToCart(item);
            return Json(new ApiResult(1, ""));
        }

        /// <summary>
        /// Mở Form sửa số lượng/giá bán của mặt hàng trong giỏ
        /// </summary>
        public IActionResult EditCartItem(int productId = 0) => PartialView(ShoppingCartHelper.GetCartItem(productId));

        /// <summary>
        /// Cập nhật thông tin mặt hàng trong giỏ
        /// </summary>
        [HttpPost]
        public IActionResult UpdateCartItem(int productID, int quantity, decimal salePrice)
        {
            if (salePrice < 0) return Json(new ApiResult(0, "Giá hàng phải lớn hơn 0"));
            ShoppingCartHelper.UpdateCartItem(productID, quantity, salePrice);
            return Json(new ApiResult(1, ""));
        }

        /// <summary>
        /// Xóa mặt hàng khỏi giỏ
        /// </summary>
        public IActionResult DeleteCartItem(int productId = 0)
        {
            if (Request.Method == "POST") { ShoppingCartHelper.RemoveItemFromCart(productId); return Json(new ApiResult(1, "")); }
            ViewBag.ProductID = productId;
            return PartialView();
        }

        /// <summary>
        /// Xóa sạch giỏ hàng
        /// </summary>
        public IActionResult ClearCart()
        {
            if (Request.Method == "POST") { ShoppingCartHelper.ClearCart(); return Json(new ApiResult(1, "")); }
            return PartialView();
        }

       

        /// <summary>
        /// Xem thông tin chi tiết của một đơn hàng
        /// </summary>
        public async Task<IActionResult> Detail(int id)
        {
            var model = await SalesDataService.GetOrderAsync(id);
            if (model == null) return RedirectToAction("Index");

            var details = await SalesDataService.ListDetailsAsync(id);
            ViewBag.Details = details;

            return View(model);
        }

        /// <summary>
        /// Mở form sửa chi tiết 1 mặt hàng trong đơn hàng đã lập
        /// </summary>
        public async Task<IActionResult> EditDetail(int id = 0, int productId = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            
            if (order == null || (int)order.Status != 1)
                return Content("Đơn hàng đã được duyệt, không thể sửa đổi mặt hàng!");

            var details = await SalesDataService.ListDetailsAsync(id);
            var item = details.FirstOrDefault(m => m.ProductID == productId);
            if (item == null) return RedirectToAction("Detail", new { id = id });

            return PartialView(item);
        }

        /// <summary>
        /// Cập nhật chi tiết 1 mặt hàng trong đơn hàng đã lập
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateDetail(int orderID, int productID, int quantity, decimal salePrice)
        {
            var order = await SalesDataService.GetOrderAsync(orderID);
            
            if (order == null || (int)order.Status != 1)
            {
                TempData["Message"] = "Đơn hàng đã được duyệt, không thể thay đổi mặt hàng.";
                return RedirectToAction("Detail", new { id = orderID });
            }

            if (quantity <= 0)
            {
                TempData["Message"] = "Số lượng phải lớn hơn 0";
                return RedirectToAction("Detail", new { id = orderID });
            }

            if (salePrice < 0)
            {
                TempData["Message"] = "Giá bán không được âm";
                return RedirectToAction("Detail", new { id = orderID });
            }

            await SalesDataService.SaveOrderDetailAsync(orderID, productID, quantity, salePrice);
            return RedirectToAction("Detail", new { id = orderID });
        }

        /// <summary>
        /// Xóa 1 mặt hàng khỏi đơn hàng đã lập
        /// </summary>
        public async Task<IActionResult> DeleteDetail(int id = 0, int productId = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            
            if (order == null || (int)order.Status != 1)
            {
                TempData["Message"] = "Không thể xóa mặt hàng vì đơn hàng đã được duyệt hoặc xử lý.";
                return RedirectToAction("Detail", new { id = id });
            }

            if (Request.Method == "POST")
            {
                await SalesDataService.DeleteOrderDetailAsync(id, productId);
                return RedirectToAction("Detail", new { id = id });
            }

            var details = await SalesDataService.ListDetailsAsync(id);
            var item = details.FirstOrDefault(m => m.ProductID == productId);
            if (item == null) return RedirectToAction("Detail", new { id = id });

            return PartialView(item);
        }



        /// <summary>
        /// Duyệt chấp nhận đơn hàng 
        /// </summary>
        public async Task<IActionResult> Accept(int id = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null || (int)order.Status != 1)
                return Content("Đơn hàng này không ở trạng thái chờ duyệt!");

            if (Request.Method == "POST")
            {
                
                var userData = User.GetUserData();

               
                int employeeID = userData != null ? Convert.ToInt32(userData.UserId) : 0;

                
                await SalesDataService.AcceptOrderAsync(id, employeeID);
                return RedirectToAction("Detail", new { id = id });
            }
            return PartialView(id);
        }

        /// <summary>
        /// Chuyển trạng thái đơn hàng sang Đang giao (Chỉ cho phép khi Đã duyệt - Status = 2)
        /// </summary>
        public async Task<IActionResult> Shipping(int id = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            
            if (order == null || (int)order.Status != 2)
                return Content("Đơn hàng phải được duyệt trước khi giao hàng!");

            if (Request.Method == "POST")
            {
                int shipperID = Convert.ToInt32(Request.Form["shipperID"]);
                if (shipperID <= 0)
                {
                    TempData["Message"] = "Vui lòng chọn người giao hàng.";
                    return RedirectToAction("Detail", new { id = id });
                }

                await SalesDataService.ShipOrderAsync(id, shipperID);
                return RedirectToAction("Detail", new { id = id });
            }

            var input = new PaginationSearchInput() { Page = 1, PageSize = 100, SearchValue = "" };
            var result = await PartnerDataService.ListShippersAsync(input);
            
            ViewBag.Shippers = result.DataItems;

            return PartialView(id);
        }

        /// <summary>
        /// Xác nhận đơn hàng đã giao thành công (Chỉ cho phép khi Đang giao - Status = 3)
        /// </summary>
        public async Task<IActionResult> Finish(int id = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            // SỬA Ở ĐÂY: Thêm (int)
            if (order == null || (int)order.Status != 3)
                return Content("Đơn hàng chưa được giao, không thể hoàn tất!");

            if (Request.Method == "POST")
            {
                await SalesDataService.CompleteOrderAsync(id);
                return RedirectToAction("Detail", new { id = id });
            }
            return PartialView(id);
        }

        /// <summary>
        /// Từ chối đơn hàng 
        /// </summary>
        public async Task<IActionResult> Reject(int id = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            if (order == null || (int)order.Status != 1)
                return Content("Chỉ có thể từ chối những đơn hàng vừa mới lập!");

            if (Request.Method == "POST")
            {
                
                var userData = User.GetUserData();
                int employeeID = userData != null ? Convert.ToInt32(userData.UserId) : 0;

                
                await SalesDataService.RejectOrderAsync(id, employeeID);
                return RedirectToAction("Detail", new { id = id });
            }
            return PartialView(id);
        }

        /// <summary>
        /// Hủy đơn hàng (Chỉ cho phép khi đơn Mới hoặc Đã duyệt - Status = 1 hoặc 2)
        /// </summary>
        public async Task<IActionResult> Cancel(int id = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            
            if (order == null || ((int)order.Status != 1 && (int)order.Status != 2))
                return Content("Đơn hàng đang giao hoặc đã hoàn tất, không thể hủy!");

            if (Request.Method == "POST")
            {
                await SalesDataService.CancelOrderAsync(id);
                return RedirectToAction("Detail", new { id = id });
            }
            return PartialView(id);
        }

        /// <summary>
        /// Xóa hoàn toàn đơn hàng khỏi hệ thống (Chỉ cho phép khi Status = 1, -1, -2)
        /// </summary>
        public async Task<IActionResult> Delete(int id = 0)
        {
            var order = await SalesDataService.GetOrderAsync(id);
            
            if (order == null || ((int)order.Status != 1 && (int)order.Status != -1 && (int)order.Status != -2))
                return Content("Tuyệt đối không được xóa đơn hàng đã duyệt, đang giao hoặc đã hoàn tất!");

            if (Request.Method == "POST")
            {
                await SalesDataService.DeleteOrderAsync(id);
                return RedirectToAction("Index");
            }
            return PartialView(id);
        }
    }
}