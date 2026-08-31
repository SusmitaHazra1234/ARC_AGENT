-- Reference schema for Azure SQL (not applied automatically).
-- Authoritative transactional store per Problem Statement.

CREATE TABLE dbo.Dealer (
    Urn nvarchar(128) NOT NULL PRIMARY KEY,
    SapCode nvarchar(64) NULL,
    PortalId nvarchar(64) NULL,
    Depot nvarchar(64) NULL,
    Region nvarchar(64) NULL,
    CoveringTsi nvarchar(128) NULL,
    UnderInsolvencyMoratorium bit NOT NULL CONSTRAINT DF_Dealer_Moratorium DEFAULT (0)
);

CREATE TABLE dbo.LedgerPosition (
    Id bigint IDENTITY PRIMARY KEY,
    DealerUrn nvarchar(128) NOT NULL,
    DocumentType nvarchar(64) NOT NULL,
    DueDate date NOT NULL,
    PostedOn date NOT NULL,
    Amount decimal(18,2) NOT NULL,
    Currency char(3) NOT NULL CONSTRAINT DF_Ledger_Currency DEFAULT ('INR'),
    SourceSystem nvarchar(64) NOT NULL,
    SourceTable nvarchar(128) NOT NULL,
    SourceKey nvarchar(128) NOT NULL
);

CREATE TABLE dbo.SecurityCheque (
    DealerUrn nvarchar(128) NOT NULL,
    ChequeNumber nvarchar(64) NOT NULL,
    Micr nvarchar(64) NULL,
    Amount decimal(18,2) NOT NULL,
    Currency char(3) NOT NULL CONSTRAINT DF_Cheque_Currency DEFAULT ('INR'),
    Status nvarchar(32) NOT NULL,
    DepositDate date NULL,
    ValidityEnd date NULL,
    ExtractionConfidence decimal(4,3) NULL,
    CONSTRAINT PK_SecurityCheque PRIMARY KEY (DealerUrn, ChequeNumber)
);

CREATE TABLE dbo.ChequeReturnMemo (
    DealerUrn nvarchar(128) NOT NULL,
    ChequeNumber nvarchar(64) NOT NULL,
    ReturnReasonCode nvarchar(64) NOT NULL,
    MemoIssueDate date NOT NULL,
    MemoReceivedDate date NOT NULL,
    ExtractionConfidence decimal(4,3) NULL,
    CONSTRAINT PK_ChequeReturnMemo PRIMARY KEY (DealerUrn, ChequeNumber, MemoReceivedDate)
);

CREATE TABLE dbo.GateDecision (
    Id bigint IDENTITY PRIMARY KEY,
    CycleId nvarchar(64) NOT NULL,
    DealerUrn nvarchar(128) NOT NULL,
    GateId nvarchar(64) NOT NULL,
    ActorUpn nvarchar(256) NOT NULL,
    ActorRole nvarchar(64) NOT NULL,
    Decision nvarchar(32) NOT NULL,
    Reason nvarchar(512) NOT NULL,
    RecommendedAction nvarchar(128) NULL,
    DecidedUtc datetimeoffset NOT NULL,
    CorrelationId nvarchar(64) NOT NULL,
    WasOverride bit NOT NULL,
    CONSTRAINT UQ_GateDecision_Idempotent UNIQUE (CycleId, DealerUrn, GateId, CorrelationId)
);

CREATE TABLE dbo.LegalCase (
    DealerUrn nvarchar(128) NOT NULL PRIMARY KEY,
    CaseReference nvarchar(128) NULL,
    CompletenessScore decimal(5,4) NOT NULL,
    GapsJson nvarchar(max) NULL
);

CREATE TABLE dbo.RecoveryCaseIndex (
    CycleId nvarchar(64) NOT NULL,
    DealerUrn nvarchar(128) NOT NULL,
    Status nvarchar(64) NOT NULL,
    CorrelationId nvarchar(64) NOT NULL,
    WaitingGate nvarchar(64) NULL,
    UpdatedUtc datetimeoffset NOT NULL,
    CONSTRAINT PK_RecoveryCaseIndex PRIMARY KEY (CycleId, DealerUrn)
);
