using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.Catalog;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai các phép xử lý dữ liệu cho Entity Category trên SQL Server sử dụng Dapper
    /// </summary>
    public class CategoryRepository : BaseRepository, IGenericRepository<Category>
    {
        /// <summary>
        /// Constructor truyền chuỗi kết nối lên cho BaseRepository
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối đến CSDL</param>
        public CategoryRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<int> AddAsync(Category data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO Categories (CategoryName, Description)
                VALUES (@CategoryName, @Description);
                
                -- Lấy mã (ID) vừa được tự sinh (IDENTITY)
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM Categories WHERE CategoryID = @CategoryID";

            int rowsAffected = await connection.ExecuteAsync(sql, new { CategoryID = id });
            return rowsAffected > 0;
        }

        public async Task<Category?> GetAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM Categories WHERE CategoryID = @CategoryID";

            return await connection.QueryFirstOrDefaultAsync<Category>(sql, new { CategoryID = id });
        }

        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = GetConnection();

            // LƯU Ý: Đổi tên bảng 'Products' thành bảng thực tế có khóa ngoại trỏ tới Categories nếu cần
            string sql = @"
                IF EXISTS(SELECT 1 FROM Products WHERE CategoryID = @CategoryID)
                    SELECT 1;
                ELSE 
                    SELECT 0;";

            return await connection.ExecuteScalarAsync<bool>(sql, new { CategoryID = id });
        }

        public async Task<PagedResult<Category>> ListAsync(PaginationSearchInput input)
        {
            var result = new PagedResult<Category>
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
                FROM Categories
                WHERE (@SearchValue = N'') 
                   OR (CategoryName LIKE @SearchValue) 
                   OR (Description LIKE @SearchValue)";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new { SearchValue = searchValue });

            // 2. Lấy danh sách phân trang (nếu có dữ liệu)
            if (result.RowCount > 0)
            {
                if (input.PageSize == 0)
                {
                    // Trường hợp không phân trang (lấy tất cả)
                    string queryAllSql = @"
                        SELECT *
                        FROM Categories
                        WHERE (@SearchValue = N'') 
                           OR (CategoryName LIKE @SearchValue) 
                           OR (Description LIKE @SearchValue)
                        ORDER BY CategoryName";

                    var data = await connection.QueryAsync<Category>(queryAllSql, new { SearchValue = searchValue });
                    result.DataItems = data.ToList();
                }
                else
                {
                    // Trường hợp có phân trang
                    string queryPagedSql = @"
                        SELECT *
                        FROM Categories
                        WHERE (@SearchValue = N'') 
                           OR (CategoryName LIKE @SearchValue) 
                           OR (Description LIKE @SearchValue)
                        ORDER BY CategoryName
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    var data = await connection.QueryAsync<Category>(queryPagedSql, new
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

        public async Task<bool> UpdateAsync(Category data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Categories
                SET CategoryName = @CategoryName,
                    Description = @Description
                WHERE CategoryID = @CategoryID";

            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }
    }
}