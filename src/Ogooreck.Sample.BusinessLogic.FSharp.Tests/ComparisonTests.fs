module ComparisonTests

open System
open Ogooreck.Assertions
open Ogooreck.BusinessLogic
open Xunit

type Event =
    | Added of string * int list
    | Removed of string * int list

let result events =
    ThenDeciderSpecificationBuilder<Event, int>(lazy (TestResult<int, Event>(0, events)))

[<Fact>]
let ``union payloads follow their custom value equality`` () =
    let actual = result [| Added("item", [ 1; 2 ]) |]
    actual.Then(Added("item", [ 1; 2 ]))
    |> ignore

    Assert.Throws<AssertionException>(Action(fun () -> actual.Then(Added("item", [ 2; 1 ])) |> ignore))
    |> ignore

[<Fact>]
let ``different union cases and payloads fail`` () =
    let actual = result [| Added("item", [ 1; 2 ]) |]

    Assert.Throws<AssertionException>(Action(fun () -> actual.Then(Removed("item", [ 1; 2 ])) |> ignore))
    |> ignore

    Assert.Throws<AssertionException>(Action(fun () -> actual.Then(Added("other", [ 1; 2 ])) |> ignore))
    |> ignore
