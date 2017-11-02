# MassTransit.RedisSagas
StackExchange Redis Saga Persistence for MassTransit

[![Build status](https://ci.appveyor.com/api/projects/status/e0l18q0tjonu896p?svg=true)](https://ci.appveyor.com/project/ryankelley/masstransit-redissagas)

This library uses `StackExchange.Redis` to connect to your redis instance. Refer to the [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis/blob/master/Docs/Basics.md) Documentation for more detail on configuring your client.

### Installation

Install using nuget: `Install-Package MassTransit.RedisSagas`

### Usage

The repository constructor requires `StackExchange.Redis.IConnectionMultiplexer` this is an expensive object to create and you should hold this object or make a singleton with your container.

### Using with Autofac sample

```
builder.Register(cx => ConnectionMultiplexer.Connect("localhost"))
.As<IConnectionMultiplexer>()
.SingleInstace();
```
