using Newtonsoft.Json;
using MassTransit.Serialization;

namespace MassTransit.RedisSagas
{
    public static class SagaSerializer
    {
        public static string Serialize<T>(T value) =>
            JsonConvert.SerializeObject(value, typeof(T), JsonMessageSerializer.SerializerSettings);

        public static T Deserialize<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json, JsonMessageSerializer.DeserializerSettings);
    }
}
