using System;
using System.Threading.Tasks;
using MassTransit.Context;
using MassTransit.Logging;
using MassTransit.RedisSagas.Abstractions;
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

        private static readonly ILog Log = Logger.Get<RedisSagaRepository<TSaga>>();
        private readonly IConnectionMultiplexer _redis;

        public RedisSagaConsumeContext(IConnectionMultiplexer redis, ConsumeContext<TMessage> context, TSaga saga) : base(context)
        {
            _redis = redis;
            Saga = saga;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        public SagaConsumeContext<TSaga, T> PopContext<T>() where T : class
        {
            var context = this as SagaConsumeContext<TSaga, T>;
            if(context == null)
                throw new ContextException($"The ConsumeContext<{TypeMetadataCache<TMessage>.ShortName}> could not be cast to {TypeMetadataCache<T>.ShortName}");

            return context;
        }

        public Task SetCompleted()
        {
            var client = _redis.GetDatabase();

            client.KeyDelete(Saga.CorrelationId.ToString());

            IsCompleted = true;

            if (Log.IsDebugEnabled)
                Log.DebugFormat("SAGA:{0}:{1} Removed {2}", TypeMetadataCache<TSaga>.ShortName, TypeMetadataCache<TMessage>.ShortName,
                    Saga.CorrelationId);

            return TaskUtil.Completed;
        }

        public TSaga Saga { get; }
        public bool IsCompleted { get; set; }
    }
}