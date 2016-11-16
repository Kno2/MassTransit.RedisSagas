# MassTransit.RedisSagas
StackExchange Redis Saga Persistence for MassTransit

This library uses `StackExchange.Redis` to connect to your redis instance. Refer to the StackExchange Documentation for more detail on configuring your client.

###Usage

The repository constructor requires `StackExchange.Redis.IConnectionMultiplexer` this is an expensive object to create and you should hold this object or make a singleton with your container.

### Using with Autofac sample

```
builder.Register(cx => ConnectionMultiplexer.Connect("localhost"))
.As<IConnectionMultiplexer>()
.SingleInstace();
```
