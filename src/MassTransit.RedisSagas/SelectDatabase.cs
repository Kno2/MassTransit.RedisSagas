namespace MassTransit.RedisSagas
{
    using StackExchange.Redis;


    public delegate IDatabase SelectDatabase(IConnectionMultiplexer multiplexer);
}
