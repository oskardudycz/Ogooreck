using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Ogooreck.API;

internal sealed record ApiJsonSettings(JsonSerializerOptions Request, JsonSerializerOptions Response)
{
    internal static readonly ApiJsonSettings Web = new(JsonSerializerOptions.Web, JsonSerializerOptions.Web);

    internal static ApiJsonSettings FromServices(IServiceProvider services)
    {
        var mvc = services.GetService<IOptions<MvcOptions>>()?.Value;
        var input = mvc?.InputFormatters.OfType<SystemTextJsonInputFormatter>()
            .FirstOrDefault(f => f.SupportedMediaTypes.Contains("application/json"));
        var output = mvc?.OutputFormatters.OfType<SystemTextJsonOutputFormatter>()
            .FirstOrDefault(f => f.SupportedMediaTypes.Contains("application/json"));

        if (input is not null && output is not null)
            return new ApiJsonSettings(input.SerializerOptions, output.SerializerOptions);

        var http = services.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions;
        return http is null ? Web : new ApiJsonSettings(http, http);
    }
}
