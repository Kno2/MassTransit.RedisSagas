using System;
using System.Threading.Tasks;
using GreenPipes;
using MassTransit.Logging;
using MassTransit.Saga;
using MassTransit.Util;
using StackExchange.Redis;

namespace MassTransit.RedisSagas
{
    public class RedisSagaRepository<TSaga> : ISagaRepository<TSaga>, IRetrieveSagaFromRepository<TSaga> where TSaga : class, IVersionedSaga
    {
        private static readonly ILog _log = Logger.Get<RedisSagaRepository<TSaga>>();

        private readonly IConnectionMultiplexer _redisConnection;
        private readonly string _redisPrefix;
        private readonly TimeSpan? _ttl;

        /// <summary>
        ///     Creates new RedisSagaRepository
        /// </summary>
        /// <param name="redisConnection"></param>
        /// <param name="redisPrefix">String to prefix all keys with, will be delimited by a `:` e.g prefix:key</param>
        /// <param name="expiry">Sets the TTL on all keys if present</param>
        public RedisSagaRepository(IConnectionMultiplexer redisConnection, string redisPrefix, TimeSpan? expiry = null)
        {
            _redisConnection = redisConnection;
            _redisPrefix = redisPrefix;
            _ttl = expiry;
        }

        public RedisSagaRepository(IConnectionMultiplexer redisConnection)
        {
            _redisConnection = redisConnection;
        }

        public async Task<TSaga> GetSaga(Guid correlationId)
        {
            return await _redisConnection.GetDatabase().As<TSaga>().Get(correlationId, _redisPrefix).ConfigureAwait(false);
            ;
        }

        public async Task Send<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy, IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            if (!context.CorrelationId.HasValue)
                throw new SagaException("The CorrelationId was not specified", typeof(TSaga), typeof(T));

            var sagaId = context.CorrelationId.Value;
            var db = _redisConnection.GetDatabase();
            TSaga instance;
            var sagas = db.As<TSaga>();

            if (policy.PreInsertInstance(context, out instance))
                await PreInsertSagaInstance<T>(sagas, instance).ConfigureAwait(false);

            if (instance == null)
                instance = await sagas.Get(sagaId, _redisPrefix).ConfigureAwait(false);

            if (instance == null)
            {
                var missingSagaPipe = new MissingPipe<T>(db, next, _redisPrefix);
                await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
            }
            else
            {
                await SendToInstance(context, policy, next, instance).ConfigureAwait(false);
            }
        }

        public Task SendQuery<T>(SagaQueryConsumeContext<TSaga, T> context, ISagaPolicy<TSaga, T> policy, IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            throw new NotImplementedByDesignException("Redis saga repository does not support queries");
        }

        public void Probe(ProbeContext context)
        {
            var scope = context.CreateScope("sagaRepository");
            scope.Set(new
            {
                Persistence = "redis"
            });
        }

        private async Task SendToInstance<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy, IPipe<SagaConsumeContext<TSaga, T>> next, TSaga instance) where T : class
        {
            try
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId, TypeMetadataCache<T>.ShortName);

                var db = _redisConnection.GetDatabase();
                var sagaConsumeContext = new RedisSagaConsumeContext<TSaga, T>(db, context, instance, _redisPrefix);

                await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);

                if (!sagaConsumeContext.IsCompleted)
                    await UpdateRedisSaga(instance).ConfigureAwait(false);
            }
            catch (SagaException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SagaException(ex.Message, typeof(TSaga), typeof(T), instance.CorrelationId, ex);
            }
        }

        private async Task<bool> PreInsertSagaInstance<T>(ITypedDatabase<TSaga> sagas, TSaga instance)
        {
            try
            {
                await sagas.Put(instance.CorrelationId, instance, _redisPrefix, _ttl).ConfigureAwait(false);

                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Insert {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId, TypeMetadataCache<T>.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Dupe {2} - {3}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId, TypeMetadataCache<T>.ShortName, ex.Message);
                return false;
            }
        }

        private async Task UpdateRedisSaga(TSaga instance)
        {
            var db = _redisConnection.GetDatabase();
            var sagas = db.As<TSaga>();

            instance.Version++;
            var old = await sagas.Get(instance.CorrelationId, _redisPrefix).ConfigureAwait(false);
            if (old.Version > instance.Version)
                throw new RedisSagaConcurrencyException($"Version conflict for saga with id {instance.CorrelationId}");

            await sagas.Put(instance.CorrelationId, instance, _redisPrefix, _ttl).ConfigureAwait(false);
        }

        /// <summary>
        ///     Once the message pipe has processed the saga instance, add it to the saga repository
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        private class MissingPipe<TMessage> : IPipe<SagaConsumeContext<TSaga, TMessage>> where TMessage : class
        {
            private readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;
            private readonly IDatabase _redisDb;
            private readonly string _redisPrefix;
            private readonly TimeSpan? _ttl;

            public MissingPipe(IDatabase redisDb, IPipe<SagaConsumeContext<TSaga, TMessage>> next, string redisPrefix = "", TimeSpan? ttl = null)
            {
                _redisDb = redisDb;
                _next = next;
                _redisPrefix = redisPrefix;
                _ttl = ttl;
            }

            void IProbeSite.Probe(ProbeContext context)
            {
                _next.Probe(context);
            }

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Added {2}", TypeMetadataCache<TSaga>.ShortName, context.Saga.CorrelationId, TypeMetadataCache<TMessage>.ShortName);

                SagaConsumeContext<TSaga, TMessage> proxy = new RedisSagaConsumeContext<TSaga, TMessage>(_redisDb, context, context.Saga, _redisPrefix);

                await _next.Send(proxy).ConfigureAwait(false);

                if (!proxy.IsCompleted)
                    await _redisDb.As<TSaga>().Put(context.Saga.CorrelationId, context.Saga, _redisPrefix, _ttl).ConfigureAwait(false);
            }
        }
    }
}
