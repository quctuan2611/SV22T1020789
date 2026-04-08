using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Sales;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai các phép xử lý dữ liệu cho đơn hàng và chi tiết đơn hàng
    /// </summary>
    public class OrderRepository : BaseRepository, IOrderRepository
    {
        // Truyền chuỗi kết nối lên cho BaseRepository xử lý
        public OrderRepository(string connectionString) : base(connectionString)
        {
        }

        #region Xử lý thông tin Đơn hàng (Order)

        public async Task<int> AddAsync(Order data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO Orders (CustomerID, OrderTime, DeliveryProvince, DeliveryAddress, EmployeeID, Status)
                VALUES (@CustomerID, @OrderTime, @DeliveryProvince, @DeliveryAddress, @EmployeeID, @Status);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> DeleteAsync(int orderID)
        {
            using var connection = GetConnection();
            string sql = @"
                DELETE FROM OrderDetails WHERE OrderID = @OrderID;
                DELETE FROM Orders WHERE OrderID = @OrderID;";
            int rowsAffected = await connection.ExecuteAsync(sql, new { OrderID = orderID });
            return rowsAffected > 0;
        }

        public async Task<OrderViewInfo?> GetAsync(int orderID)
        {
            using var connection = GetConnection();
            string sql = @"
                SELECT o.*, c.CustomerName, c.ContactName as CustomerContactName, c.Phone as CustomerPhone, 
                       c.Email as CustomerEmail, c.Address as CustomerAddress, e.FullName as EmployeeName, 
                       s.ShipperName, s.Phone as ShipperPhone,
                       (SELECT ISNULL(SUM(Quantity * SalePrice), 0) FROM OrderDetails WHERE OrderID = o.OrderID) AS TotalMoney
                FROM Orders o
                LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                LEFT JOIN Shippers s ON o.ShipperID = s.ShipperID
                WHERE o.OrderID = @OrderID";
            return await connection.QueryFirstOrDefaultAsync<OrderViewInfo>(sql, new { OrderID = orderID });
        }

        public async Task<PagedResult<OrderViewInfo>> ListAsync(OrderSearchInput input)
        {
            var result = new PagedResult<OrderViewInfo> { Page = input.Page, PageSize = input.PageSize };
            string searchValue = string.IsNullOrEmpty(input.SearchValue) ? "" : $"%{input.SearchValue}%";
            using var connection = GetConnection();

            int statusFilter = (int)input.Status;

            string condition = @"
                (@SearchValue = N'' OR c.CustomerName LIKE @SearchValue OR e.FullName LIKE @SearchValue OR o.DeliveryProvince LIKE @SearchValue)
                AND (@Status = 0 OR o.Status = @Status)
                AND (@DateFrom IS NULL OR o.OrderTime >= @DateFrom)
                AND (@DateTo IS NULL OR o.OrderTime <= @DateTo)";

            string countSql = $@"
                SELECT COUNT(*) FROM Orders o
                LEFT JOIN Customers c ON o.CustomerID = c.CustomerID
                LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                WHERE {condition}";
            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new { SearchValue = searchValue, Status = statusFilter, DateFrom = input.DateFrom, DateTo = input.DateTo });

            if (result.RowCount > 0)
            {
                if (input.PageSize == 0)
                {
                    string queryAllSql = $@"
                        SELECT o.*, c.CustomerName, c.Phone as CustomerPhone, e.FullName as EmployeeName,
                               (SELECT ISNULL(SUM(Quantity * SalePrice), 0) FROM OrderDetails WHERE OrderID = o.OrderID) AS TotalMoney
                        FROM Orders o LEFT JOIN Customers c ON o.CustomerID = c.CustomerID LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                        WHERE {condition} ORDER BY o.OrderTime DESC";
                    var data = await connection.QueryAsync<OrderViewInfo>(queryAllSql, new { SearchValue = searchValue, Status = statusFilter, DateFrom = input.DateFrom, DateTo = input.DateTo });
                    result.DataItems = data.ToList();
                }
                else
                {
                    string queryPagedSql = $@"
                        SELECT o.*, c.CustomerName, c.Phone as CustomerPhone, e.FullName as EmployeeName,
                               (SELECT ISNULL(SUM(Quantity * SalePrice), 0) FROM OrderDetails WHERE OrderID = o.OrderID) AS TotalMoney
                        FROM Orders o LEFT JOIN Customers c ON o.CustomerID = c.CustomerID LEFT JOIN Employees e ON o.EmployeeID = e.EmployeeID
                        WHERE {condition} ORDER BY o.OrderTime DESC
                        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                    var data = await connection.QueryAsync<OrderViewInfo>(queryPagedSql, new { SearchValue = searchValue, Status = statusFilter, DateFrom = input.DateFrom, DateTo = input.DateTo, Offset = input.Offset, PageSize = input.PageSize });
                    result.DataItems = data.ToList();
                }
            }
            return result;
        }

        public async Task<bool> UpdateAsync(Order data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Orders
                SET CustomerID = @CustomerID, OrderTime = @OrderTime, DeliveryProvince = @DeliveryProvince,
                    DeliveryAddress = @DeliveryAddress, EmployeeID = @EmployeeID, AcceptTime = @AcceptTime,
                    ShipperID = @ShipperID, ShippedTime = @ShippedTime, FinishedTime = @FinishedTime, Status = @Status
                WHERE OrderID = @OrderID";
            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }

        #endregion

        #region Xử lý chi tiết Đơn hàng (OrderDetails)

        public async Task<bool> AddDetailAsync(OrderDetail data)
        {
            using var connection = GetConnection();
            string sql = "INSERT INTO OrderDetails (OrderID, ProductID, Quantity, SalePrice) VALUES (@OrderID, @ProductID, @Quantity, @SalePrice)";
            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteDetailAsync(int orderID, int productID)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM OrderDetails WHERE OrderID = @OrderID AND ProductID = @ProductID";
            int rowsAffected = await connection.ExecuteAsync(sql, new { OrderID = orderID, ProductID = productID });
            return rowsAffected > 0;
        }

        public async Task<OrderDetailViewInfo?> GetDetailAsync(int orderID, int productID)
        {
            using var connection = GetConnection();
            string sql = @"
                SELECT od.*, p.ProductName, p.Unit, p.Photo
                FROM OrderDetails od JOIN Products p ON od.ProductID = p.ProductID
                WHERE od.OrderID = @OrderID AND od.ProductID = @ProductID";
            return await connection.QueryFirstOrDefaultAsync<OrderDetailViewInfo>(sql, new { OrderID = orderID, ProductID = productID });
        }

        public async Task<List<OrderDetailViewInfo>> ListDetailsAsync(int orderID)
        {
            using var connection = GetConnection();
            string sql = @"
                SELECT od.*, p.ProductName, p.Unit, p.Photo
                FROM OrderDetails od JOIN Products p ON od.ProductID = p.ProductID
                WHERE od.OrderID = @OrderID";
            var data = await connection.QueryAsync<OrderDetailViewInfo>(sql, new { OrderID = orderID });
            return data.ToList();
        }

        public async Task<bool> UpdateDetailAsync(OrderDetail data)
        {
            using var connection = GetConnection();
            string sql = "UPDATE OrderDetails SET Quantity = @Quantity, SalePrice = @SalePrice WHERE OrderID = @OrderID AND ProductID = @ProductID";
            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }
        public async Task<bool> UpdateStatusAsync(int orderID, OrderStatusEnum status, int? shipperID, DateTime? deliveryDate, DateTime? finishedDate)
        {
            using var connection = GetConnection();
            string sql = @"UPDATE Orders 
                   SET Status = @Status, 
                       ShipperID = @ShipperID, 
                       ShippedTime = @ShippedTime, 
                       FinishedTime = @FinishedTime 
                   WHERE OrderID = @OrderID";

            var parameters = new
            {
                OrderID = orderID,
                Status = (int)status,
                ShipperID = shipperID,
                ShippedTime = deliveryDate, // Tương ứng với ShippedTime trong DB
                FinishedTime = finishedDate
            };

            int rowsAffected = await connection.ExecuteAsync(sql, parameters);
            return rowsAffected > 0;
        }
        #endregion
    }
}