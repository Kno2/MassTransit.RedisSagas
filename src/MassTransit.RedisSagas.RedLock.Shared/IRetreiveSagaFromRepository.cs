using System;
using MassTransit.Saga;

namespace MassTransit.RedisSagas
{
    public interface IRetrieveSagaFromRepository<out TSaga> where TSaga : ISaga
    {
        TSaga GetSaga(Guid correlationId);
    }
}
