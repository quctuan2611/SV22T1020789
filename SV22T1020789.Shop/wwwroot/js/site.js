$(function () {
    // 1. Hiệu ứng mượt mà khi mới load trang
    $('main').hide().fadeIn(800);

    // 2. Hiệu ứng "Giả lập" khi bấm thêm vào giỏ hàng (Để UI nhìn xịn hơn)
    $('.btn-custom-cart').on('click', function (e) {
        // Tạm thời ngăn việc chuyển trang ngay lập tức để xem hiệu ứng
        // e.preventDefault(); 

        let btn = $(this);
        let originalText = btn.html();

        // Đổi màu và chữ
        btn.html('<i class="bi bi-check2-circle"></i> Đã thêm vào giỏ');
        btn.css('background', 'linear-gradient(135deg, #11998e, #38ef7d)'); // Chuyển sang màu xanh lá
        btn.css('box-shadow', '0 5px 15px rgba(56, 239, 125, 0.4)');

        // Trả lại trạng thái cũ sau 2 giây
        setTimeout(function () {
            btn.html(originalText);
            btn.css('background', '');
            btn.css('box-shadow', '');
        }, 2000);
    });
});