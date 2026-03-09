using Newtonsoft.Json.Linq;

namespace AcadiaLogic.Dutchie.Models;

public static class JsonExtensionDataExtensions
{
    public static bool TryGetAdditionalValue<T>(
        this IDictionary<string, JToken>? additionalData,
        string key,
        out T? value)
    {
        value = default;

        if (additionalData is null || !additionalData.TryGetValue(key, out var token))
            return false;

        try
        {
            value = token.Type == JTokenType.Null ? default : token.ToObject<T>();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
