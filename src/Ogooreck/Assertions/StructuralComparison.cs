using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ogooreck.Assertions;

// Implements the default equivalence rules used by Ogooreck before removing
// FluentAssertions 6.12. Event order is the separately selected behavior change.
internal static class StructuralComparison
{
    public static void AssertEquivalent<T>(T actual, T expected, string description) =>
        Assert(Compare(actual, expected, typeof(T), "", [], IsCollection(typeof(T))), description);

    public static void AssertOrderedEquivalent<T>(IReadOnlyList<T> actual, IReadOnlyList<T> expected, string description) =>
        Assert(CompareCollections(actual, expected, typeof(object), "", [], true, true), description);

    private static void Assert(string? failure, string description)
    {
        if (failure is not null)
            throw new AssertionException($"{description}: {failure}");
    }

    private static string? Compare(object? actual, object? expected, Type declaredType, string path,
        List<object> ancestors, bool rootIsCollection)
    {
        // The old engine counts member separators, not collection indexes.
        if (path.Count(c => c == '.') > 10)
            return $"{Display(path)}: maximum recursion depth of 10 was reached.";

        var tracksReference = expected is not null && !UsesValueEquality(expected.GetType());
        if (tracksReference && ancestors.Any(item => ReferenceEquals(item, expected)))
            return $"{Display(path)}: expected graph contains a cyclic reference.";
        if (ReferenceEquals(actual, expected)) return null;
        if (actual is null || expected is null) return Difference(path, actual, expected);

        var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (type == typeof(object)) type = expected.GetType();

        if (tracksReference) ancestors.Add(expected);
        try
        {
            var dictionary = GenericInterfaces(type, typeof(IDictionary<,>)).SingleOrDefault();
            if (dictionary is not null)
                return CompareDictionary(actual, expected, dictionary, path, ancestors, rootIsCollection);

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                if (actual is not IDictionary actualMap || expected is not IDictionary expectedMap)
                    return Difference(path, actual, expected);
                if (actualMap.Count != expectedMap.Count)
                    return CountDifference(path, actualMap.Count, expectedMap.Count);
                foreach (var key in expectedMap.Keys)
                {
                    var failure = Compare(actualMap[key], expectedMap[key], typeof(object),
                        $"{path}[{key}]", ancestors, rootIsCollection);
                    if (failure is not null) return failure;
                }
                return null;
            }

            if (expected is Array { Rank: > 1 } expectedArray)
            {
                if (actual is not Array actualArray || actualArray.Rank != expectedArray.Rank ||
                    Enumerable.Range(0, expectedArray.Rank).Any(i =>
                        actualArray.GetLength(i) != expectedArray.GetLength(i) ||
                        actualArray.GetLowerBound(i) != expectedArray.GetLowerBound(i)))
                    return $"{Display(path)}: array dimensions do not match.";
                return CompareCollections(actualArray, expectedArray, typeof(object), path, ancestors, rootIsCollection, true);
            }

            if (IsCollection(type))
            {
                if (actual is not IEnumerable actualItems || actual is string || expected is not IEnumerable expectedItems)
                    return Difference(path, actual, expected);
                var enumerations = GenericInterfaces(type, typeof(IEnumerable<>));
                if (enumerations.Length > 1)
                    return $"{Display(path)}: expected collection has ambiguous element types.";
                var itemType = enumerations.SingleOrDefault()?.GenericTypeArguments[0] ?? typeof(object);
                return CompareCollections(actualItems, expectedItems, itemType, path, ancestors, rootIsCollection,
                    typeof(IEnumerable<byte>).IsAssignableFrom(type));
            }

            if (type == typeof(string))
                return actual is string text && string.Equals(text, (string)expected, StringComparison.Ordinal)
                    ? null : Difference(path, actual, expected);
            if (type.IsEnum)
                return actual.GetType().IsEnum && Convert.ToDecimal(actual, CultureInfo.InvariantCulture) ==
                    Convert.ToDecimal(expected, CultureInfo.InvariantCulture)
                    ? null : Difference(path, actual, expected);
            if (UsesValueEquality(type))
                return ValuesEqual(actual, expected) ? null : Difference(path, actual, expected);

            var members = Members(type).ToArray();
            if (members.Length == 0 && (path.Length == 0 || rootIsCollection && IsFirstCollectionItem(path)))
                return $"{Display(path)}: no members were found for comparison.";
            foreach (var member in members)
            {
                // Matching can cross property/field boundaries and use an explicit
                // interface implementation, as the previous default matching did.
                var actualMember = FindMember(actual.GetType(), member.Name);
                if (member.DeclaringType?.IsInstanceOfType(actual) == true)
                    actualMember = member;
                if (actualMember is null)
                    return $"{Display(MemberPath(path, member.Name))}: actual value has no matching member.";
                var failure = Compare(Read(actualMember, actual), Read(member, expected), MemberType(member),
                    MemberPath(path, member.Name), ancestors, rootIsCollection);
                if (failure is not null) return failure;
            }
            return null;
        }
        finally
        {
            if (tracksReference) ancestors.RemoveAt(ancestors.Count - 1);
        }
    }

    private static string? CompareDictionary(object actual, object expected, Type expectedContract, string path,
        List<object> ancestors, bool rootIsCollection)
    {
        var keyType = expectedContract.GenericTypeArguments[0];
        var valueType = expectedContract.GenericTypeArguments[1];
        var actualContract = GenericInterfaces(actual.GetType(), typeof(IDictionary<,>))
            .FirstOrDefault(t => t.GenericTypeArguments[0].IsAssignableFrom(keyType));
        if (actualContract is null)
        {
            // The previous generic dictionary assertion also accepts key/value sequences.
            var pairType = GenericInterfaces(actual.GetType(), typeof(IEnumerable<>))
                .Select(t => t.GenericTypeArguments[0])
                .SingleOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) &&
                    t.GenericTypeArguments[0] == keyType);
            if (pairType is null) return Difference(path, actual, expected);
            var actualValueType = pairType.GenericTypeArguments[1];
            actualContract = typeof(IDictionary<,>).MakeGenericType(keyType, actualValueType);
            var map = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, actualValueType))!;
            foreach (var pair in (IEnumerable)actual)
                map.Add(Read(pair.GetType().GetProperty("Key")!, pair)!, Read(pair.GetType().GetProperty("Value")!, pair));
            actual = map;
        }
        var expectedEntries = ((IEnumerable)expected).Cast<object>().ToArray();
        var actualCount = ((IEnumerable)actual).Cast<object>().Count();
        if (actualCount != expectedEntries.Length) return CountDifference(path, actualCount, expectedEntries.Length);
        var containsKey = actualContract.GetMethod("ContainsKey")!;
        var indexer = actualContract.GetProperty("Item")!;
        foreach (var entry in expectedEntries)
        {
            var key = Read(entry.GetType().GetProperty("Key")!, entry);
            if (containsKey.Invoke(actual, [key]) is not true)
                return $"{Display(path)}: missing dictionary key <{key}>.";
            var failure = Compare(indexer.GetValue(actual, [key]), Read(entry.GetType().GetProperty("Value")!, entry),
                valueType, $"{path}[{key}]", ancestors, rootIsCollection);
            if (failure is not null) return failure;
        }
        return null;
    }

    private static string? CompareCollections(IEnumerable actual, IEnumerable expected, Type itemType, string path,
        List<object> ancestors, bool rootIsCollection, bool ordered)
    {
        var actualItems = Items(actual);
        var expectedItems = Items(expected);
        if (actualItems.Length != expectedItems.Length)
            return CountDifference(path, actualItems.Length, expectedItems.Length);
        var used = new bool[actualItems.Length];
        for (var e = 0; e < expectedItems.Length; e++)
        {
            var itemPath = $"{path}[{e}]";
            if (ordered)
            {
                var failure = Compare(actualItems[e], expectedItems[e], itemType, itemPath, ancestors, rootIsCollection);
                if (failure is not null) return failure;
                continue;
            }
            // Preserve the old default's first unmatched equivalent occurrence,
            // including duplicate accounting and overlapping member expectations.
            string? firstFailure = null;
            var match = -1;
            for (var a = 0; a < actualItems.Length; a++)
            {
                if (used[a]) continue;
                var failure = Compare(actualItems[a], expectedItems[e], itemType, itemPath, ancestors, rootIsCollection);
                if (failure is null) { match = a; break; }
                firstFailure ??= failure;
            }
            if (match < 0) return $"{Display(itemPath)}: no unmatched equivalent item. {firstFailure}";
            used[match] = true;
        }
        return null;
    }

    private static object?[] Items(IEnumerable items)
    {
        try { return items.Cast<object?>().ToArray(); }
        catch (InvalidOperationException) when (items.GetType().IsGenericType &&
            items.GetType().GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>))
        { return []; }
    }

    private static bool ValuesEqual(object actual, object expected)
    {
        if (actual.Equals(expected)) return true;
        if (actual.GetType() == expected.GetType() || !IsNumber(actual) || !IsNumber(expected)) return false;
        return ConvertsExactly(actual, expected) && ConvertsExactly(expected, actual);
    }

    private static bool ConvertsExactly(object source, object target)
    {
        try
        {
            var converted = Convert.ChangeType(source, target.GetType(), CultureInfo.InvariantCulture);
            return converted.Equals(target) && source.Equals(Convert.ChangeType(converted, source.GetType(), CultureInfo.InvariantCulture));
        }
        catch (Exception e) when (e is InvalidCastException or OverflowException or FormatException) { return false; }
    }

    private static bool IsNumber(object value) => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    private static bool IsCollection(Type type) => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    private static Type[] GenericInterfaces(Type type, Type definition) => type.GetInterfaces().Prepend(type)
        .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == definition).Distinct().ToArray();
    private static bool UsesValueEquality(Type type)
    {
        var equals = type.GetMethod(nameof(object.Equals), [typeof(object)]);
        var overrides = equals is not null && equals.GetBaseDefinition().DeclaringType != equals.DeclaringType;
        var record = type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null ||
            type.IsValueType && type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance,
                [typeof(StringBuilder)]) is not null &&
            type.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static, [type, type])?
                .IsDefined(typeof(CompilerGeneratedAttribute), false) == true;
        return overrides && !record && !(type.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
            type.Name.Contains("AnonymousType", StringComparison.Ordinal)) &&
            !(type.IsGenericType && typeof(ITuple).IsAssignableFrom(type)) &&
            !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
    }

    private static IEnumerable<MemberInfo> Members(Type type)
    {
        var types = type.IsInterface ? type.GetInterfaces().Prepend(type) : Hierarchy(type);
        var properties = types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.GetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0).DistinctBy(p => p.Name);
        var fields = types.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .DistinctBy(f => f.Name);
        return properties.Cast<MemberInfo>().Concat(fields);
    }

    private static MemberInfo? FindMember(Type type, string name)
    {
        foreach (var t in Hierarchy(type))
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var property = t.GetProperty(name, flags);
            if (property?.GetMethod is not null && property.GetIndexParameters().Length == 0) return property;
        }
        return Hierarchy(type).Select(t => t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.DeclaredOnly)).FirstOrDefault(f => f is not null);
    }

    private static IEnumerable<Type> Hierarchy(Type type)
    {
        for (Type? t = type; t is not null; t = t.BaseType) yield return t;
    }
    private static Type MemberType(MemberInfo member) => member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
    private static object? Read(MemberInfo member, object value) => member is PropertyInfo p ? p.GetValue(value) : ((FieldInfo)member).GetValue(value);
    private static bool IsFirstCollectionItem(string path) => path.StartsWith('[') && path.EndsWith(']') && path.Count(c => c == '[') == 1;
    private static string MemberPath(string path, string member) => path.Length == 0 ? member : $"{path}.{member}";
    private static string Display(string path) => path.Length == 0 ? "$" : path.StartsWith('[') ? $"${path}" : $"$.{path}";
    private static string CountDifference(string path, int actual, int expected) => $"{Display(path)}: expected {expected} items, but found {actual}.";
    private static string Difference(string path, object? actual, object? expected) =>
        $"{Display(path)}: expected <{expected ?? "null"}>, but found <{actual ?? "null"}>.";
}
