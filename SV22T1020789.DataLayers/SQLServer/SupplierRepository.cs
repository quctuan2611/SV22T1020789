using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Partner;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai các phép xử lý dữ liệu cho Entity Supplier trên SQL Server sử dụng Dapper
    /// </summary>
    public class SupplierRepository : BaseRepository, IGenericRepository<Supplier>
    {
        /// <summary>
        /// Constructor truyền chuỗi kết nối lên cho BaseRepository
        /// </summary>
        public SupplierRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<int> AddAsync(Supplier data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO Suppliers (SupplierName, ContactName, Province, Address, Phone, Email)
                VALUES (@SupplierName, @ContactName, @Province, @Address, @Phone, @Email);
                
                -- Lấy mã (ID) vừa được tự sinh (IDENTITY)
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            // Thực thi và trả về ID
            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM Suppliers WHERE SupplierID = @SupplierID";

            int rowsAffected = await connection.ExecuteAsync(sql, new { SupplierID = id });
            return rowsAffected > 0;
        }

        public async Task<Supplier?> GetAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM Suppliers WHERE SupplierID = @SupplierID";

            return await connection.QueryFirstOrDefaultAsync<Supplier>(sql, new { SupplierID = id });
        }

        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = GetConnection();

            // LƯU Ý: Đổi tên bảng 'Products' thành bảng thực tế có khóa ngoại trỏ tới Suppliers
            string sql = @"
                IF EXISTS(SELECT 1 FROM Products WHERE SupplierID = @SupplierID)
                    SELECT 1;
                ELSE 
                    SELECT 0;";

            return await connection.ExecuteScalarAsync<bool>(sql, new { SupplierID = id });
        }

        public async Task<PagedResult<Supplier>> ListAsync(PaginationSearchInput input)
        {
            var result = new PagedResult<Supplier>
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            // Từ khóa tìm kiếm (tìm tương đối)
            string searchValue = $"%{input.SearchValue}%";

            using var connection = GetConnection();

            // 1. Đếm tổng số dòng dữ liệu thỏa mãn điều kiện
            string countSql = @"
                SELECT COUNT(*)
                FROM Suppliers
                WHERE (@SearchValue = N'%%') 
                   OR (SupplierName LIKE @SearchValue) 
                   OR (ContactName LIKE @SearchValue)";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new { SearchValue = searchValue });

            // 2. Lấy danh sách phân trang (nếu có dữ liệu)
            if (result.RowCount > 0)
            {
                if (input.PageSize == 0)
                {
                    // Trường hợp không phân trang (lấy tất cả)
                    string queryAllSql = @"
                        SELECT *
                        FROM Suppliers
                        WHERE (@SearchValue = N'%%') 
                           OR (SupplierName LIKE @SearchValue) 
                           OR (ContactName LIKE @SearchValue)
                        ORDER BY SupplierName";

                    var data = await connection.QueryAsync<Supplier>(queryAllSql, new { SearchValue = searchValue });
                    result.DataItems = data.ToList();
                }
                else
                {
                    // Trường hợp có phân trang
                    string queryPagedSql = @"
                        SELECT *
                        FROM Suppliers
                        WHERE (@SearchValue = N'%%') 
                           OR (SupplierName LIKE @SearchValue) 
                           OR (ContactName LIKE @SearchValue)
                        ORDER BY SupplierName
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    var data = await connection.QueryAsync<Supplier>(queryPagedSql, new
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

        public async Task<bool> UpdateAsync(Supplier data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Suppliers
                SET SupplierName = @SupplierName,
                    ContactName = @ContactName,
                    Province = @Province,
                    Address = @Address,
                    Phone = @Phone,
                    Email = @Email
                WHERE SupplierID = @SupplierID";

            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }
    }
}