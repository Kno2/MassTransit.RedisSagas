using System;

namespace MassTransit.RedisSagas
{
    public class RedisSagaConcurrencyException : MassTransitException
    {
        public RedisSagaConcurrencyException() { }

        public RedisSagaConcurrencyException(string message) : base(message) { }

        public RedisSagaConcurrencyException(string message, Exception inner) : base(message, inner) { }
    }
}