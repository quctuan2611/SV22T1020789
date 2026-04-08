using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. Thêm dịch vụ Controller và View
builder.Services.AddControllersWithViews();

// 2. Đăng ký HttpContextAccessor (Quan trọng để Layout/Service đọc được Session/Cookie)
builder.Services.AddHttpContextAccessor();

// 3. Cấu hình Session (Dùng để lưu Giỏ hàng - Cart)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".SV22T1020789.Session";
});

// 4. Cấu hình Authentication (Đăng nhập cho Khách hàng bên Shop)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ShopperAuthenticationCookie";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// 5. KHỞI TẠO CÁC DỊCH VỤ DỮ LIỆU (DATABASE)
// Lấy chuỗi kết nối từ file appsettings.json
string connectionString = builder.Configuration.GetConnectionString("LiteCommerceDB") ?? "";

// Khởi tạo các Service nghiệp vụ (Hàng hóa, Khách hàng, Tỉnh thành...)
SV22T1020789.BusinessLayers.Configuration.Initialize(connectionString);

// CHỖ NÀY CỰC KỲ QUAN TRỌNG: Khởi tạo dịch vụ bảo mật/đăng nhập
// Nếu thiếu dòng này, ní gọi SecurityDataService.CustomerAuthorizeAsync sẽ bị lỗi Null!
SV22T1020789.BusinessLayers.SecurityDataService.Init(connectionString);


// --- CẤU HÌNH PIPELINE (THỨ TỰ MIDDLEWARE LÀ SỐNG CÒN) ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

// UseSession phải nằm SAU UseRouting và TRƯỚC UseAuthentication
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// 6. Cấu hình Route mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();