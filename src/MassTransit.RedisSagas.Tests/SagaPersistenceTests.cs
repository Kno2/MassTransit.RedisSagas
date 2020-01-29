using System;
using System.Threading.Tasks;
using MassTransit.Saga;
using MassTransit.TestFramework;
using NUnit.Framework;
using RedisInside;
using Shouldly;
using StackExchange.Redis;

namespace MassTransit.RedisSagas.Tests
{
    [TestFixture]
    public class LocateAnExistingSaga : InMemoryTestFixture
    {
        private readonly Redis _redis;

        [OneTimeTearDown]
        public void TearDownRedis()
        {
            _redis.Dispose();
        }

        private readonly Lazy<ISagaRepository<SimpleSaga>> _sagaRepository;
        private readonly ConnectionMultiplexer _clientManager;

        public LocateAnExistingSaga()
        {
            _redis = new Redis();
            _clientManager = ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = {_redis.Endpoint}
            });
            _sagaRepository = new Lazy<ISagaRepository<SimpleSaga>>(() => new RedisSagaRepository<SimpleSaga>(_clientManager));
        }

        protected override void ConfigureInMemoryReceiveEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
            configurator.Saga(_sagaRepository.Value);
        }

        [Test]
        public async Task A_correlated_message_should_find_the_correct_saga()
        {
            var sagaId = NewId.NextGuid();
            var message = new InitiateSimpleSaga(sagaId);

            await InputQueueSendEndpoint.Send(message).ConfigureAwait(false);

            var found = await _sagaRepository.Value.ShouldContainSaga(message.CorrelationId, TestTimeout);

            found.ShouldBeTrue();

            var nextMessage = new CompleteSimpleSaga {CorrelationId = sagaId};

            await InputQueueSendEndpoint.Send(nextMessage).ConfigureAwait(false);

            found = await _sagaRepository.Value.ShouldContainSaga(sagaId, x => x.Completed, TestTimeout).ConfigureAwait(false);
            found.ShouldBeTrue();
            var retrieveRepository = _sagaRepository.Value as IRetrieveSagaFromRepository<SimpleSaga>;
            var retrieved = await retrieveRepository.GetSaga(sagaId);
            retrieved.ShouldNotBeNull();
            retrieved.Completed.ShouldBeTrue();
        }

        [Test]
        public async Task An_initiating_message_should_start_the_saga()
        {
            var sagaId = NewId.NextGuid();
            var message = new InitiateSimpleSaga(sagaId);

            await InputQueueSendEndpoint.Send(message);

            var found = await _sagaRepository.Value.ShouldContainSaga(message.CorrelationId, TestTimeout);

            found.ShouldBeTrue();
            var db = _clientManager.GetDatabase(0);
            var rawData = db.StringGet(sagaId.ToString());
            rawData.ShouldNotBeNull();
        }
    }
}
