# Use-Case, Activity and Sequence Diagrams

## Use-case diagram

```mermaid
flowchart LR
    Student([Student / Voter])
    Candidate([Candidate])
    Officer([Electoral Officer])
    Supervisor([Super Administrator])
    Observer([Independent Observer])

    Student --> UC1[Sign in / activate account]
    Student --> UC2[View eligible elections]
    Student --> UC3[Review candidates and rules]
    Student --> UC4[Cast one anonymous ballot]
    Student --> UC5[Receive non-revealing confirmation]
    Student --> UC6[View published results]

    Candidate --> UC7[Submit nomination and manifesto]
    Candidate --> UC8[Manage own candidate profile/photo]

    Officer --> UC9[Create/configure election]
    Officer --> UC10[Import voter register]
    Officer --> UC11[Approve/reject/disqualify candidates]
    Officer --> UC12[Schedule/open/close/suspend/cancel]
    Officer --> UC13[Request result review]
    Officer --> UC14[Review aggregate turnout]

    Observer --> UC15[Review aggregate results]
    Observer --> UC16[Verify result snapshot]
    Observer --> UC17[Inspect and verify audit trail]

    Supervisor --> UC18[Manage users and roles]
    Supervisor --> UC19[Publish verified result]
    Supervisor --> UC20[Request authorised recount]
    Supervisor --> UC17
    Supervisor --> UC18
    Supervisor --> UC21[Review settings and system activity]
```

## Ballot-submission activity diagram

```mermaid
flowchart TD
    A[Student signs in] --> B{Active student profile?}
    B -- No --> X1[Deny]
    B -- Yes --> C{Eligible for election?}
    C -- No --> X1
    C -- Yes --> D{Election Open and server within window?}
    D -- No --> X2[Reject with conflict]
    D -- Yes --> E[Load approved positions and candidates]
    E --> F{Every position valid, candidate approved, selection limit met?}
    F -- No --> X3[Reject validation; no ballot]
    F -- Yes --> G[Start transaction and lock election row]
    G --> H[Conditionally consume entitlement WHERE consumed IS NULL]
    H --> I{One row updated?}
    I -- No --> X4[Reject already voted/concurrent; rollback]
    I -- Yes --> J[Insert anonymous ballot + selections/abstentions]
    J --> K[Insert internal receipt]
    K --> L[Append audit event without choices]
    L --> M[Commit]
    M --> N[Return confirmation reference]
```

## Ballot submission sequence

```mermaid
sequenceDiagram
    participant V as Student browser
    participant A as API
    participant DB as PostgreSQL
    V->>A: POST /api/student/elections/{id}/ballots + CSRF
    A->>A: Cookie auth + Student policy + DTO validation
    A->>DB: BEGIN
    A->>DB: Lock election row FOR UPDATE
    A->>DB: Verify eligibility, entitlement, status, trusted time
    A->>A: Validate positions, approved candidates, limits, abstentions
    A->>DB: UPDATE entitlement SET ConsumedAt WHERE ConsumedAt IS NULL
    alt entitlement already consumed
        DB-->>A: 0 rows
        A->>DB: ROLLBACK
        A-->>V: 409 already voted/concurrent
    else entitlement consumed
        A->>DB: INSERT Ballot (random ID, no identity)
        A->>DB: INSERT selections/abstentions + internal receipt
        A->>DB: Append hash-chained audit event (no choices)
        A->>DB: COMMIT
        A-->>V: 201 confirmation reference only
    end
```

## Result publication sequence

```mermaid
sequenceDiagram
    participant O as Electoral Officer
    participant A as API
    participant DB as PostgreSQL
    participant R as Observer
    participant S as Super Administrator
    O->>A: POST results/review
    A->>DB: Lock election, verify Closed
    A->>DB: Compute aggregate snapshot from accepted ballots
    A->>DB: Set UnderReview + audit
    A-->>O: provisional result
    R->>A: POST results/{resultId}/verify + notes
    A->>DB: Verify latest snapshot + record observer approval
    A-->>R: Verified snapshot
    S->>A: POST results/{resultId}/publish + notes
    A->>DB: Verify observer approval + latest version
    A->>DB: Mark earlier published version Superseded
    A->>DB: Mark result Published; set election Published; audit
    A-->>S: Published result
```

## Privacy boundary diagram

```mermaid
flowchart LR
    U[ApplicationUser] --> S[Student]
    S --> E[VoterEligibility]
    E --> T[VoterEntitlement]
    T -. no ballot id .-> B[Ballot]
    B --> BS[BallotSelections]
    B --> BA[BallotAbstentions]
    B --> BR[BallotReceipt]
    B --> RS[ElectionResult snapshots]
    T --> C[Confirmation reference hash]
    style T fill:#fff4d5,stroke:#a87a1c
    style B fill:#eaf7ef,stroke:#185f40
```

The dotted relationship is deliberately **not** a database relation: the entitlement and anonymous
ballot are only joined by the submission transaction, not by a stored voter key.
