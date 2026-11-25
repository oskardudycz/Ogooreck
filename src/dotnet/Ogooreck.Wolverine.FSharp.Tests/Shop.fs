module Shop

open System
open System.Net.Http.Json
open Ogooreck.API
open Ogooreck.Wolverine
open Xunit
open type ApiSpecification

let wolverine = WolverinePlugin()

type Shop() =
    inherit
        ApiSpecification<Program>(fun spec ->
            spec.Use(wolverine).Deadline(TimeSpan.FromSeconds 30.0))

let addProduct cartId product =
    Request(fun client ->
        client.PostAsJsonAsync(
            $"/carts/{cartId}/products",
            {| product = product |},
            TestContext.Current.CancellationToken
        ))

let placeOrder cartId =
    Request(fun client ->
        client.PostAsync(
            $"/carts/{cartId}/orders",
            null,
            TestContext.Current.CancellationToken
        ))
