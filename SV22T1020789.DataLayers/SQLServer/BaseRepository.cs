using Microsoft.Data.SqlClient;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Lớp cơ sở (lớp cha) chứa các thành phần dùng chung cho các file Repository 
    /// thao tác với CSDL SQL Server
    /// </summary>
    public abstract class BaseRepository
    {
        /// <summary>
        /// Chuỗi kết nối đến CSDL
        /// </summary>
        protected string _connectionString = "";

        /// <summary>
        /// Constructor: Yêu cầu truyền chuỗi kết nối khi khởi tạo các lớp kế thừa
        /// </summary>
        /// <param name="connectionString">Chuỗi kết nối CSDL</param>
        public BaseRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Tạo một đối tượng Connection để kết nối đến SQL Server
        /// </summary>
        /// <returns></returns>
        protected SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}