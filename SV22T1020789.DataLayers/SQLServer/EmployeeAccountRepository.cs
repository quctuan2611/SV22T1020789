using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Security;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Xử lý tài khoản của nhân viên
    /// </summary>
    public class EmployeeAccountRepository : BaseRepository, IUserAccountRepository
    {
        public EmployeeAccountRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<UserAccount?> AuthorizeAsync(string userName, string password)
        {
            using var connection = GetConnection();
            string sql = @"
                SELECT 
                    CAST(EmployeeID AS VARCHAR) AS UserId,
                    Email AS UserName,
                    FullName AS DisplayName,
                    Email,
                    Photo,
                    'Employee' AS RoleNames
                FROM Employees 
                WHERE Email = @Email AND Password = @Password AND IsWorking = 1";

            // Ở đây userName chính là Email của nhân viên
            var parameters = new { Email = userName, Password = password };

            return await connection.QueryFirstOrDefaultAsync<UserAccount>(sql, parameters);
        }

        public async Task<bool> ChangePasswordAsync(string userName, string password)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Employees 
                SET Password = @Password 
                WHERE Email = @Email";

            var parameters = new { Email = userName, Password = password };
            int rowsAffected = await connection.ExecuteAsync(sql, parameters);

            return rowsAffected > 0;
        }
    }
}