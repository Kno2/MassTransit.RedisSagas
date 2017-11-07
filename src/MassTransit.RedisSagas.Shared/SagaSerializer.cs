using Newtonsoft.Json;
using MassTransit.Serialization;

namespace MassTransit.RedisSagas
{
    static class SagaSerializer
    {
        internal static string Serialize<T>(T value) =>
            JsonConvert.SerializeObject(value, typeof(T), JsonMessageSerializer.SerializerSettings);

        internal static T Deserialize<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json, JsonMessageSerializer.DeserializerSettings);
    }
}
