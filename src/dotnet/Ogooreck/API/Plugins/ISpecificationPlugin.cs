namespace Ogooreck.API;

/// <summary>
/// Wires one tool into a specification: a messaging tool, an event store, or any service of the app that a test needs to watch.
/// Pass it to <c>Use(...)</c>. A plugin calls the same configuration methods on its <see cref="SpecificationPluginRegistration"/> that a specification offers directly,
/// so a setup is one <c>Use(plugin)</c> call per tool instead of a setup class that grows with every tool.
/// </summary>
/// <remarks>
/// A plugin is usually a static field shared by every specification of a test project, so keep per-specification state in the registration's callbacks, not in the plugin.
/// A plugin that holds resources implements <see cref="IAsyncDisposable"/> and belongs to the caller, as an <see cref="OtlpReceiver"/> passed to <c>Telemetry(...)</c> does.
/// </remarks>
/// <example>
/// <code>
/// public sealed class FakeClock: ISpecificationPlugin
/// {
///     public FakeTimeProvider Time { get; } = new();
///
///     public void Register(SpecificationPluginRegistration registration) =&gt;
///         registration.Services(services =&gt; services.AddSingleton&lt;TimeProvider&gt;(Time));
/// }
/// </code>
/// </example>
public interface ISpecificationPlugin
{
    /// <summary>Registers what the plugin contributes. <c>Use(...)</c> calls it at once, once per specification.</summary>
    /// <example>
    /// <code>
    /// public void Register(SpecificationPluginRegistration registration) =&gt; registration.Recording(Published, Read, CatchUp);
    /// </code>
    /// </example>
    public void Register(SpecificationPluginRegistration registration);
}

/// <summary>
/// A plugin made from another plugin through an option, such as <c>Wolverine.MessageTypes(...)</c>, that shares the first plugin's recorder.
/// When a specification already uses the original plugin, passing the derived plugin to <c>Use(...)</c> replaces it, so nothing is recorded twice.
/// </summary>
/// <example>
/// <code>
/// public sealed class MartenPlugin: IDerivedPlugin
/// {
///     public object Origin { get; }
/// }
/// </code>
/// </example>
public interface IDerivedPlugin: ISpecificationPlugin
{
    /// <summary>The first plugin of the family: the plugin that no option made, which every plugin made from it, directly or not, returns.</summary>
    /// <example>
    /// <code>
    /// public object Origin =&gt; origin;
    /// </code>
    /// </example>
    public object Origin { get; }
}
