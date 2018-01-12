using MassTransit.Saga;

namespace MassTransit.RedisSagas.RedLock
{
    public interface IVersionedSaga : ISaga
    {
        int Version { get; set; }
    }
}
