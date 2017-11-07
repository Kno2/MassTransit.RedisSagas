using MassTransit.Saga;

namespace MassTransit.RedisSaga
{
    public interface IVersionedSaga : ISaga
    {
        int Version { get; set; }
    }
}
