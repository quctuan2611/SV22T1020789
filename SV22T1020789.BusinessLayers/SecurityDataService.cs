using SV22T1020789.DataLayers.SQLServer;
using SV22T1020789.Models.Security;
using System.Threading.Tasks;

namespace SV22T1020789.BusinessLayers
{
    /// <summary>
    /// Các dịch vụ liên quan đến bảo mật và tài khoản (Nhân viên, Khách hàng)
    /// </summary>
    public static class SecurityDataService
    {
        private static UserAccountRepository employeeAccountDB = null!;
        private static CustomerAccountRepository customerAccountDB = null!;

        /// <summary>
        /// Khởi tạo dịch vụ (Cần gọi hàm này tại Startup/Program trước khi sử dụng các dịch vụ khác)
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối CSDL</param>
        public static void Init(string connectionString)
        {
            // Khởi tạo cho Nhân viên (Sử dụng ADO.NET truyền thống)
            employeeAccountDB = new UserAccountRepository(connectionString);

            // Khởi tạo cho Khách hàng (Sử dụng Dapper - Bất đồng bộ)
            customerAccountDB = new CustomerAccountRepository(connectionString);
        }

        // ================= ĐỐI VỚI NHÂN VIÊN (DÀNH CHO ADMIN) =================

        /// <summary>
        /// Kiểm tra đăng nhập cho nhân viên
        /// </summary>
        /// <param name="userName">Email của nhân viên</param>
        /// <param name="password">Mật khẩu (đã hash MD5)</param>
        /// <returns>Thông tin tài khoản hoặc null nếu sai thông tin</returns>
        public static async Task<UserAccount?> EmployeeAuthorizeAsync(string userName, string password)
        {
            return await Task.Run(() => employeeAccountDB.Authorize(userName, password));
        }

        /// <summary>
        /// Nhân viên tự đổi mật khẩu (yêu cầu mật khẩu cũ)
        /// </summary>
        /// <param name="userName">Email nhân viên</param>
        /// <param name="oldPassword">Mật khẩu cũ</param>
        /// <param name="newPassword">Mật khẩu mới</param>
        /// <returns>True nếu đổi thành công</returns>
        public static bool ChangePassword(string userName, string oldPassword, string newPassword)
        {
            return employeeAccountDB.ChangePassword(userName, oldPassword, newPassword);
        }

        /// <summary>
        /// Admin đặt lại mật khẩu cho nhân viên (không cần mật khẩu cũ)
        /// </summary>
        /// <param name="employeeId">ID của nhân viên</param>
        /// <param name="newPassword">Mật khẩu mới (đã hash)</param>
        /// <returns>True nếu đặt lại thành công</returns>
        public static bool ResetPassword(string employeeId, string newPassword)
        {
            return employeeAccountDB.ResetPassword(employeeId, newPassword);
        }

        // ================= ĐỐI VỚI KHÁCH HÀNG (DÀNH CHO SHOP) =================

        /// <summary>
        /// Kiểm tra đăng nhập cho Khách hàng
        /// </summary>
        /// <param name="userName">Email khách hàng</param>
        /// <param name="password">Mật khẩu (đã hash MD5)</param>
        /// <returns>Thông tin tài khoản khách hàng hoặc null</returns>
        public static async Task<UserAccount?> CustomerAuthorizeAsync(string userName, string password)
        {
            return await customerAccountDB.AuthorizeAsync(userName, password);
        }

        /// <summary>
        /// Admin đặt lại mật khẩu cho khách hàng thông qua Email
        /// </summary>
        /// <param name="email">Email của khách hàng</param>
        /// <param name="newPassword">Mật khẩu mới (đã hash)</param>
        /// <returns>True nếu cập nhật thành công</returns>
        public static async Task<bool> ResetCustomerPasswordAsync(string email, string newPassword)
        {
            return await customerAccountDB.ChangePasswordAsync(email, newPassword);
        }
    }
}