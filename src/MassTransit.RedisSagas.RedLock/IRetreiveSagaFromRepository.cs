using System;
using System.Threading.Tasks;
using MassTransit.Saga;

namespace MassTransit.RedisSagas
{
    public interface IRetrieveSagaFromRepository<TSaga> where TSaga : ISaga
    {
        Task<TSaga> GetSaga(Guid correlationId);
    }
}
