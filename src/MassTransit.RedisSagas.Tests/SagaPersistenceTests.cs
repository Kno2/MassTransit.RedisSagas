using NUnit.Framework;
using NUnit.Framework.Internal;

namespace MassTransit.RedisSagas.Tests
{
    [TestFixture]
    public class SagaPersistenceTests
    {

        [OneTimeTearDown]
        public void TearDownRedis() => _redis.Dispose();
    }
}