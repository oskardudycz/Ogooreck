module Deciders.BankAccountPrimitives

open System

[<Struct>]
type AccountId = AccountId of Guid

module AccountId =
    let newId () = AccountId(Guid.NewGuid())

[<Struct>]
type ClientId = ClientId of Guid

module ClientId =
    let newId () = ClientId(Guid.NewGuid())

[<Struct>]
type AtmId = AtmId of Guid

module AtmId =
    let newId () = AtmId(Guid.NewGuid())

[<Struct>]
type CashierId = CashierId of Guid

module CashierId =
    let newId () = CashierId(Guid.NewGuid())

[<Struct>]
type CurrencyCode = CurrencyCode of string

module CurrencyCode =
    let newCode code = CurrencyCode(code)

[<Struct>]
type AccountNumber = AccountNumber of string

module AccountNumber =
    let newNumber number = AccountNumber(number)
