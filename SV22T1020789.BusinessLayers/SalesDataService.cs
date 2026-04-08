using SV22T1020789.BusinessLayers;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.DataLayers.SQLServer;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Sales;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SV22T1020789.BusinessLayers
{
    /// <summary>
    /// Cung cấp các chức năng xử lý dữ liệu liên quan đến bán hàng
    /// bao gồm: đơn hàng (Order) và chi tiết đơn hàng (OrderDetail).
    /// </summary>
    public static class SalesDataService
    {
        /// <summary>
        /// Khởi tạo OrderRepository mỗi lần gọi để đảm bảo an toàn luồng (Thread-Safe).
        /// Tránh lỗi nhiều request dùng chung một Connection.
        /// </summary>
        private static IOrderRepository OrderDB => new OrderRepository(Configuration.ConnectionString);

        #region Order

        /// <summary>
        /// Tìm kiếm và lấy danh sách đơn hàng dưới dạng phân trang
        /// </summary>
        public static async Task<PagedResult<OrderViewInfo>> ListOrdersAsync(OrderSearchInput input)
        {
            
            if (input.DateTo.HasValue)
            {
                
                input.DateTo = input.DateTo.Value.Date.AddDays(1).AddSeconds(-1);
            }
            

            return await OrderDB.ListAsync(input);
        }

        /// <summary>
        /// Lấy thông tin chi tiết của một đơn hàng
        /// </summary>
        public static async Task<OrderViewInfo?> GetOrderAsync(int orderID)
        {
            return await OrderDB.GetAsync(orderID);
        }

        /// <summary>
        /// Bổ sung đơn hàng (bug chuẩn)
        /// </summary>
        public static async Task<int> AddOrderAsync(int customerID,
                                                    string deliveryProvince,
                                                    string deliveryAddress)
        {
            var order = new Order()
            {
                CustomerID = customerID == 0 ? null : customerID,
                DeliveryProvince = deliveryProvince,
                DeliveryAddress = deliveryAddress,
                Status = OrderStatusEnum.New,
                OrderTime = DateTime.Now,
            };

            return await OrderDB.AddAsync(order);
        }

        /// <summary>
        /// Cập nhật thông tin đơn hàng
        /// </summary>
        public static async Task<bool> UpdateOrderAsync(Order data)
        {
            return await OrderDB.UpdateAsync(data);
        }

        /// <summary>
        /// Xóa đơn hàng
        /// </summary>
        public static async Task<bool> DeleteOrderAsync(int orderID)
        {
            return await OrderDB.DeleteAsync(orderID);
        }

        #endregion

        #region Order Status Processing

        /// <summary>
        /// Duyệt đơn hàng
        /// </summary>
        public static async Task<bool> AcceptOrderAsync(int orderID, int employeeID)
        {
            var order = await OrderDB.GetAsync(orderID);
            if (order == null || order.Status != OrderStatusEnum.New)
                return false;

            order.EmployeeID = employeeID;
            order.AcceptTime = DateTime.Now;
            order.Status = OrderStatusEnum.Accepted;

            return await OrderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Từ chối đơn hàng
        /// </summary>
        public static async Task<bool> RejectOrderAsync(int orderID, int employeeID)
        {
            var order = await OrderDB.GetAsync(orderID);
            if (order == null || order.Status != OrderStatusEnum.New)
                return false;

            order.EmployeeID = employeeID;
            order.FinishedTime = DateTime.Now;
            order.Status = OrderStatusEnum.Rejected;

            return await OrderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Hủy đơn hàng
        /// </summary>
        public static async Task<bool> CancelOrderAsync(int orderID)
        {
            var order = await OrderDB.GetAsync(orderID);
            if (order == null)
                return false;

            if (order.Status != OrderStatusEnum.New && order.Status != OrderStatusEnum.Accepted)
                return false;

            order.FinishedTime = DateTime.Now;
            order.Status = OrderStatusEnum.Cancelled;

            return await OrderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Giao đơn hàng cho người giao hàng
        /// </summary>
        public static async Task<bool> ShipOrderAsync(int orderID, int shipperID)
        {
            var order = await OrderDB.GetAsync(orderID);
            if (order == null || order.Status != OrderStatusEnum.Accepted)
                return false;

            order.ShipperID = shipperID;
            order.ShippedTime = DateTime.Now;
            order.Status = OrderStatusEnum.Shipping;

            return await OrderDB.UpdateAsync(order);
        }

        /// <summary>
        /// Hoàn tất đơn hàng
        /// </summary>
        public static async Task<bool> CompleteOrderAsync(int orderID)
        {
            var order = await OrderDB.GetAsync(orderID);
            if (order == null || order.Status != OrderStatusEnum.Shipping)
                return false;

            order.FinishedTime = DateTime.Now;
            order.Status = OrderStatusEnum.Completed;

            return await OrderDB.UpdateAsync(order);
        }

        #endregion

        #region Order Detail

        /// <summary>
        /// Lấy danh sách mặt hàng của đơn hàng
        /// </summary>
        public static async Task<List<OrderDetailViewInfo>> ListDetailsAsync(int orderID)
        {
            return await OrderDB.ListDetailsAsync(orderID);
        }

        /// <summary>
        /// Lấy thông tin một mặt hàng trong đơn hàng
        /// </summary>
        public static async Task<OrderDetailViewInfo?> GetDetailAsync(int orderID, int productID)
        {
            return await OrderDB.GetDetailAsync(orderID, productID);
        }

        /// <summary>
        /// Thêm mặt hàng vào đơn hàng
        /// </summary>
        public static async Task<bool> AddDetailAsync(OrderDetail data)
        {
            // Bắt validation dữ liệu cơ bản
            if (data.Quantity <= 0 || data.SalePrice < 0) return false;
            return await OrderDB.AddDetailAsync(data);
        }

        /// <summary>
        /// Cập nhật mặt hàng trong đơn hàng
        /// </summary>
        public static async Task<bool> UpdateDetailAsync(OrderDetail data)
        {
            // Bắt validation dữ liệu cơ bản
            if (data.Quantity <= 0 || data.SalePrice < 0) return false;
            return await OrderDB.UpdateDetailAsync(data);
        }

        /// <summary>
        /// Xóa mặt hàng khỏi đơn hàng
        /// </summary>
        public static async Task<bool> DeleteDetailAsync(int orderID, int productID)
        {
            return await OrderDB.DeleteDetailAsync(orderID, productID);
        }

        /// <summary>
        /// Lưu chi tiết đơn hàng 
        /// </summary>
        public static async Task<bool> SaveOrderDetailAsync(int orderID, int productID, int quantity, decimal salePrice)
        {
            // Bắt lỗi dữ liệu đầu vào cơ bản để tránh user truyền số âm
            if (quantity <= 0 || salePrice < 0) return false;

            // Kiểm tra xem mặt hàng đã tồn tại trong đơn chưa
            var detail = await GetDetailAsync(orderID, productID);

            if (detail == null)
            {
                // Nếu chưa có, tiến hành thêm mới
                return await AddDetailAsync(new OrderDetail()
                {
                    OrderID = orderID,
                    ProductID = productID,
                    Quantity = quantity,
                    SalePrice = salePrice
                });
            }
            else
            {
                return await UpdateDetailAsync(new OrderDetail()
                {
                    OrderID = orderID,
                    ProductID = productID,
                    Quantity = quantity,
                    SalePrice = salePrice
                });
            }
        }
        /// <summary>
        /// Xóa chi tiết đơn hàng 
        /// </summary>
        public static async Task<bool> DeleteOrderDetailAsync(int orderID, int productID)
        {
            return await DeleteDetailAsync(orderID, productID);
        }
        #endregion
    }
}