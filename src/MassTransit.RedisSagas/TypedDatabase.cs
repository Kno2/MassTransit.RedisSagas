using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace MassTransit.RedisSagas
{
    public class TypedDatabase<T> : ITypedDatabase<T> where T : class
    {
        private readonly IDatabase _db;

        public TypedDatabase(IDatabase db)
        {
            _db = db;
        }

        public async Task<T> Get(Guid key, string prefix = "")
        {
            var cacheKey = string.IsNullOrWhiteSpace(prefix) ? key.ToString() : $"{prefix}:{key}";
            var value = await _db.StringGetAsync(cacheKey).ConfigureAwait(false);
            return value.IsNullOrEmpty ? null : SagaSerializer.Deserialize<T>(value);
        }

        public async Task Put(Guid key, T value, string prefix = "", TimeSpan? expiry = null)
        {
            var cacheKey = string.IsNullOrWhiteSpace(prefix) ? key.ToString() : $"{prefix}:{key}";
            await _db.StringSetAsync(cacheKey, SagaSerializer.Serialize(value), expiry).ConfigureAwait(false);
        }

        public async Task Delete(Guid key, string prefix = "")
        {
            var cacheKey = string.IsNullOrWhiteSpace(prefix) ? key.ToString() : $"{prefix}:{key}";
            await _db.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
        }
    }

    public interface ITypedDatabase<T> where T : class
    {
        Task<T> Get(Guid key, string keyPrefix);
        Task Put(Guid key, T value, string keyPrefix, TimeSpan? expiry = null);
        Task Delete(Guid key, string keyPrefix);
    }

    public static class DatabaseExtensions
    {
        public static ITypedDatabase<T> As<T>(this IDatabase db) where T : class
        {
            return new TypedDatabase<T>(db);
        }
    }

}
