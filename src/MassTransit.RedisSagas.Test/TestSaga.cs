using System;
using MassTransit.Saga;

namespace MassTransit.RedisSagas.Test
{
    public class TestSaga : ISaga
    {
        public Guid CorrelationId { get; set; }
        public Guid Id => CorrelationId;
    }
}