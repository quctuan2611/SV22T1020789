using System.Text.Json;

namespace SV22T1020789.Shop 
{
    public static class SessionExtensions
    {
        // Hàm ghi dữ liệu phức tạp (như List) vào Session
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        // Hàm đọc dữ liệu phức tạp từ Session
        public static T? Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default : JsonSerializer.Deserialize<T>(value);
        }
    }
}