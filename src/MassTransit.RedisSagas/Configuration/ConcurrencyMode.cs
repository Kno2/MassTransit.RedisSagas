namespace MassTransit.RedisSagas
{
    public enum ConcurrencyMode
    {
        Optimistic = 0,
        Pessimistic = 1,
    }
}
