using System;
using System.Threading.Tasks;
using MassTransit.RedisSaga;
using MassTransit.Saga;
using MassTransit.RedisSagas;

namespace MassTransit.RedisSagas.Test
{
    public class SimpleSaga :
    InitiatedBy<InitiateSimpleSaga>,
    Orchestrates<CompleteSimpleSaga>,
    ISaga, IVersionedSaga
    {
        public bool Completed { get; set; }
        public bool Initiated { get; set; }
        public bool Observed { get; set; }
        public string Name { get; set; }

        public async Task Consume(ConsumeContext<InitiateSimpleSaga> context)
        {
            Initiated = true;
            Name = context.Message.Name;
        }

        public Guid CorrelationId { get; set; }

        //public async Task Consume(ConsumeContext<ObservableSagaMessage> message)
        //{
        //    Observed = true;
        //}

        //public Expression<Func<SimpleSaga, ObservableSagaMessage, bool>> CorrelationExpression
        //{
        //    get { return (saga, message) => saga.Name == message.Name; }
        //}

        public async Task Consume(ConsumeContext<CompleteSimpleSaga> message)
        {
            Completed = true;
        }

        public Guid Id => CorrelationId;
        public int Version { get; set; }
    }
}