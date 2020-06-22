using System;

namespace MassTransit.RedisSagas.Exceptions
{
    public class RedisSagaConcurrencyException : SagaException
    {
        public RedisSagaConcurrencyException()
        {
        }

        public RedisSagaConcurrencyException(string message, Type sagaType, Type messageType, Guid correlationId)
            : base(message, sagaType, messageType, correlationId)
        {
        }
    }
}
