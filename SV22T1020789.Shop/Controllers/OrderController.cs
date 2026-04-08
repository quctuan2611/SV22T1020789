using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SV22T1020789.BusinessLayers;
using SV22T1020789.Models.Sales;

namespace SV22T1020789.Shop.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        /// <summary>
        /// Lịch sử mua hàng của khách hàng
        /// </summary>
        public async Task<IActionResult> Index()
        {
            int.TryParse(User.FindFirstValue("UserId"), out int customerId);

            var result = await SalesDataService.ListOrdersAsync(new OrderSearchInput
            {
                Page = 1,
                PageSize = 1000,
                Status = 0,
                SearchValue = ""
            });

            var myOrders = result.DataItems.Where(x => x.CustomerID == customerId)
                                           .OrderByDescending(x => x.OrderTime).ToList();

            var orderTotals = new Dictionary<int, decimal>();
            foreach (var order in myOrders)
            {
                var details = await SalesDataService.ListDetailsAsync(order.OrderID);
                orderTotals[order.OrderID] = details.Sum(x => x.TotalPrice);
            }

            ViewBag.OrderTotals = orderTotals;
            return View(myOrders);
        }

        /// <summary>
        /// Chi tiết đơn hàng: Hiển thị danh sách món đã mua kèm Ảnh và Tên sản phẩm, cộng với Thông tin Khách hàng
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            // 1. Lấy thông tin đơn hàng gốc
            var order = await SalesDataService.GetOrderAsync(id);
            int.TryParse(User.FindFirstValue("UserId"), out int customerId);

            // Bảo mật: Nếu không tìm thấy đơn hoặc đơn không phải của mình thì đuổi về Index
            if (order == null || order.CustomerID != customerId)
                return RedirectToAction("Index");

            // 2. Lấy thông tin người đặt hàng (Chủ tài khoản)
            if (order.CustomerID.HasValue)
            {
                var customerInfo = await PartnerDataService.GetCustomerAsync(order.CustomerID.Value);
                ViewBag.CustomerInfo = customerInfo;
            }

            
            string receiverName = "";
            string receiverPhone = "";
            string actualAddress = order.DeliveryAddress ?? "";

            
            if (!string.IsNullOrEmpty(order.DeliveryAddress) && order.DeliveryAddress.Contains(" - "))
            {
                string[] addressParts = order.DeliveryAddress.Split(" - ");
                if (addressParts.Length >= 3)
                {
                    receiverName = addressParts[0];
                    receiverPhone = addressParts[1];
                   
                    actualAddress = string.Join(" - ", addressParts.Skip(2));
                }
            }
            else
            {
                
                if (ViewBag.CustomerInfo != null)
                {
                    receiverName = ViewBag.CustomerInfo.CustomerName;
                    receiverPhone = ViewBag.CustomerInfo.Phone;
                }
            }

            // Đẩy dữ liệu đã tách sạch sẽ sang View
            ViewBag.ReceiverName = receiverName;
            ViewBag.ReceiverPhone = receiverPhone;
            ViewBag.ActualAddress = actualAddress;
            

            // 3. Lấy danh sách món hàng trong đơn (OrderDetail)
            var listDetails = await SalesDataService.ListDetailsAsync(id);
            ViewBag.OrderDetails = listDetails;

            // 4. XỬ LÝ ẢNH VÀ TÊN: Lấy thông tin sản phẩm gốc từ CatalogDataService
            var productInfo = new Dictionary<int, (string Name, string Photo)>();
            foreach (var item in listDetails)
            {
                if (!productInfo.ContainsKey(item.ProductID))
                {
                    var p = await CatalogDataService.GetProductAsync(item.ProductID);
                    productInfo[item.ProductID] = (p?.ProductName ?? "Sản phẩm", p?.Photo ?? "nophoto.png");
                }
            }
            ViewBag.ProductInfo = productInfo;

            return View(order);
        }
    }
}