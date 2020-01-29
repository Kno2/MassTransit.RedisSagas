using Newtonsoft.Json;

namespace MassTransit.RedisSagas
{
    public static class SagaSerializer
    {
        public static string Serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
