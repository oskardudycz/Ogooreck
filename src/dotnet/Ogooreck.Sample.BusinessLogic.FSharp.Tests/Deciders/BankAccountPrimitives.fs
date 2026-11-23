module Deciders.BankAccountPrimitives

open System
open FSharp.UMX

type AccountId = Guid<accountId>
and [<Measure>] accountId

module AccountId =
    let parse (value: Guid) : AccountId = UMX.tag value
    let newId () = Guid.NewGuid >> parse

type ClientId = Guid<clientId>
and [<Measure>] clientId

module ClientId =
    let parse (value: Guid) : ClientId = UMX.tag value
    let newId = Guid.NewGuid >> parse

type AtmId = Guid<atmId>
and [<Measure>] atmId

module AtmId =
    let parse (value: Guid) : AtmId = UMX.tag value
    let newId = Guid.NewGuid >> parse

type CashierId = Guid<cashierId>
and [<Measure>] cashierId

module CashierId =
    let parse (value: Guid) : CashierId = UMX.tag value
    let newId = Guid.NewGuid >> parse

type CurrencyIsoCode = string<currencyIsoCode>
and [<Measure>] currencyIsoCode

module CurrencyIsoCode =
    let parse (value: string) : CurrencyIsoCode = UMX.tag value


type AccountNumber = string<accountNumber>
and [<Measure>] accountNumber

module AccountNumber =
    let parse (value: string) : AccountNumber = UMX.tag value
