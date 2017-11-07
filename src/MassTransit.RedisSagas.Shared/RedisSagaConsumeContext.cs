using System;
using System.Threading.Tasks;
using MassTransit.Context;
using MassTransit.Logging;
using MassTransit.Util;
using StackExchange.Redis;

namespace MassTransit.RedisSagas
{
    public class RedisSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextProxyScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, IVersionedSaga
    {
        static readonly ILog Log = Logger.Get<RedisSagaRepository<TSaga>>();
        readonly IDatabase _redisDb;

        public RedisSagaConsumeContext(IDatabase redisDb, ConsumeContext<TMessage> context, TSaga instance)
            : base(context)
        {
            Saga = instance;
            _redisDb = redisDb;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        SagaConsumeContext<TSaga, T> SagaConsumeContext<TSaga>.PopContext<T>()
        {
            var context = this as SagaConsumeContext<TSaga, T>;
            if (context == null)
                throw new ContextException($"The ConsumeContext<{TypeMetadataCache<TMessage>.ShortName}> could not be cast to {TypeMetadataCache<T>.ShortName}");

            return context;
        }

        async Task SagaConsumeContext<TSaga>.SetCompleted()
        {
            ITypedDatabase<TSaga> db = _redisDb.As<TSaga>();
            await db.Delete(Saga.CorrelationId).ConfigureAwait(false);

            IsCompleted = true;
            if (Log.IsDebugEnabled)
                Log.DebugFormat("SAGA:{0}:{1} Removed {2}", TypeMetadataCache<TSaga>.ShortName, TypeMetadataCache<TMessage>.ShortName,
                    Saga.CorrelationId);
        }

        public TSaga Saga { get; }
        public bool IsCompleted { get; private set; }
    }
}