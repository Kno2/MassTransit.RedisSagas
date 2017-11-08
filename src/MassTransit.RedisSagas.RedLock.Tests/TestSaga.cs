using System;
using MassTransit.Saga;

namespace MassTransit.RedisSagas.RedLock.Tests
{
    public class TestSaga : ISaga
    {
        public Guid CorrelationId { get; set; }
        public Guid Id => CorrelationId;
    }
}