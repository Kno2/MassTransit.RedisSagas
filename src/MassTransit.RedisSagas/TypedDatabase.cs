using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using MassTransit.RedisSagas;


namespace MassTransit.RedisSagas
{
    public class TypedDatabase<T> : ITypedDatabase<T> where T : class
    {
        readonly IDatabase _db;

        public TypedDatabase(IDatabase db) => _db = db;

        public async Task<T> Get(Guid key, string prefix = "")
        {
            var cacheKey = string.IsNullOrEmpty(prefix) ? key.ToString() : $"{prefix}:{key}";
            var value = await _db.StringGetAsync(cacheKey).ConfigureAwait(false);
            return value.IsNullOrEmpty ? null : SagaSerializer.Deserialize<T>(value);
        }

        public async Task Put(Guid key, T value, string prefix = "")
        {
            var cacheKey = string.IsNullOrEmpty(prefix) ? key.ToString() : $"{prefix}:{key}";
            await _db.StringSetAsync(cacheKey, SagaSerializer.Serialize(value)).ConfigureAwait(false);
        }

        public async Task Delete(Guid key, string prefix = "")
        {
            var cacheKey = string.IsNullOrEmpty(prefix) ? key.ToString() : $"{prefix}:{key}";
            await _db.KeyDeleteAsync(key.ToString()).ConfigureAwait(false);
        }
    }

    public interface ITypedDatabase<T> where T : class
    {
        Task<T> Get(Guid key, string keyPrefix);
        Task Put(Guid key, T value, string keyPrefix);
        Task Delete(Guid key, string keyPrefix);
    }

    public static class DatabaseExtensions
    {
        public static ITypedDatabase<T> As<T>(this IDatabase db) where T : class =>
            new TypedDatabase<T>(db);
    }

    
}
