using MassTransit.Saga;

namespace MassTransit.RedisSagas
{
    public interface IVersionedSaga : ISaga
    {
        int Version { get; set; }
    }
}