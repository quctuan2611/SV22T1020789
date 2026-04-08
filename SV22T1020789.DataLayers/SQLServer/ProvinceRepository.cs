using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.DataDictionary;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai phép xử lý dữ liệu cho từ điển dữ liệu Tỉnh/Thành phố trên SQL Server
    /// </summary>
    public class ProvinceRepository : BaseRepository, IDataDictionaryRepository<Province>
    {
        public ProvinceRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<List<Province>> ListAsync()
        {
            using var connection = GetConnection();

            // Lấy danh sách tất cả các tỉnh thành và sắp xếp theo bảng chữ cái
            string sql = @"
                SELECT ProvinceName 
                FROM Provinces 
                ORDER BY ProvinceName";

            var data = await connection.QueryAsync<Province>(sql);

            return data.ToList();
        }
    }
}