using System.ComponentModel;
using FluentAssertions;
using Newtonsoft.Json;
using Ogooreck.Newtonsoft;

namespace Ogooreck.API;

#pragma warning disable CS1591
public static class HttpResponseMessageExtensions
{
    public static bool TryGetCreatedId<T>(this HttpResponseMessage response, out T? value)
    {
        value = default;

        var locationHeader = response.Headers.Location?.OriginalString.TrimEnd('/');

        if (string.IsNullOrEmpty(locationHeader))
            return false;

        locationHeader = locationHeader.StartsWith("/") ? locationHeader : $"/{locationHeader}";

        var start = locationHeader.LastIndexOf("/", locationHeader.Length - 1, StringComparison.Ordinal);

        var createdId = locationHeader.Substring(start + 1, locationHeader.Length - 1 - start);

        var result = TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(createdId);

        if (result == null)
            return false;

        value = (T?)result;

        return true;
    }

    public static T GetCreatedId<T>(this HttpResponseMessage response) =>
        response.TryGetCreatedId<T>(out var createdId)
            ? createdId!
            : throw new ArgumentOutOfRangeException(nameof(response.Headers.Location));

    public static string GetCreatedId(this HttpResponseMessage response) =>
        response.GetCreatedId<string>();

    public static bool TryGetETagValue<T>(this HttpResponseMessage response, out T? value)
    {
        value = default;

        var eTagHeader = response.Headers.ETag?.Tag;

        if (string.IsNullOrEmpty(eTagHeader))
            return false;

        eTagHeader = eTagHeader.Substring(1, eTagHeader.Length - 2);

        var result = TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(eTagHeader);

        if (result == null)
            return false;

        value = (T?)result;

        return true;
    }

    public static T GetETagValue<T>(this HttpResponseMessage response) =>
        response.TryGetCreatedId<T>(out var createdId)
            ? createdId!
            : throw new ArgumentOutOfRangeException(nameof(response.Headers.ETag));

    public static string GetETagValue(this HttpResponseMessage response) =>
        response.GetETagValue<string>();

    public static Task<T> GetResultFromJson<T>(
        this ApiSpecification.Result result,
        JsonSerializerSettings? settings = null
    ) =>
        result.Response.GetResultFromJson<T>();


    public static async Task<T> GetResultFromJson<T>(
        this HttpResponseMessage response,
        JsonSerializerSettings? settings = null
    )
    {
        var result = await response.Content.ReadAsStringAsync();

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        var deserialised = result.FromJson<T>(settings);

        deserialised.Should().NotBeNull();

        return deserialised;
    }
}
