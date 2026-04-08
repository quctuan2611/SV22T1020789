using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Partner;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai các phép xử lý dữ liệu cho Entity Shipper trên SQL Server sử dụng Dapper
    /// </summary>
    public class ShipperRepository : BaseRepository, IGenericRepository<Shipper>
    {
        public ShipperRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<int> AddAsync(Shipper data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO Shippers (ShipperName, Phone)
                VALUES (@ShipperName, @Phone);
                
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM Shippers WHERE ShipperID = @ShipperID";

            int rowsAffected = await connection.ExecuteAsync(sql, new { ShipperID = id });
            return rowsAffected > 0;
        }

        public async Task<Shipper?> GetAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM Shippers WHERE ShipperID = @ShipperID";

            return await connection.QueryFirstOrDefaultAsync<Shipper>(sql, new { ShipperID = id });
        }

        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = GetConnection();

            // LƯU Ý: Giả định Shipper có khóa ngoại nằm trong bảng Orders
            string sql = @"
                IF EXISTS(SELECT 1 FROM Orders WHERE ShipperID = @ShipperID)
                    SELECT 1;
                ELSE 
                    SELECT 0;";

            return await connection.ExecuteScalarAsync<bool>(sql, new { ShipperID = id });
        }

        public async Task<PagedResult<Shipper>> ListAsync(PaginationSearchInput input)
        {
            var result = new PagedResult<Shipper>
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            // Tạo chuỗi tìm kiếm tương đối (LIKE)
            string searchValue = string.IsNullOrEmpty(input.SearchValue) ? "" : $"%{input.SearchValue}%";

            using var connection = GetConnection();

            // 1. Đếm tổng số dòng
            string countSql = @"
                SELECT COUNT(*)
                FROM Shippers
                WHERE (@SearchValue = N'') 
                   OR (ShipperName LIKE @SearchValue) 
                   OR (Phone LIKE @SearchValue)";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new { SearchValue = searchValue });

            // 2. Lấy dữ liệu phân trang
            if (result.RowCount > 0)
            {
                if (input.PageSize == 0)
                {
                    // Lấy tất cả (không phân trang)
                    string queryAllSql = @"
                        SELECT *
                        FROM Shippers
                        WHERE (@SearchValue = N'') 
                           OR (ShipperName LIKE @SearchValue) 
                           OR (Phone LIKE @SearchValue)
                        ORDER BY ShipperName";

                    var data = await connection.QueryAsync<Shipper>(queryAllSql, new { SearchValue = searchValue });
                    result.DataItems = data.ToList();
                }
                else
                {
                    // Lấy theo trang
                    string queryPagedSql = @"
                        SELECT *
                        FROM Shippers
                        WHERE (@SearchValue = N'') 
                           OR (ShipperName LIKE @SearchValue) 
                           OR (Phone LIKE @SearchValue)
                        ORDER BY ShipperName
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    var data = await connection.QueryAsync<Shipper>(queryPagedSql, new
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

        public async Task<bool> UpdateAsync(Shipper data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Shippers
                SET ShipperName = @ShipperName,
                    Phone = @Phone
                WHERE ShipperID = @ShipperID";

            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }
    }
}