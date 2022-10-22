module Deciders.BankAccountDecider

open System
open BankAccount

type OpenBankAccount =
    { BankAccountId: Guid
      AccountNumber: string
      ClientId: Guid
      CurrencyISOCode: string }

type RecordDeposit = { Amount: decimal; CashierId: Guid }
type WithdrawCashFromATM = { Amount: decimal; AtmId: Guid }
type CloseBankAccount = { Reason: string }

type Command =
    | OpenBankAccount of OpenBankAccount
    | RecordDeposit of RecordDeposit
    | WithdrawCashFromATM of WithdrawCashFromATM
    | CloseBankAccount of CloseBankAccount

let openBankAccount now command =
    { BankAccountId = command.BankAccountId
      AccountNumber = command.AccountNumber
      ClientId = command.ClientId
      CurrencyISOCode = command.CurrencyISOCode
      CreatedAt = now ()
      Version = 1 }
    |> BankAccountOpened

let recordDeposit now (command: RecordDeposit, bankAccount) =
    if (bankAccount.Status = BankAccountStatus.Closed) then
        invalidOp "Account is closed!"

    { BankAccountId = bankAccount.Id
      Amount = command.Amount
      CashierId = command.CashierId
      RecordedAt = now ()
      Version = bankAccount.Version + 1L }
    |> DepositRecorded

let withdrawCashFromATM now (command, bankAccount) =
    if (bankAccount.Status = BankAccountStatus.Closed) then
        invalidOp "Account is closed!"

    if (bankAccount.Balance < command.Amount) then
        invalidOp "Not enough money!"

    { BankAccountId = bankAccount.Id
      Amount = command.Amount
      ATMId = command.AtmId
      RecordedAt = now ()
      Version = bankAccount.Version + 1L }
    |> CashWithdrawnFromATM

let closeBankAccount now (command, bankAccount) =
    if (bankAccount.Status = BankAccountStatus.Closed) then
        invalidOp "Account is already closed!"

    { BankAccountId = bankAccount.Id
      Reason = command.Reason
      ClosedAt = now ()
      Version = bankAccount.Version + 1L }
    |> BankAccountClosed

let decide now command bankAccount =
    match command with
    | OpenBankAccount c -> openBankAccount now c
    | RecordDeposit c -> recordDeposit now (c, bankAccount)
    | WithdrawCashFromATM c -> withdrawCashFromATM now (c, bankAccount)
    | CloseBankAccount c -> closeBankAccount now (c, bankAccount)
