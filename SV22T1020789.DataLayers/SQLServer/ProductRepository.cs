using Dapper;
using SV22T1020789.DataLayers.Interfaces;
using SV22T1020789.Models.Catalog;
using SV22T1020789.Models.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SV22T1020789.DataLayers.SQLServer
{
    /// <summary>
    /// Triển khai các phép xử lý dữ liệu cho Entity Product, ProductAttribute, ProductPhoto trên SQL Server
    /// </summary>
    public class ProductRepository : BaseRepository, IProductRepository
    {
        // Truyền chuỗi kết nối lên cho BaseRepository xử lý
        public ProductRepository(string connectionString) : base(connectionString)
        {
        }

        #region Xử lý dữ liệu Mặt hàng (Product)

        public async Task<int> AddAsync(Product data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO Products (ProductName, ProductDescription, SupplierID, CategoryID, Unit, Price, Photo, IsSelling)
                VALUES (@ProductName, @ProductDescription, @SupplierID, @CategoryID, @Unit, @Price, @Photo, @IsSelling);
                
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, data);
        }

        public async Task<bool> DeleteAsync(int productID)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM Products WHERE ProductID = @ProductID";
            int rowsAffected = await connection.ExecuteAsync(sql, new { ProductID = productID });
            return rowsAffected > 0;
        }

        public async Task<Product?> GetAsync(int productID)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM Products WHERE ProductID = @ProductID";
            return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { ProductID = productID });
        }

        public async Task<bool> IsUsedAsync(int productID)
        {
            using var connection = GetConnection();
            string sql = @"
                IF EXISTS(SELECT 1 FROM OrderDetails WHERE ProductID = @ProductID)
                    SELECT 1;
                ELSE 
                    SELECT 0;";
            return await connection.ExecuteScalarAsync<bool>(sql, new { ProductID = productID });
        }

        public async Task<PagedResult<Product>> ListAsync(ProductSearchInput input)
        {
            var result = new PagedResult<Product>
            {
                Page = input.Page,
                PageSize = input.PageSize
            };

            string searchValue = string.IsNullOrEmpty(input.SearchValue) ? "" : $"%{input.SearchValue}%";
            using var connection = GetConnection();

            // ĐÃ CHỈNH SỬA: Tối ưu điều kiện tìm kiếm giá
            string condition = @"
                (@SearchValue = N'' OR ProductName LIKE @SearchValue)
                AND (@CategoryID = 0 OR CategoryID = @CategoryID)
                AND (@SupplierID = 0 OR SupplierID = @SupplierID)
                AND (Price >= @MinPrice)
                AND (@MaxPrice = 0 OR Price <= @MaxPrice)";

            string countSql = $"SELECT COUNT(*) FROM Products WHERE {condition}";
            result.RowCount = await connection.ExecuteScalarAsync<int>(countSql, new
            {
                SearchValue = searchValue,
                CategoryID = input.CategoryID,
                SupplierID = input.SupplierID,
                MinPrice = input.MinPrice,
                MaxPrice = input.MaxPrice
            });

            if (result.RowCount > 0)
            {
                if (input.PageSize == 0)
                {
                    string queryAllSql = $"SELECT * FROM Products WHERE {condition} ORDER BY ProductName";
                    var data = await connection.QueryAsync<Product>(queryAllSql, new
                    {
                        SearchValue = searchValue,
                        CategoryID = input.CategoryID,
                        SupplierID = input.SupplierID,
                        MinPrice = input.MinPrice,
                        MaxPrice = input.MaxPrice
                    });
                    result.DataItems = data.ToList();
                }
                else
                {
                    string queryPagedSql = $@"
                        SELECT * FROM Products WHERE {condition} ORDER BY ProductName
                        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                    var data = await connection.QueryAsync<Product>(queryPagedSql, new
                    {
                        SearchValue = searchValue,
                        CategoryID = input.CategoryID,
                        SupplierID = input.SupplierID,
                        MinPrice = input.MinPrice,
                        MaxPrice = input.MaxPrice,
                        Offset = input.Offset, // Đảm bảo model ProductSearchInput của bạn có thuộc tính Offset
                        PageSize = input.PageSize
                    });
                    result.DataItems = data.ToList();
                }
            }
            return result;
        }

        public async Task<bool> UpdateAsync(Product data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE Products
                SET ProductName = @ProductName, ProductDescription = @ProductDescription, SupplierID = @SupplierID,
                    CategoryID = @CategoryID, Unit = @Unit, Price = @Price, Photo = @Photo, IsSelling = @IsSelling
                WHERE ProductID = @ProductID";
            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }

        #endregion

        #region Xử lý dữ liệu Thuộc tính (ProductAttribute)

        public async Task<long> AddAttributeAsync(ProductAttribute data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO ProductAttributes (ProductID, AttributeName, AttributeValue, DisplayOrder)
                VALUES (@ProductID, @AttributeName, @AttributeValue, @DisplayOrder);
                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
            return await connection.ExecuteScalarAsync<long>(sql, data);
        }

        public async Task<bool> DeleteAttributeAsync(long attributeID)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM ProductAttributes WHERE AttributeID = @AttributeID";
            int rowsAffected = await connection.ExecuteAsync(sql, new { AttributeID = attributeID });
            return rowsAffected > 0;
        }

        public async Task<ProductAttribute?> GetAttributeAsync(long attributeID)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM ProductAttributes WHERE AttributeID = @AttributeID";
            return await connection.QueryFirstOrDefaultAsync<ProductAttribute>(sql, new { AttributeID = attributeID });
        }

        public async Task<List<ProductAttribute>> ListAttributesAsync(int productID)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM ProductAttributes WHERE ProductID = @ProductID ORDER BY DisplayOrder";
            var data = await connection.QueryAsync<ProductAttribute>(sql, new { ProductID = productID });
            return data.ToList();
        }

        public async Task<bool> UpdateAttributeAsync(ProductAttribute data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE ProductAttributes
                SET ProductID = @ProductID, AttributeName = @AttributeName, AttributeValue = @AttributeValue, DisplayOrder = @DisplayOrder
                WHERE AttributeID = @AttributeID";
            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }

        #endregion

        #region Xử lý dữ liệu Ảnh (ProductPhoto)

        public async Task<long> AddPhotoAsync(ProductPhoto data)
        {
            using var connection = GetConnection();
            string sql = @"
                INSERT INTO ProductPhotos (ProductID, Photo, Description, DisplayOrder, IsHidden)
                VALUES (@ProductID, @Photo, @Description, @DisplayOrder, @IsHidden);
                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
            return await connection.ExecuteScalarAsync<long>(sql, data);
        }

        public async Task<bool> DeletePhotoAsync(long photoID)
        {
            using var connection = GetConnection();
            string sql = "DELETE FROM ProductPhotos WHERE PhotoID = @PhotoID";
            int rowsAffected = await connection.ExecuteAsync(sql, new { PhotoID = photoID });
            return rowsAffected > 0;
        }

        public async Task<ProductPhoto?> GetPhotoAsync(long photoID)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM ProductPhotos WHERE PhotoID = @PhotoID";
            return await connection.QueryFirstOrDefaultAsync<ProductPhoto>(sql, new { PhotoID = photoID });
        }

        public async Task<List<ProductPhoto>> ListPhotosAsync(int productID)
        {
            using var connection = GetConnection();
            string sql = "SELECT * FROM ProductPhotos WHERE ProductID = @ProductID ORDER BY DisplayOrder";
            var data = await connection.QueryAsync<ProductPhoto>(sql, new { ProductID = productID });
            return data.ToList();
        }

        public async Task<bool> UpdatePhotoAsync(ProductPhoto data)
        {
            using var connection = GetConnection();
            string sql = @"
                UPDATE ProductPhotos
                SET ProductID = @ProductID, Photo = @Photo, Description = @Description, DisplayOrder = @DisplayOrder, IsHidden = @IsHidden
                WHERE PhotoID = @PhotoID";
            int rowsAffected = await connection.ExecuteAsync(sql, data);
            return rowsAffected > 0;
        }

        #endregion
    }
}