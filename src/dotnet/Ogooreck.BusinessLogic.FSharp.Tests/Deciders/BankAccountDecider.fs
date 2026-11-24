module Deciders.BankAccountDecider

open System
open BankAccount
open BankAccountPrimitives

type OpenBankAccount =
    { BankAccountId: AccountId
      AccountNumber: AccountNumber
      ClientId: ClientId
      CurrencyIsoCode: CurrencyIsoCode
      Now: DateTimeOffset }

type RecordDeposit =
    { Amount: decimal
      CashierId: CashierId
      Now: DateTimeOffset }

type WithdrawCashFromAtm =
    { Amount: decimal
      AtmId: AtmId
      Now: DateTimeOffset }

type CloseBankAccount = { Reason: string; Now: DateTimeOffset }

type Command =
    | OpenBankAccount of OpenBankAccount
    | RecordDeposit of RecordDeposit
    | WithdrawCashFromAtm of WithdrawCashFromAtm
    | CloseBankAccount of CloseBankAccount

let openBankAccount (command: OpenBankAccount) bankAccount : Event =
    match bankAccount with
    | Open _ -> invalidOp "Account is already opened!"
    | Closed _ -> invalidOp "Account is already closed!"
    | Initial ->
        BankAccountOpened
            { BankAccountId = command.BankAccountId
              AccountNumber = command.AccountNumber
              ClientId = command.ClientId
              CurrencyIsoCode = command.CurrencyIsoCode
              CreatedAt = command.Now
              Version = 1 }

let recordDeposit (command: RecordDeposit) bankAccount : Event =
    match bankAccount with
    | Initial -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is closed!"
    | Open state ->
        DepositRecorded
            { BankAccountId = state.Id
              Amount = command.Amount
              CashierId = command.CashierId
              RecordedAt = command.Now
              Version = state.Version + 1L }

let withdrawCashFromAtm (command: WithdrawCashFromAtm) bankAccount : Event =
    match bankAccount with
    | Initial -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is closed!"
    | Open state ->
        if (state.Balance < command.Amount) then
            invalidOp "Not enough money!"

        CashWithdrawnFromAtm
            { BankAccountId = state.Id
              Amount = command.Amount
              AtmId = command.AtmId
              RecordedAt = command.Now
              Version = state.Version + 1L }

let closeBankAccount (command: CloseBankAccount) bankAccount : Event =
    match bankAccount with
    | Initial -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is already closed!"
    | Open state ->
        BankAccountClosed
            { BankAccountId = state.Id
              Reason = command.Reason
              ClosedAt = command.Now
              Version = state.Version + 1L }

let decide command bankAccount =
    match command with
    | OpenBankAccount c -> openBankAccount c bankAccount
    | RecordDeposit c -> recordDeposit c bankAccount
    | WithdrawCashFromAtm c -> withdrawCashFromAtm c bankAccount
    | CloseBankAccount c -> closeBankAccount c bankAccount
