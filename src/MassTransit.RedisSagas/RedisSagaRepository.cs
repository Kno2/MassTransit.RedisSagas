using System;
using System.Threading.Tasks;
using MassTransit.Logging;
using MassTransit.Pipeline;
using MassTransit.Saga;
using MassTransit.Util;
using StackExchange.Redis;

namespace MassTransit.RedisSagas
{
    public class RedisSagaRepository<TSaga> : ISagaRepository<TSaga>, IRetrieveSagaFromRepository<TSaga> where TSaga : class, IVersionedSaga
    {
        private readonly IConnectionMultiplexer _redis;
        static readonly ILog _log = Logger.Get<RedisSagaRepository<TSaga>>();

        public RedisSagaRepository(IConnectionMultiplexer redis)
        {
            _redis = redis;
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

        public async Task Send<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy, IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            if (!context.CorrelationId.HasValue)
                throw new SagaException("The CorrelationId was not specified", typeof(TSaga), typeof(T));

            var sagaId = context.CorrelationId.Value;
            TSaga instance;
            var redis = _redis.GetDatabase();

            if (policy.PreInsertInstance(context, out instance))
            {
                PreInsertSagaInstance(redis, context, instance);
            }

            if (instance == null)
            {
                instance = redis.Get<TSaga>(sagaId);
            }

            if (instance == null)
            {
                var missingSagaPipe = new MissingPipe<TSaga, T>(_redis, next);

                await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
            }
            else
            {
                await SendToInstance(context, policy, next, instance).ConfigureAwait(false);
            }
        }

        async Task SendToInstance<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy, IPipe<SagaConsumeContext<TSaga, T>> next, TSaga instance)
             where T : class
        {
            try
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Used {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId, TypeMetadataCache<T>.ShortName);

                SagaConsumeContext<TSaga, T> sagaConsumeContext = new RedisSagaConsumeContext<TSaga, T>(_redis, context, instance);

                await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);

                if (!sagaConsumeContext.IsCompleted)
                    UpdateRedisSaga(instance);
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

        private void UpdateRedisSaga(TSaga instance)
        {
            var db = _redis.GetDatabase();

            instance.Version++;
            var old = db.Get<TSaga>(instance.CorrelationId);
            if (old.Version > instance.Version)
                throw new RedisSagaConcurrencyException($"Version conflict for saga with id {instance.CorrelationId}");

            db.Add<TSaga>(instance);
        }


        public Task SendQuery<T>(SagaQueryConsumeContext<TSaga, T> context, ISagaPolicy<TSaga, T> policy, IPipe<SagaConsumeContext<TSaga, T>> next) where T : class
        {
            throw new NotImplementedByDesignException("Redis saga repository does not support queries");
        }

        public TSaga GetSaga(Guid correlationId)
        {
            var saga = _redis.GetDatabase().Get<TSaga>(correlationId);
            return saga;
        }

        private void PreInsertSagaInstance<T>(IDatabase db, ConsumeContext<T> context, TSaga instance) where T : class
        {
            try
            {
                db.Add(context.CorrelationId.ToString(), instance);

                _log.DebugFormat("SAGA:{0}:{1} Insert {2}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId, TypeMetadataCache<T>.ShortName);
            }
            catch (Exception ex)
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Dupe {2} - {3}", TypeMetadataCache<TSaga>.ShortName, instance.CorrelationId, TypeMetadataCache<T>.ShortName,
                        ex.Message);
            }
        }

        private class MissingPipe<TSaga, TMessage> :
            IPipe<SagaConsumeContext<TSaga, TMessage>>
        where TSaga : class, IVersionedSaga
        where TMessage : class
        {
            static readonly ILog _log = Logger.Get<RedisSagaRepository<TSaga>>();
            private readonly IConnectionMultiplexer _redis;
            readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;

            public MissingPipe(IConnectionMultiplexer redis, IPipe<SagaConsumeContext<TSaga, TMessage>> next)
            {
                _redis = redis;
                _next = next;
            }

            public void Probe(ProbeContext context)
            {
                _next.Probe(context);
            }

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("SAGA:{0}:{1} Added {2}", TypeMetadataCache<TSaga>.ShortName, context.Saga.CorrelationId,
                        TypeMetadataCache<TMessage>.ShortName);

                SagaConsumeContext<TSaga, TMessage> proxy = new RedisSagaConsumeContext<TSaga, TMessage>(_redis, context, context.Saga);

                await _next.Send(proxy).ConfigureAwait(false);

                if (!proxy.IsCompleted)
                    _redis.GetDatabase().Add<TSaga>(context.Saga);
            }
        }
    }
}