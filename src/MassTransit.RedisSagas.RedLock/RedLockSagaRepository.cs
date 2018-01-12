using System;
using System.Threading.Tasks;
using GreenPipes;
using MassTransit.Logging;
using MassTransit.Saga;
using MassTransit.Util;
using RedLock;
using StackExchange.Redis;

namespace MassTransit.RedisSagas.RedLock
{
    public class RedLockSagaRepository<TSaga> : ISagaRepository<TSaga>,
        IRetrieveSagaFromRepository<TSaga>
        where TSaga : class, IVersionedSaga
    {
        private static readonly ILog _log = Logger.Get<RedLockSagaRepository<TSaga>>();

        private readonly IConnectionMultiplexer _redisConnection;
        private readonly IRedisLockFactory _lockFactory;
        private readonly string _redisPrefix;

        public RedLockSagaRepository(IConnectionMultiplexer redisConnection, IRedisLockFactory lockFactory, string redisPrefix)
        {
            _redisConnection = redisConnection;
            _lockFactory = lockFactory;
            _redisPrefix = redisPrefix;
        }

        public RedLockSagaRepository(IConnectionMultiplexer redisConnection, RedisLockFactory lockFactory)
        {
            _redisConnection = redisConnection;
            _lockFactory = lockFactory;
        }

        public async Task<TSaga> GetSaga(Guid correlationId)
        {
            //            using (var distLock = _lockFactory.Create($"redislock:{correlationId}", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0.5)))
            //            {
            //                if (distLock.IsAcquired)
            //                {
            var db = _redisConnection.GetDatabase();
            return await db.As<TSaga>().Get(correlationId, _redisPrefix);
            //                }
            //            }
            //            return null;
        }

        public async Task Send<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            if (!context.CorrelationId.HasValue)
                throw new SagaException("The CorrelationId was not specified", typeof(TSaga), typeof(T));

            var sagaId = context.CorrelationId.Value;
            var db = _redisConnection.GetDatabase();
            TSaga instance;
            ITypedDatabase<TSaga> sagas = db.As<TSaga>();

            if (policy.PreInsertInstance(context, out instance))
                await PreInsertSagaInstance<T>(sagas, instance).ConfigureAwait(false);

            if (instance == null)
            {
                using (var distLock = _lockFactory.Create($"redislock:{context.CorrelationId.Value}", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0.5)))
                {
                    if (distLock.IsAcquired)
                    {
                        instance = await sagas.Get(sagaId, _redisPrefix).ConfigureAwait(false);
                    }
                }
            }



            if (instance == null)
            {
                var missingSagaPipe = new MissingPipe<T>(db, _lockFactory, next, _redisPrefix);
                await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
            }
            else
            {
                await SendToInstance(context, policy, next, instance).ConfigureAwait(false);
            }
        }

        public Task SendQuery<T>(SagaQueryConsumeContext<TSaga, T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            throw new NotImplementedByDesignException("Redis saga repository does not support queries");
        }

        public void Probe(ProbeContext context)
        {
            var scope = context.CreateScope("sagaRepository");
            scope.Set(new
            {
                Persistence = "redis",
                SagaType = TypeMetadataCache<TSaga>.ShortName,
            });
        }

        async Task SendToInstance<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next, TSaga instance)
            where T : class
        {
            try
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId, TypeMetadataCache<T>.ShortName);

                var db = _redisConnection.GetDatabase();
                var sagaConsumeContext = new RedLockSagaConsumeContext<TSaga, T>(db, _lockFactory, context, instance, _redisPrefix);

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

        async Task<bool> PreInsertSagaInstance<T>(ITypedDatabase<TSaga> sagas, TSaga instance)
        {
            try
            {
                using (var distLock = _lockFactory.Create($"redislock:{instance.CorrelationId}", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)))
                {
                    if (distLock.IsAcquired)
                    {
                        // Work inside Lock
                        await sagas.Put(instance.CorrelationId, instance, _redisPrefix).ConfigureAwait(false);
                    }
                }


                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Insert {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Dupe {2} - {3}", TypeMetadataCache<TSaga>.ShortName,
                        instance.CorrelationId,
                        TypeMetadataCache<T>.ShortName, ex.Message);
                return false;
            }
        }

        async Task UpdateRedisSaga(TSaga instance)
        {
            var db = _redisConnection.GetDatabase();
            ITypedDatabase<TSaga> sagas = db.As<TSaga>();

            instance.Version++;
            using (var distLock = _lockFactory.Create($"redislock:{instance.CorrelationId}", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0.5)))
            {
                if (distLock.IsAcquired)
                {
                    var old = await sagas.Get(instance.CorrelationId, _redisPrefix).ConfigureAwait(false);
                    if (old.Version > instance.Version)
                        throw new RedisSagaConcurrencyException($"Version conflict for saga with id {instance.CorrelationId}");

                    await sagas.Put(instance.CorrelationId, instance, _redisPrefix).ConfigureAwait(false);
                }
            }
        }


        /// <summary>
        ///     Once the message pipe has processed the saga instance, add it to the saga repository
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        class MissingPipe<TMessage> :
            IPipe<SagaConsumeContext<TSaga, TMessage>>
            where TMessage : class
        {
            readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;
            readonly IDatabase _redisDb;
            private readonly IRedisLockFactory _lockFactory;
            private readonly string _redisPrefix;

            public MissingPipe(IDatabase redisDb, IRedisLockFactory lockFactory, IPipe<SagaConsumeContext<TSaga, TMessage>> next, string redisPrefix = "")
            {
                _redisDb = redisDb;
                _lockFactory = lockFactory;
                _next = next;
                _redisPrefix = redisPrefix;
            }

            void IProbeSite.Probe(ProbeContext context)
            {
                _next.Probe(context);
            }

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Added {2}", TypeMetadataCache<TSaga>.ShortName,
                        context.Saga.CorrelationId,
                        TypeMetadataCache<TMessage>.ShortName);

                SagaConsumeContext<TSaga, TMessage> proxy = new RedLockSagaConsumeContext<TSaga, TMessage>(_redisDb, _lockFactory, context, context.Saga, _redisPrefix);

                await _next.Send(proxy).ConfigureAwait(false);

                if (!proxy.IsCompleted)
                {
                    using (var distLock = _lockFactory.Create($"redislock:{context.Saga.CorrelationId}", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0.5)))
                    {
                        if (distLock.IsAcquired)
                        {
                            // Work inside Lock
                            await _redisDb.As<TSaga>().Put(context.Saga.CorrelationId, context.Saga, _redisPrefix).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }
}