using Newtonsoft.Json;

namespace MassTransit.RedisSagas
{
    public static class SagaSerializer
    {
        public static string Serialize<T>(T value) =>
            JsonConvert.SerializeObject(value);

        public static T Deserialize<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json);
    }
}
