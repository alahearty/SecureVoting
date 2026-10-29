# Defence Presentation Outline

## Slide 1 - Title

Design and Implementation of a Secure Electronic Voting System for Departmental Elections.  
Name, matriculation number, department, supervisor, date.

## Slide 2 - Problem

- Manual/paper or ad hoc online voting creates eligibility, duplicate-vote and record-keeping risks.
- Students need a clear private workflow; officers need controlled administration.
- Results need aggregate verification and a defensible evidence trail.
- Existing simple forms often rely on frontend controls and do not prove one-vote behaviour.

## Slide 3 - Aim and objectives

- Secure authentication and controlled student provisioning.
- Server-enforced one-vote entitlement.
- Privacy-conscious anonymous ballot.
- Controlled election lifecycle and candidate approval.
- Aggregate, verified, versioned results.
- Tamper-evident audit and reproducible tests.

## Slide 4 - Scope

In scope: departmental election administration, voter eligibility, ballot submission, tallying,
publication, dashboards, audit and deployment.  
Out of scope: national elections, biometric identity, cryptographic end-to-end verifiability,
ranked-choice voting, SMS OTP and real hardware validation.

## Slide 5 - Methodology and stack

Vue 3 + TypeScript + Vite + Tailwind; ASP.NET Core 10; EF Core 10 + PostgreSQL; Identity; xUnit;
installed PostgreSQL; Playwright; Testcontainers/Docker optional Compose; GitHub Actions.

## Slide 6 - Architecture

Show the SPA/API/PostgreSQL diagram and layered backend. Explain same-origin cookie/CSRF design.

## Slide 7 - Ballot privacy model

Show identity/eligibility/entitlement vs anonymous ballot vs aggregate result. Highlight the absence
of a voter key on ballot selections and the conditional entitlement update.

## Slide 8 - One-vote sequence

Show the transaction sequence:
authenticate → eligible → window open → validate choices → consume entitlement conditionally →
insert anonymous ballot → append audit → commit. Describe the 409 concurrent/duplicate result.

## Slide 9 - Election lifecycle

Show Draft → Scheduled → Open → Closed → UnderReview → Published with Suspended/Cancelled and audit
reasons. Emphasise server time and no voting outside the window.

## Slide 10 - Result workflow

Show provisional tally → observer verification → Super Administrator publication → immutable snapshot.
Explain recount/versioning.

## Slide 11 - Security controls

Identity, password hashing, lockout, rate limits, secure cookies, CSRF, RBAC, validation, CSP,
parameterized queries, photo validation, no choices in logs/audit, hash chain.

## Slide 12 - Demonstration evidence

Screens or short demo: student ballot, confirmation, officer approval, observer verification, published
result, audit verification.

## Slide 13 - Testing evidence

27 backend unit, 33 backend integration, 6 frontend unit, 3 Playwright end-to-end (69 total);
concurrency/rate-limit tests; Docker non-root health and backup restore; legacy-data migration drill;
three-department synthetic demonstration; 300-voter load clean at 12-way concurrency under the prior
schema. State what was not tested.

## Slide 14 - Limitations and residual risk

No physical/institutional validation; no cryptographic E2E voting; no MFA; privileged DB insider can
rewrite chain; timing correlation; no legal certification.

## Slide 15 - Recommendations

Hardware validation, independent penetration test, authenticated ballot envelope, secure key
provisioning, institution identity provider, accessibility review, load testing and formal election
procedure.

## Slide 16 - Conclusion and questions

“Ready for further laboratory validation, not a real election.” Invite questions. Have the audit,
architecture, database and test evidence ready in appendices.

## Likely examiner questions

1. How do you prevent double voting without storing student ID on the ballot?
2. What happens under two simultaneous submissions?
3. Can an administrator see a student's choices?
4. How are results protected from editing after publication?
5. How do you know the audit log was not altered?
6. What remains unproven or untested?
7. Why PostgreSQL rather than a simpler database?
8. What would you change for a university-wide deployment?
