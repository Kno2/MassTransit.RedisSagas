using System;
using System.Threading.Tasks;
using MassTransit.Saga;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace MassTransit.RedisSagas
{
    public static class RedisExtensions
    {

        /// <summary>
		///     Get the object with the specified key from Redis database
		/// </summary>
		/// <typeparam name="T">The type of the expected object</typeparam>
		/// <param name="key">The cache key.</param>
		/// <returns>
		///     Null if not present, otherwise the instance of T.
		/// </returns>
        public static T Get<T>(this IDatabase db, string key)
        {
            var valueBytes = db.StringGet(key);
            return !valueBytes.HasValue ? default(T) : JsonConvert.DeserializeObject<T>(valueBytes);
        }

        /// <summary>
        ///     Get the object with the specified key from Redis database
        /// </summary>
        /// <typeparam name="T">The type of the expected object</typeparam>
        /// <param name="key">The cache key.</param>
        /// <returns>
        ///     Null if not present, otherwise the instance of T.
        /// </returns>
        public static T Get<T>(this IDatabase db, Guid key)
        {
            var valueBytes = db.StringGet(key.ToString());
            return !valueBytes.HasValue ? default(T) : JsonConvert.DeserializeObject<T>(valueBytes);
        }


        /// <summary>
        ///     Get the object with the specified key from Redis database
        /// </summary>
        /// <typeparam name="T">The type of the expected object</typeparam>
        /// <param name="key">The cache key.</param>
        /// <returns>
        ///     Null if not present, otherwise the instance of T.
        /// </returns>
        public static async Task<T> GetAsync<T>(this IDatabase database, string key)
        {
            var valueBytes = await database.StringGetAsync(key);

            if (!valueBytes.HasValue)
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(valueBytes);
        }


        /// <summary>
		///     Adds the specified instance to the Redis database.
		/// </summary>
		/// <typeparam name="T">The type of the class to add to Redis</typeparam>
		/// <param name="key">The cache key.</param>
		/// <param name="value">The instance of T.</param>
		/// <returns>
		///     True if the object has been added. Otherwise false
		/// </returns>
		public static bool Add<T>(this IDatabase database, string key, T value)
        {
            var entryBytes = JsonConvert.SerializeObject(value);

            return database.StringSet(key, entryBytes);
        }

        /// <summary>
        ///     Adds the specified instance to the Redis database.
        /// </summary>
        /// <typeparam name="T">The type of the class to add to Redis</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The instance of T.</param>
        /// <returns>
        ///     True if the object has been added. Otherwise false
        /// </returns>
        public static bool Add<T>(this IDatabase database, ISaga value)
        {
            var entryBytes = JsonConvert.SerializeObject(value);

            return database.StringSet(value.CorrelationId.ToString(), entryBytes);
        }

    }
}