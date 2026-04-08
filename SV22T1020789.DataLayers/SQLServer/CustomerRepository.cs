using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Partner;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai các phép xử lý dữ liệu cho Entity Customer trên SQL Server sử dụng Dapper
    /// </summary>
    public class CustomerRepository : BaseRepository, ICustomerRepository
    {
        public CustomerRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<int> AddAsync(Customer data)
        {
            using var connection = GetConnection();

            // SỬA LỖI TẠI ĐÂY: Đã thêm cột Password và tham số @Password vào câu lệnh INSERT
            string sql = @"
                INSERT INTO Customers (CustomerName, ContactName, Province, Address, Phone, Email, Password, IsLocked)
                VALUES (@CustomerName, @ContactName, @Province, @Address, @Phone, @Email, @Password, @IsLocked);
                
                -- Lấy mã (ID) vừa được tự sinh (IDENTITY)
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM Customers WHERE CustomerID = @CustomerID";

            int rowsAffected = await connection.ExecuteAsync(sql, new { CustomerID = id });
            return rowsAffected > 0;
        }

        public async Task<Customer?> GetAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM Customers WHERE CustomerID = @CustomerID";

            return await connection.QueryFirstOrDefaultAsync<Customer>(sql, new { CustomerID = id });
        }

        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = GetConnection();

            // Giả định bảng Orders lưu thông tin đơn hàng của khách
            string sql = @"
                IF EXISTS(SELECT 1 FROM Orders WHERE CustomerID = @CustomerID)
                    SELECT 1;
                ELSE 
                    SELECT 0;";

            return await connection.ExecuteScalarAsync<bool>(sql, new { CustomerID = id });
        }

        public async Task<PagedResult<Customer>> ListAsync(PaginationSearchInput input)
        {
            var result = new PagedResult<Customer>
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            // Từ khóa tìm kiếm (tìm tương đối)
            string searchValue = string.IsNullOrEmpty(input.SearchValue) ? "" : $"%{input.SearchValue}%";

            using var connection = GetConnection();

            // 1. Đếm tổng số dòng dữ liệu thỏa mãn điều kiện
            string countSql = @"
                SELECT COUNT(*)
                FROM Customers
                WHERE (@SearchValue = N'') 
                   OR (CustomerName LIKE @SearchValue) 
                   OR (ContactName LIKE @SearchValue)
                   OR (Phone LIKE @SearchValue)";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new { SearchValue = searchValue });

            // 2. Lấy danh sách phân trang (nếu có dữ liệu)
            if (result.RowCount > 0)
            {
                if (input.PageSize == 0)
                {
                    // Lấy tất cả
                    string queryAllSql = @"
                        SELECT *
                        FROM Customers
                        WHERE (@SearchValue = N'') 
                           OR (CustomerName LIKE @SearchValue) 
                           OR (ContactName LIKE @SearchValue)
                           OR (Phone LIKE @SearchValue)
                        ORDER BY CustomerName";

                    var data = await connection.QueryAsync<Customer>(queryAllSql, new { SearchValue = searchValue });
                    result.DataItems = data.ToList();
                }
                else
                {
                    // Lấy phân trang
                    string queryPagedSql = @"
                        SELECT *
                        FROM Customers
                        WHERE (@SearchValue = N'') 
                           OR (CustomerName LIKE @SearchValue) 
                           OR (ContactName LIKE @SearchValue)
                           OR (Phone LIKE @SearchValue)
                        ORDER BY CustomerName
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    var data = await connection.QueryAsync<Customer>(queryPagedSql, new
                    {
                        SearchValue = searchValue,
                        Offset = input.Offset,
                        PageSize = input.PageSize
                    });

                    result.DataItems = data.ToList();
                }
            }

            return result;
        }

        public async Task<bool> UpdateAsync(Customer data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Customers
                SET CustomerName = @CustomerName,
                    ContactName = @ContactName,
                    Province = @Province,
                    Address = @Address,
                    Phone = @Phone,
                    Email = @Email,
                    IsLocked = @IsLocked
                WHERE CustomerID = @CustomerID";

            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }

        public async Task<bool> ValidateEmailAsync(string email, int id = 0)
        {
            using var connection = GetConnection();

            // Nếu id = 0 (khách hàng mới), kiểm tra email có trong bảng hay chưa.
            // Nếu id != 0 (cập nhật khách hàng), kiểm tra email có bị trùng với khách hàng KHÁC (khác id hiện tại) hay không.
            string sql = @"
                IF EXISTS (
                    SELECT 1 
                    FROM Customers 
                    WHERE Email = @Email AND (@CustomerID = 0 OR CustomerID <> @CustomerID)
                )
                    SELECT 1;
                ELSE
                    SELECT 0;";

            // Trả về true nếu email hợp lệ (KHÔNG BỊ TRÙNG)
            // Lệnh SQL trả về 1 nếu TỒN TẠI (bị trùng). Do đó ta so sánh kết quả == false (tức là không tồn tại)
            bool isExists = await connection.ExecuteScalarAsync<bool>(sql, new { Email = email, CustomerID = id });

            return !isExists;
        }
    }
}