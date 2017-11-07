using MassTransit.Saga;

namespace MassTransit.RedisSagas.Abstractions
{
    public interface IVersionedSaga : ISaga
    {
        int Version { get; set; }
    }
}
