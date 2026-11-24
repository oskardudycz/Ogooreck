namespace Ogooreck.API;

/// <summary>The Given steps of a scenario.</summary>
public sealed class GivenApiSpecificationBuilder
{
    private readonly Lazy<StartedHost> host;
    private readonly SpecificationSettings settings;
    private readonly TestStep[] given;

    internal GivenApiSpecificationBuilder(Lazy<StartedHost> host, SpecificationSettings settings, TestStep[] given)
    {
        this.host = host;
        this.settings = settings;
        this.given = given;
    }

    /// <summary>Sets the single step under test.</summary>
    /// <example>
    /// <code>
    /// api.Given(CreateCart()).When(AddProduct(cartId, "socks"))
    /// </code>
    /// </example>
    public WhenApiSpecificationBuilder When(TestStep step) => new(host, settings, given, step);
}
