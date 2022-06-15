using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ogooreck.Newtonsoft;

/// <summary>
///
/// </summary>
public static class SerializationExtensions
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public static JsonSerializerSettings WithDefaults(this JsonSerializerSettings settings)
    {
        settings.WithNonDefaultConstructorContractResolver()
            .Converters.Add(new StringEnumConverter());

        return settings;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="settings"></param>
    /// <returns></returns>
    public static JsonSerializerSettings WithNonDefaultConstructorContractResolver(this JsonSerializerSettings settings)
    {
        settings.ContractResolver = new NonDefaultConstructorContractResolver();
        return settings;
    }

    /// <summary>
    /// Deserialize object from json with JsonNet
    /// </summary>
    /// <typeparam name="T">Type of the deserialized object</typeparam>
    /// <param name="json">json string</param>
    /// <param name="settings"></param>
    /// <returns>deserialized object</returns>
    public static T FromJson<T>(this string json, JsonSerializerSettings? settings = null)
    {
        return JsonConvert.DeserializeObject<T>(json,
            settings ?? new JsonSerializerSettings().WithDefaults())!;
    }
}
