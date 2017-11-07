using System;
using MassTransit.Saga;

namespace MassTransit.RedisSagas.Abstractions.Shared
{
    public interface IRetrieveSagaFromRepository<out TSaga> where TSaga : ISaga
    {
        TSaga GetSaga(Guid correlationId);
    }
}
