using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV22T1020789.BusinessLayers
{
    /// <summary>
    /// Lớp lưu giữ các thông tin cấu hình sử dụng trong BusinessLayer
    /// </summary>
    public static class Configuration
    {
        private static string _connectionString = "";

        /// <summary>
        /// Khởi tạo các cấu hình cho BusinessLayer
        /// (hàm này được gọi trước khi chạy ứng dụng)
        /// </summary>
        /// <param name="connectionString"></param>
        public static void Initialize(string connectionString)
        {
            _connectionString = connectionString;

            // Bổ sung dòng này để kích hoạt SecurityDataService
            SecurityDataService.Init(connectionString);
        }

        /// <summary>
        /// Lấy chuỗi tham số kết nối đến CSDL
        /// </summary>
        public static string ConnectionString => _connectionString;
    }
}