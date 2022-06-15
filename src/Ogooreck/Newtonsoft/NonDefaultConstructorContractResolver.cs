using Newtonsoft.Json.Serialization;

namespace Ogooreck.Newtonsoft;

/// <summary>
///
/// </summary>
public class NonDefaultConstructorContractResolver: DefaultContractResolver
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="objectType"></param>
    /// <returns></returns>
    protected override JsonObjectContract CreateObjectContract(Type objectType)
    {
        return JsonObjectContractProvider.UsingNonDefaultConstructor(
            base.CreateObjectContract(objectType),
            objectType,
            base.CreateConstructorParameters
        );
    }
}
