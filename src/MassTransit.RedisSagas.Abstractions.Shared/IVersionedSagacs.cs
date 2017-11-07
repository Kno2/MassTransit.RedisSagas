using MassTransit.Saga;

namespace MassTransit.RedisSagas.Abstractions.Shared
{
    public interface IVersionedSaga : ISaga
    {
        int Version { get; set; }
    }

    
}
