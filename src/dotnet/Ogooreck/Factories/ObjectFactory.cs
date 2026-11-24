using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace Ogooreck.Factories;

/// <summary>
/// Creates the value a specification starts from when a scenario has no <c>Given</c> state: an empty string for <c>string</c>,
/// a new instance when the type has a default constructor (public or not), and otherwise an uninitialized instance, so records without one still work.
/// </summary>
public static class ObjectFactory<T>
{
    /// <summary>
    /// Creates the starting state of type <typeparamref name="T"/>. It is the default for <c>Decider.For</c> when you give no <c>getInitialState</c>.
    /// </summary>
    public static readonly Func<T> GetDefaultOrUninitialized = Creator();

    private static Func<T> Creator()
    {
        var t = typeof(T);
        if (t == typeof(string))
            return Expression.Lambda<Func<T>>(Expression.Constant(string.Empty)).Compile();

        if (t.HasDefaultConstructor())
            return Expression.Lambda<Func<T>>(Expression.New(t)).Compile();

        return () => (T)RuntimeHelpers.GetUninitializedObject(t);
    }
}

/// <summary>
/// Reflection helpers behind <see cref="ObjectFactory{T}"/>.
/// </summary>
public static class ObjectFactory
{
    /// <summary>
    /// Tells whether <paramref name="t"/> can be created without arguments: any value type, or a class with a parameterless constructor of any visibility.
    /// </summary>
    /// <example>
    /// <code>
    /// typeof(Guid).HasDefaultConstructor() // true
    /// </code>
    /// </example>
    public static bool HasDefaultConstructor(this Type t)
    {
        return t.IsValueType || t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null) != null;
    }
}
