using System.Diagnostics;
using System.Runtime.CompilerServices;

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

    /// <summary>Sets the single step under test. The run's trace names the calling test by its method, file and line.</summary>
    /// <example>
    /// <code>
    /// api.Given(CreateCart()).When(AddProduct(cartId, "socks"))
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public WhenApiSpecificationBuilder When(TestStep step, [CallerMemberName] string test = "",
        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        new(host, settings, given, step, TestCaller.Of(new StackFrame(1), test, file, line));
}
