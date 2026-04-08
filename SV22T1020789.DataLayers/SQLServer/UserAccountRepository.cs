using SV22T1020789.Models.Security;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Xử lý dữ liệu liên quan đến tài khoản nhân viên trên SQL Server
    /// </summary>
    public class UserAccountRepository : BaseRepository // Kế thừa từ BaseRepository của bạn
    {
        public UserAccountRepository(string connectionString) : base(connectionString)
        {
        }

        /// <summary>
        /// Kiểm tra thông tin đăng nhập và trả về đối tượng UserAccount
        /// </summary>
        public UserAccount? Authorize(string userName, string password)
        {
            UserAccount? data = null;
            using (var connection = GetConnection()) // Dùng hàm GetConnection() từ BaseRepository
            {
                connection.Open(); // Mở kết nối
                var command = connection.CreateCommand();
                command.CommandText = @"SELECT EmployeeID, Email, FullName, Photo, RoleNames 
                                        FROM Employees 
                                        WHERE Email = @Email AND Password = @Password";
                command.CommandType = CommandType.Text;
                command.Parameters.AddWithValue("@Email", userName);
                command.Parameters.AddWithValue("@Password", password);

                using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    if (reader.Read())
                    {
                        data = new UserAccount()
                        {
                            UserId = reader["EmployeeID"].ToString() ?? "",
                            UserName = reader["Email"].ToString() ?? "",
                            DisplayName = reader["FullName"].ToString() ?? "",
                            Photo = reader["Photo"].ToString() ?? "nophoto.png",
                            RoleNames = reader["RoleNames"].ToString() ?? ""
                        };
                    }
                }
                connection.Close();
            }
            return data;
        }

        /// <summary>
        /// Thực hiện đổi mật khẩu cho tài khoản (Cần mật khẩu cũ - Dành cho User tự đổi)
        /// </summary>
        public bool ChangePassword(string userName, string oldPassword, string newPassword)
        {
            bool result = false;
            using (var connection = GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"UPDATE Employees 
                                        SET Password = @NewPassword 
                                        WHERE Email = @Email AND Password = @OldPassword";
                command.Parameters.AddWithValue("@Email", userName);
                command.Parameters.AddWithValue("@OldPassword", oldPassword);
                command.Parameters.AddWithValue("@NewPassword", newPassword);

                result = command.ExecuteNonQuery() > 0;
                connection.Close();
            }
            return result;
        }

        /// <summary>
        /// Admin đặt lại mật khẩu cho nhân viên (Không cần mật khẩu cũ - Dựa vào EmployeeID)
        /// </summary>
        public bool ResetPassword(string employeeId, string newPassword)
        {
            bool result = false;
            using (var connection = GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"UPDATE Employees 
                                        SET Password = @NewPassword 
                                        WHERE EmployeeID = @EmployeeID";
                command.Parameters.AddWithValue("@EmployeeID", employeeId);
                command.Parameters.AddWithValue("@NewPassword", newPassword);

                result = command.ExecuteNonQuery() > 0;
                connection.Close();
            }
            return result;
        }
    }
}