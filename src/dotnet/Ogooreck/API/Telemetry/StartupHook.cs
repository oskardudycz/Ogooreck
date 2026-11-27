using System.Runtime.CompilerServices;
using Ogooreck.API;

internal static class StartupHook
{
    public static void Initialize() => RuntimeHelpers.RunClassConstructor(typeof(Diagnostics).TypeHandle);
}
