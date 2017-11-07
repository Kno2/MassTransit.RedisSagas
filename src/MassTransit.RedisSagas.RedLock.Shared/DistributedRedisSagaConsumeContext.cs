using System;
using System.Threading.Tasks;
using MassTransit.Context;
using MassTransit.Logging;
using MassTransit.Util;
using RedLock;
using MassTransit.RedisSagas.Abstractions;
using TaskUtil = GreenPipes.Util.TaskUtil;

namespace MassTransit.RedisSagas.RedLock
{
    public class DistributedRedisSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextProxyScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, IVersionedSaga
    {

        private static readonly ILog Log = Logger.Get<DistributedRedisSagaRepository<TSaga>>();
        private readonly RedLockFactory _redLockFactory;

        public DistributedRedisSagaConsumeContext(RedLockFactory redLockFactory, ConsumeContext<TMessage> context, TSaga saga) : base(context)
        {
            Saga = saga;
            _redlockFactory = redLockFactory;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        public SagaConsumeContext<TSaga, T> PopContext<T>() where T : class
        {
            if (!(this is SagaConsumeContext<TSaga, T> context))
                throw new ContextException($"The ConsumeContext<{TypeMetadataCache<TMessage>.ShortName}> could not be cast to {TypeMetadataCache<T>.ShortName}");

            return context;
        }

        public Task SetCompleted()
        {
            var expiry = TimeSpan.FromSeconds(30);
            var wait = TimeSpan.FromSeconds(10);
            var retry = TimeSpan.FromSeconds(1);
            using (var redLock = await redlockFactory.CreateLockAsync(Saga.CorrelationId.ToString(), expiry, wait, retry))
            {
                if (redLock.IsAcquired)
                {
                    
                }
            }

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