namespace Ogooreck.API;

/// <summary>
/// Wires one tool into a specification: a messaging tool, an event store, or any service of the app that a test needs to watch.
/// Pass it to <c>Use(...)</c>. A plug calls the same configuration methods on its <see cref="PlugRegistration"/> that a specification offers directly,
/// so a setup is one <c>Use(plug)</c> call per tool instead of a setup class that grows with every tool.
/// </summary>
/// <remarks>
/// A plug is usually a static field shared by every specification of a test project, so keep per-specification state in the registration's callbacks, not in the plug.
/// A plug that holds resources implements <see cref="IAsyncDisposable"/> and belongs to the caller, as an <see cref="OtlpReceiver"/> passed to <c>Telemetry(...)</c> does.
/// </remarks>
/// <example>
/// <code>
/// public sealed class FakeClock: IPlug
/// {
///     public FakeTimeProvider Time { get; } = new();
///
///     public void Plug(PlugRegistration registration) =&gt;
///         registration.Services(services =&gt; services.AddSingleton&lt;TimeProvider&gt;(Time));
/// }
/// </code>
/// </example>
public interface IPlug
{
    /// <summary>Registers what the plug contributes. <c>Use(...)</c> calls it at once, once per specification.</summary>
    /// <example>
    /// <code>
    /// public void Plug(PlugRegistration registration) =&gt; registration.Recording(Published, Read, CatchUp);
    /// </code>
    /// </example>
    public void Plug(PlugRegistration registration);
}

/// <summary>
/// A plug made from another plug through an option, such as <c>Wolverine.MessageTypes(...)</c>, that shares the first plug's recorder.
/// When a specification already uses the original plug, passing the derived plug to <c>Use(...)</c> replaces it, so nothing is recorded twice.
/// </summary>
/// <example>
/// <code>
/// public sealed class MartenPlug: IDerivedPlug
/// {
///     public object Origin { get; }
/// }
/// </code>
/// </example>
public interface IDerivedPlug: IPlug
{
    /// <summary>The first plug of the family: the plug that no option made, which every plug made from it, directly or not, returns.</summary>
    /// <example>
    /// <code>
    /// public object Origin =&gt; origin;
    /// </code>
    /// </example>
    public object Origin { get; }
}
