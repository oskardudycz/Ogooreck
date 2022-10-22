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

let recordDeposit now (command: RecordDeposit) (bankAccount: BankAccount) =
    match bankAccount with
    | NotInitialised _ -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is closed!"
    | Open openBankAccount ->
        { BankAccountId = openBankAccount.Id
          Amount = command.Amount
          CashierId = command.CashierId
          RecordedAt = now ()
          Version = openBankAccount.Version + 1L }
        |> DepositRecorded

let withdrawCashFromATM now (command: WithdrawCashFromATM) (bankAccount: BankAccount) =
    match bankAccount with
    | NotInitialised _ -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is closed!"
    | Open openBankAccount ->
        if (openBankAccount.Balance < command.Amount) then
            invalidOp "Not enough money!"

        { BankAccountId = openBankAccount.Id
          Amount = command.Amount
          ATMId = command.AtmId
          RecordedAt = now ()
          Version = openBankAccount.Version + 1L }
        |> CashWithdrawnFromATM

let closeBankAccount now (command: CloseBankAccount) (bankAccount: BankAccount) =
    match bankAccount with
    | NotInitialised _ -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is already closed!"
    | Open openBankAccount ->
        { BankAccountId = openBankAccount.Id
          Reason = command.Reason
          ClosedAt = now ()
          Version = openBankAccount.Version + 1L }
        |> BankAccountClosed

let decide now command bankAccount =
    match command with
    | OpenBankAccount c -> openBankAccount now c
    | RecordDeposit c -> recordDeposit now c bankAccount
    | WithdrawCashFromATM c -> withdrawCashFromATM now c bankAccount
    | CloseBankAccount c -> closeBankAccount now c bankAccount
