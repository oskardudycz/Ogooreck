using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;

namespace Ogooreck.Tests;

// Expected outcomes are characterized against the previously used FA 6.12.0 DLL
// by an external audit harness. The repository has no FluentAssertions reference.
internal static class ComparisonCases
{
    public static readonly Dictionary<string, (bool Equivalent, Func<(object? Actual, object? Expected)> Values)> All = new()
    {
        ["both null"] = (true, () => (null, null)),
        ["actual null"] = (false, () => (null, "a")),
        ["expected null"] = (false, () => ("a", null)),
        ["equal strings"] = (true, () => ("abc", "abc")),
        ["string case"] = (false, () => ("abc", "ABC")),
        ["string whitespace"] = (false, () => ("abc ", "abc")),
        ["string is not a collection"] = (false, () => (new[] { 'a', 'b' }, "ab")),
        ["collection is not a string"] = (false, () => ("ab", new[] { 'a', 'b' })),
        ["number is not a string"] = (false, () => (1, "1")),
        ["integer equality"] = (true, () => (1, 1)),
        ["integer mismatch"] = (false, () => (1, 2)),
        ["int and long"] = (true, () => (1, 1L)),
        ["decimal and double"] = (true, () => (1m, 1d)),
        ["decimal and float"] = (true, () => (0.1m, 0.1f)),
        ["signed and unsigned"] = (true, () => (1, 1UL)),
        ["numeric overflow"] = (false, () => (-1L, ulong.MaxValue)),
        ["lossy numeric conversion"] = (false, () => (16777217, 16777216f)),
        ["fractional numeric mismatch"] = (false, () => (1.5m, 2)),
        ["NaN numeric widths"] = (true, () => (double.NaN, float.NaN)),
        ["infinity numeric widths"] = (true, () => (double.PositiveInfinity, float.PositiveInfinity)),
        ["bool is not numeric"] = (false, () => (true, 1)),
        ["char is not numeric"] = (false, () => ('a', 97)),
        ["enum equality"] = (true, () => (First.One, First.One)),
        ["enum underlying values"] = (true, () => (First.One, Second.RenamedOne)),
        ["enum mismatch"] = (false, () => (First.One, Second.Two)),
        ["enum is not number"] = (false, () => (1, First.One)),
        ["number is not enum"] = (false, () => (First.One, 1)),
        ["enum is not name"] = (false, () => ("One", First.One)),
        ["same DTO"] = (true, () => (new Dto { Name = "a" }, new Dto { Name = "a" })),
        ["DTO member mismatch"] = (false, () => (new Dto { Name = "a" }, new Dto { Name = "b" })),
        ["extra actual member"] = (true, () => (new { Name = "a", Extra = 1 }, new { Name = "a" })),
        ["missing actual member"] = (false, () => (new { Name = "a" }, new { Name = "a", Extra = 1 })),
        ["property to field"] = (true, () => (new Field { Value = 1 }, new { Value = 1 })),
        ["field to property"] = (true, () => (new { Value = 1 }, new Field { Value = 1 })),
        ["property to private property"] = (true, () => (new PrivateMember(1), new { Value = 1 })),
        ["private members ignored"] = (true, () => (new PrivateDetails(1), new PrivateDetails(2))),
        ["indexers ignored"] = (true, () => (new Indexed(1), new Indexed(2))),
        ["nested null"] = (true, () => (new Dto(), new Dto())),
        ["nested null mismatch"] = (false, () => (new Dto(), new Dto { Name = "a" })),
        ["custom Equals accepts"] = (true, () => (new Value("id", 1), new Value("id", 2))),
        ["custom Equals rejects"] = (false, () => (new Value("id", 1), new Value("other", 1))),
        ["record member collections"] = (true, () => (new Record([1, 2]), new Record([2, 1]))),
        ["record struct member collections"] = (true, () => (new RecordStruct([1, 2]), new RecordStruct([2, 1]))),
        ["anonymous member collections"] = (true, () => (new { Items = new[] { 1, 2 } }, new { Items = new[] { 2, 1 } })),
        ["struct value equality"] = (false, () => (new Struct { Items = [1, 2] }, new Struct { Items = [2, 1] })),
        ["tuple members"] = (true, () => (Tuple.Create(new[] { 1, 2 }), Tuple.Create(new[] { 2, 1 }))),
        ["value tuple members"] = (true, () => ((Items: new[] { 1, 2 }, Number: 1), (Items: new[] { 2, 1 }, Number: 1))),
        ["key value pair members"] = (true, () => (new KeyValuePair<string, int[]>("a", [1, 2]), new KeyValuePair<string, int[]>("a", [2, 1]))),
        ["declared member type"] = (true, () => (new BaseBox { Item = new Derived { Id = 1, Detail = 1 } }, new BaseBox { Item = new Derived { Id = 1, Detail = 2 } })),
        ["declared collection element type"] = (true, () => (new CollectionBox { Items = [new Derived { Id = 1, Detail = 1 }] }, new CollectionBox { Items = [new Derived { Id = 1, Detail = 2 }] })),
        ["object member runtime type"] = (false, () => (new ObjectBox { Item = new Derived { Id = 1, Detail = 1 } }, new ObjectBox { Item = new Derived { Id = 1, Detail = 2 } })),
        ["explicit interface member"] = (true, () => (new InterfaceBox { Item = new Explicit() }, new InterfaceBox { Item = new Implicit() })),
        ["unordered array"] = (true, () => (new[] { 1, 2, 3 }, new[] { 3, 1, 2 })),
        ["different collection types"] = (true, () => (new List<int> { 1, 2 }, new long[] { 2, 1 })),
        ["collection count mismatch"] = (false, () => (new[] { 1, 2 }, new[] { 1 })),
        ["collection value mismatch"] = (false, () => (new[] { 1, 2 }, new[] { 1, 3 })),
        ["duplicates retained"] = (true, () => (new[] { 1, 1, 2 }, new[] { 2, 1, 1 })),
        ["duplicate count mismatch"] = (false, () => (new[] { 1, 1, 2 }, new[] { 2, 2, 1 })),
        ["empty collections"] = (true, () => (Array.Empty<int>(), new List<int>())),
        ["collection null mismatch"] = (false, () => (null, Array.Empty<int>())),
        ["null collection elements"] = (true, () => (new string?[] { "a", null }, new string?[] { null, "a" })),
        ["null collection element mismatch"] = (false, () => (new string?[] { "a", null }, new[] { "a", "b" })),
        ["jagged arrays"] = (true, () => (new[] { new[] { 1, 2 }, new[] { 3 } }, new[] { new[] { 3 }, new[] { 2, 1 } })),
        ["ordered byte array"] = (false, () => (new byte[] { 1, 2 }, new byte[] { 2, 1 })),
        ["ordered byte list"] = (false, () => (new List<byte> { 1, 2 }, new List<byte> { 2, 1 })),
        ["unordered boxed bytes"] = (true, () => (new object[] { (byte)1, (byte)2 }, new object[] { (byte)2, (byte)1 })),
        ["multidimensional arrays"] = (true, () => (new[,] { { 1, 2 }, { 3, 4 } }, new[,] { { 1, 2 }, { 3, 4 } })),
        ["multidimensional array order"] = (false, () => (new[,] { { 1, 2 }, { 3, 4 } }, new[,] { { 2, 1 }, { 3, 4 } })),
        ["multidimensional array shape"] = (false, () => (new[,] { { 1, 2 }, { 3, 4 } }, new[,] { { 1, 2, 3, 4 } })),
        ["multidimensional array rank"] = (false, () => (new[] { 1, 2 }, new[,] { { 1, 2 } })),
        ["dictionary order"] = (true, () => (new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 })),
        ["dictionary missing key"] = (false, () => (new Dictionary<string, int> { ["a"] = 1 }, new Dictionary<string, int> { ["b"] = 1 })),
        ["dictionary extra entry"] = (false, () => (new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, new Dictionary<string, int> { ["a"] = 1 })),
        ["dictionary value mismatch"] = (false, () => (new Dictionary<string, int> { ["a"] = 1 }, new Dictionary<string, int> { ["a"] = 2 })),
        ["dictionary key value association"] = (false, () => (new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, new Dictionary<string, int> { ["a"] = 2, ["b"] = 1 })),
        ["dictionary value numeric equality"] = (true, () => (new Dictionary<string, int> { ["a"] = 1 }, new Dictionary<string, long> { ["a"] = 1 })),
        ["dictionary key types"] = (false, () => (new Dictionary<int, string> { [1] = "a" }, new Dictionary<long, string> { [1] = "a" })),
        ["dictionary subject key comparer"] = (true, () => (new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 }, new Dictionary<string, int> { ["a"] = 1 })),
        ["dictionary expectation comparer does not override subject"] = (false, () => (new Dictionary<string, int> { ["A"] = 1 }, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["a"] = 1 })),
        ["dictionary nested collections"] = (true, () => (new Dictionary<string, int[]> { ["a"] = [1, 2] }, new Dictionary<string, int[]> { ["a"] = [2, 1] })),
        ["dictionary accepts key value sequence"] = (true, () => (new[] { new KeyValuePair<string, int>("a", 1) }, new Dictionary<string, int> { ["a"] = 1 })),
        ["dictionary sequence numeric values"] = (true, () => (new[] { new KeyValuePair<string, int>("a", 1) }, new Dictionary<string, long> { ["a"] = 1 })),
        ["dictionary assignable key types"] = (true, () => (new Dictionary<object, int> { ["a"] = 1 }, new Dictionary<string, int> { ["a"] = 1 })),
        ["non generic dictionary"] = (true, () => (new Hashtable { ["a"] = 1, ["b"] = 2 }, new Hashtable { ["b"] = 2, ["a"] = 1 })),
        ["default immutable arrays"] = (true, () => (default(ImmutableArray<int>), ImmutableArray<int>.Empty)),
        ["immutable array order"] = (true, () => (ImmutableArray.Create(1, 2), ImmutableArray.Create(2, 1))),
        ["overlapping matches use first available"] = (false, () => (new object[] { new { Name = "a", Id = 1 }, new { Name = "a", Id = 2 } }, new object[] { new { Name = "a" }, new { Name = "a", Id = 1 } })),
        ["root without members"] = (false, () => (new Empty(), new Empty())),
        ["nested without members"] = (true, () => (new { Item = new Empty() }, new { Item = new Empty() })),
        ["same reference cycle"] = (true, () => { var n = Cycle(); return (n, n); }),
        ["distinct cycles"] = (false, () => (Cycle(), Cycle())),
        ["collection cycles"] = (false, () => { var a = new ArrayList(); a.Add(a); var b = new ArrayList(); b.Add(b); return (a, b); }),
        ["shared siblings are not cycles"] = (true, SharedSiblings),
        ["graph at depth limit"] = (true, () => (Deep(10), Deep(10))),
        ["graph beyond depth limit"] = (false, () => (Deep(11), Deep(11))),
        ["deep collection indexes do not add member depth"] = (true, () => (NestedArrays(20), NestedArrays(20))),
        ["same JSON element value"] = (true, () => { var j = Json("{\"a\":1}"); return (j, j); }),
        ["separate JSON documents use value equality"] = (false, () => (Json("{\"a\":1}"), Json("{\"a\":1}"))),
        ["JSON content mismatch"] = (false, () => (Json("{\"a\":1}"), Json("{\"a\":2}"))),
        ["datetime equality"] = (true, () => (new DateTime(2020, 1, 1), new DateTime(2020, 1, 1))),
        ["datetime mismatch"] = (false, () => (new DateTime(2020, 1, 1), new DateTime(2020, 1, 2))),
        ["datetimeoffset instant equality"] = (true, () => (new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2020, 1, 1, 1, 0, 0, TimeSpan.FromHours(1)))),
        ["URI value equality"] = (true, () => (new Uri("https://example.com/a"), new Uri("https://EXAMPLE.com/a"))),
        ["large reversed collection"] = (true, () => (Enumerable.Range(0, 100).ToArray(), Enumerable.Range(0, 100).Reverse().ToArray()))
    };

    private static JsonElement Json(string text) { using var d = JsonDocument.Parse(text); return d.RootElement.Clone(); }
    private static Node Cycle() { var n = new Node(); n.Next = n; return n; }
    private static Node Deep(int edges) { var root = new Node(); var n = root; for (var i = 0; i < edges; i++) n = n.Next = new Node(); return root; }
    private static object NestedArrays(int depth) { object value = 1; for (var i = 0; i < depth; i++) value = new[] { value }; return value; }
    private static (object?, object?) SharedSiblings()
    {
        var a = new Node(); var b = new Node();
        return (new { First = a, Second = a }, new { First = b, Second = b });
    }

    private enum First { One = 1 }
    private enum Second : long { RenamedOne = 1, Two = 2 }
    private sealed class Dto { public string? Name { get; init; } }
    private sealed class Field { public int Value; }
    private sealed class PrivateMember(int value) { private int Value { get; } = value; }
    private sealed class PrivateDetails(int value) { public int Value => 1; private int Detail { get; } = value; }
    private sealed class Indexed(int value) { public int Id => 1; public int this[int index] => value; }
    private sealed class Value(string id, int detail)
    {
        public string Id { get; } = id;
        public int Detail { get; } = detail;
        public override bool Equals(object? other) => other is Value v && v.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
    private record Record(int[] Items);
    private readonly record struct RecordStruct(int[] Items);
    private struct Struct { public int[] Items; }
    private class Base { public int Id { get; init; } }
    private sealed class Derived : Base { public int Detail { get; init; } }
    private sealed class BaseBox { public Base? Item { get; init; } }
    private sealed class CollectionBox { public List<Base> Items { get; init; } = []; }
    private sealed class ObjectBox { public object? Item { get; init; } }
    private interface IContract { int Value { get; } }
    private sealed class Explicit : IContract { int IContract.Value => 1; }
    private sealed class Implicit : IContract { public int Value => 1; }
    private sealed class InterfaceBox { public IContract? Item { get; init; } }
    private sealed class Empty;
    private sealed class Node { public int Value => 1; public Node? Next { get; set; } }
}
