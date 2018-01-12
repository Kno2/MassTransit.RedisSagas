using System;
using System.Threading.Tasks;
using MassTransit.Context;
using MassTransit.Logging;
using MassTransit.RedisSagas.RedLock;
using MassTransit.Util;
using RedLock;
using StackExchange.Redis;

namespace MassTransit.RedisSagas
{
    public class RedLockSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextProxyScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, IVersionedSaga
    {
        static readonly ILog Log = Logger.Get<RedLockSagaRepository<TSaga>>();
        readonly IDatabase _redisDb;
        private readonly IRedisLockFactory _lockFactory;
        private readonly string _redisPrefix;

        public RedLockSagaConsumeContext(IDatabase redisDb, IRedisLockFactory lockFactory, ConsumeContext<TMessage> context, TSaga instance, string redisPrefix = "")
            : base(context)
        {
            Saga = instance;
            _redisDb = redisDb;
            _lockFactory = lockFactory;
            _redisPrefix = redisPrefix;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        SagaConsumeContext<TSaga, T> SagaConsumeContext<TSaga>.PopContext<T>()
        {
            if (!(this is SagaConsumeContext<TSaga, T> context))
                throw new ContextException($"The ConsumeContext<{TypeMetadataCache<TMessage>.ShortName}> could not be cast to {TypeMetadataCache<T>.ShortName}");

            return context;
        }

        async Task SagaConsumeContext<TSaga>.SetCompleted()
        {
            ITypedDatabase<TSaga> db = _redisDb.As<TSaga>();

            using(var distLock  = _lockFactory.Create($"redislock:{Saga.CorrelationId}", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0.5)))
            {
                if (Log.IsDebugEnabled)
                    Log.Debug($"SAGA:{TypeMetadataCache<TSaga>.ShortName}:{TypeMetadataCache<TMessage>.ShortName} Entering Lock {Saga.CorrelationId}");

                if (distLock.IsAcquired)
                {
                    await db.Delete(Saga.CorrelationId, _redisPrefix).ConfigureAwait(false);
                }

                if (Log.IsDebugEnabled)
                    Log.Debug($"SAGA:{TypeMetadataCache<TSaga>.ShortName}:{TypeMetadataCache<TMessage>.ShortName} Leaving Lock {Saga.CorrelationId}");
            }

            IsCompleted = true;
            if (Log.IsDebugEnabled)
                Log.Debug($"SAGA:{TypeMetadataCache<TSaga>.ShortName}:{TypeMetadataCache<TMessage>.ShortName} Removed {Saga.CorrelationId}");
        }

        public TSaga Saga { get; }
        public bool IsCompleted { get; private set; }
    }
}