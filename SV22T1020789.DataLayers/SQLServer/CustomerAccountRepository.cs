using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Security;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Xử lý tài khoản của khách hàng
    /// </summary>
    public class CustomerAccountRepository : BaseRepository, IUserAccountRepository
    {
        public CustomerAccountRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<UserAccount?> AuthorizeAsync(string userName, string password)
        {
            using var connection = GetConnection();

            // Đã cập nhật câu lệnh SQL: Thêm IsLocked vào SELECT và bỏ check IsLocked ở WHERE
            string sql = @"
                SELECT 
                    CAST(CustomerID AS VARCHAR) AS UserId,
                    Email AS UserName,
                    CustomerName AS DisplayName,
                    Email,
                    '' AS Photo,
                    'Customer' AS RoleNames,
                    CAST(ISNULL(IsLocked, 0) AS BIT) AS IsLocked
                FROM Customers 
                WHERE Email = @Email AND Password = @Password";

            // Khách hàng sử dụng Email làm userName đăng nhập
            var parameters = new { Email = userName, Password = password };

            return await connection.QueryFirstOrDefaultAsync<UserAccount>(sql, parameters);
        }

        public async Task<bool> ChangePasswordAsync(string userName, string password)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Customers 
                SET Password = @Password 
                WHERE Email = @Email";

            var parameters = new { Email = userName, Password = password };
            int rowsAffected = await connection.ExecuteAsync(sql, parameters);

            return rowsAffected > 0;
        }
    }
}