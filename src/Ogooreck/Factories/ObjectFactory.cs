using System.Runtime.CompilerServices;
namespace Ogooreck.Factories;

/// <summary>
/// Wraps objects creation
/// </summary>
/// <typeparam name="T"></typeparam>
public static class ObjectFactory<T>
{
    /// <summary>
    /// Creates empty unitialised instance of object T
    /// </summary>
    /// <returns></returns>
    public static T GetUnitialized() =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    /// <summary>
    /// Creates empty unitialised instance of object T
    /// </summary>
    /// <returns></returns>
    public static T? GetDefault()
    {
        try
        {
            return (T?)Activator.CreateInstance(typeof(T), true);
        }
        catch (MissingMethodException? e)
        {
            Console.WriteLine(e);
            return default;
        }
    }

    /// <summary>
    /// Creates empty unitialised instance of object T
    /// </summary>
    /// <returns></returns>
    public static T GetDefaultOrUninitialized()
        => GetDefault() ?? GetUnitialized();
}
