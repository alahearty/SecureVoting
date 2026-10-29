# Demonstration Script

> Phase 10 adds a three-department scenario. Use
> [phase-10-demo-scenario.md](phase-10-demo-scenario.md) for the current university-wide demonstration.

**Audience:** final-year project panel.  
**Duration:** 12-18 minutes.  
**Data:** synthetic only. Do not use real student data or real ballots.

## Before the session

- Start the Docker stack or local API/database.
- Confirm `GET /health` is healthy.
- Confirm the SPA loads at the chosen URL.
- Use a clean demo database or the seeded demonstration workspace.
- Do not display actual passwords, activation tokens, API secrets or infrastructure credentials on the
  projected screen. Use a prepared local account.

## 1. Context and safety statement (1 minute)

Explain the problem: departmental elections need controlled eligibility, one accepted ballot per
student, private choices, accurate counting and reviewable evidence. State clearly that the system is
a monitoring/records platform, not a certified election authority, and that all data shown is synthetic.

## 2. Student activation and login (2 minutes)

1. Open `/activate` (or use an already provisioned demo student).
2. Show the one-time activation flow conceptually without projecting a real token.
3. Sign in as a student and show the student dashboard:
   - eligible election;
   - election status and schedule;
   - candidate/position count;
   - one-vote reminder.

## 3. Ballot privacy design (2 minutes)

Explain the schema on a diagram:

- eligibility is identity-linked;
- entitlement is identity-linked and one-use;
- ballot is anonymous and has no student ID;
- selections reference only the anonymous ballot;
- confirmation reference is hashed on the entitlement, not the ballot;
- results are aggregate snapshots.

Do not claim absolute anonymity. Name timing, privileged DB access and infrastructure as residual risks.

## 4. Cast a ballot (3 minutes)

1. Open the eligible election and select **Cast your ballot**.
2. Select a candidate for each configured position.
3. Show the review screen and explicit final confirmation.
4. Submit and show the confirmation page with a random reference.
5. Return to the election and show that the ballot is recorded and cannot be submitted again.
6. Emphasise that the server, not the disabled button, enforces the one-vote entitlement.

## 5. Administrative and observer workflow (4 minutes)

1. Sign in as Electoral Officer.
2. Show the election workspace: positions, register import summary, candidate approval, lifecycle.
3. Explain that early close/suspend/cancel requires a reason and is audited.
4. Close voting and request result review.
5. Sign in as Observer; show the aggregate-only provisional result, tie handling and verification notes.
6. Sign in as Super Administrator; show publication approval and explain that published versions are
   immutable through the normal workflow.

If time permits, show a recount creates a new provisional version rather than overwriting the old one.

## 6. Audit and integrity (2 minutes)

1. Open the audit trail.
2. Show actor, event, outcome, correlation and hash chain.
3. Run **Verify chain** and show the success message.
4. Explain the limitation: a privileged database administrator can rewrite the whole chain; external
   checkpoints are future work.

## 7. Results and recovery (1 minute)

- Show the published result page, chart, aggregate table, PDF/print actions and CSV export.
- Show the health endpoint.
- Briefly show the documented PostgreSQL backup/restore procedure.
- State that no physical modem, real network, real election or real voter data was used.

## Closing statement

“The software demonstrates server-enforced eligibility and one-vote behaviour, a privacy-conscious
ballot model, controlled result publication and a testable audit trail. It is ready for further
laboratory validation, not a real election. Physical identity verification, independent penetration
testing, institutional approval and an election disputes procedure remain prerequisites.”
