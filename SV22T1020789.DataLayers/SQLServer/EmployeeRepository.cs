using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Common;
using SV22T1020789.Models.HR;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai các phép xử lý dữ liệu cho Entity Employee trên SQL Server sử dụng Dapper
    /// </summary>
    public class EmployeeRepository : BaseRepository, IEmployeeRepository
    {
        public EmployeeRepository(string connectionString) : base(connectionString)
        {
        }

        /// <summary>
        /// Thêm mới nhân viên (Đã bao gồm RoleNames)
        /// </summary>
        public async Task<int> AddAsync(Employee data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO Employees (FullName, BirthDate, Address, Phone, Email, Photo, IsWorking, RoleNames)
                VALUES (@FullName, @BirthDate, @Address, @Phone, @Email, @Photo, @IsWorking, @RoleNames);
                
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        /// <summary>
        /// Xóa nhân viên
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM Employees WHERE EmployeeID = @EmployeeID";

            int rowsAffected = await connection.ExecuteAsync(sql, new { EmployeeID = id });
            return rowsAffected > 0;
        }

        /// <summary>
        /// Lấy chi tiết nhân viên (SELECT * sẽ tự lấy RoleNames)
        /// </summary>
        public async Task<Employee?> GetAsync(int id)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM Employees WHERE EmployeeID = @EmployeeID";

            return await connection.QueryFirstOrDefaultAsync<Employee>(sql, new { EmployeeID = id });
        }

        /// <summary>
        /// Kiểm tra nhân viên có đang được dùng không
        /// </summary>
        public async Task<bool> IsUsedAsync(int id)
        {
            using var connection = GetConnection();
            string sql = @"
                IF EXISTS(SELECT 1 FROM Orders WHERE EmployeeID = @EmployeeID)
                    SELECT 1;
                ELSE 
                    SELECT 0;";

            return await connection.ExecuteScalarAsync<bool>(sql, new { EmployeeID = id });
        }

        /// <summary>
        /// Danh sách nhân viên phân trang
        /// </summary>
        public async Task<PagedResult<Employee>> ListAsync(PaginationSearchInput input)
        {
            var result = new PagedResult<Employee>
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string searchValue = string.IsNullOrEmpty(input.SearchValue) ? "" : $"%{input.SearchValue}%";

            using var connection = GetConnection();

            string countSql = @"
                SELECT COUNT(*)
                FROM Employees
                WHERE (@SearchValue = N'') 
                   OR (FullName LIKE @SearchValue) 
                   OR (Phone LIKE @SearchValue)
                   OR (Email LIKE @SearchValue)";

            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new { SearchValue = searchValue });

            if (result.RowCount > 0)
            {
                string querySql = @"
                    SELECT *
                    FROM Employees
                    WHERE (@SearchValue = N'') 
                       OR (FullName LIKE @SearchValue) 
                       OR (Phone LIKE @SearchValue)
                       OR (Email LIKE @SearchValue)
                    ORDER BY FullName";

                if (input.PageSize > 0)
                {
                    querySql += @"
                        OFFSET @Offset ROWS
                        FETCH NEXT @PageSize ROWS ONLY";
                }

                var data = await connection.QueryAsync<Employee>(querySql, new
                {
                    SearchValue = searchValue,
                    Offset = input.Offset,
                    PageSize = input.PageSize
                });

                result.DataItems = data.ToList();
            }

            return result;
        }

        /// <summary>
        /// Cập nhật nhân viên (ĐÃ THÊM ROLENAMES - CHỖ NÀY LÀ THEN CHỐT)
        /// </summary>
        public async Task<bool> UpdateAsync(Employee data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Employees
                SET FullName = @FullName,
                    BirthDate = @BirthDate,
                    Address = @Address,
                    Phone = @Phone,
                    Email = @Email,
                    Photo = @Photo,
                    IsWorking = @IsWorking,
                    RoleNames = @RoleNames -- Lưu chuỗi quyền xuống Database ở đây
                WHERE EmployeeID = @EmployeeID";

            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }

        /// <summary>
        /// Kiểm tra trùng Email
        /// </summary>
        public async Task<bool> ValidateEmailAsync(string email, int id = 0)
        {
            using var connection = GetConnection();
            string sql = @"
                IF EXISTS (
                    SELECT 1 
                    FROM Employees 
                    WHERE Email = @Email AND (@EmployeeID = 0 OR EmployeeID <> @EmployeeID)
                )
                    SELECT 1;
                ELSE
                    SELECT 0;";

            bool isExists = await connection.ExecuteScalarAsync<bool>(sql, new { Email = email, EmployeeID = id });
            return !isExists;
        }
    }
}