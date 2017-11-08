using System;
using MassTransit.Saga;
using System.Threading.Tasks;

namespace MassTransit.RedisSagas
{
    public interface IRetrieveSagaFromRepository<TSaga> where TSaga : ISaga
    {
        Task<TSaga> GetSaga(Guid correlationId);
    }
}
