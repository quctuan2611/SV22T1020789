namespace SV22T1020789.Shop.Models
{
    /// <summary>
    /// Đại diện cho 1 dòng sản phẩm nằm trong Giỏ hàng
    /// </summary>
    public class CartItem
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = "";
        public string Photo { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }

        // Tính thành tiền của mặt hàng này
        public decimal TotalPrice => Quantity * Price;
    }
}