# Phase 10 - Three-Department Demonstration Scenario

This scenario uses only synthetic seed data. The demo password is the local `SEED_DEMO_PASSWORD`
value; no real staff or student credentials are included.

## Seeded departments and elections

| Department | Code | Seeded election |
|---|---|---|
| Computer Science | CSC | 2026/2027 Computer Science Departmental Election (full demo) |
| Mathematics | MTH | 2026/2027 Mathematics Departmental Election (draft) |
| Physics | PHY | 2026/2027 Physics Departmental Election (draft) |

The Mathematics and Physics elections contain six synthetic students each, two positions and four
approved synthetic candidates. The Computer Science election retains the existing full demo used by
the automated tests.

## Synthetic accounts

All accounts use the demo password from `.env`:

| Purpose | Account |
|---|---|
| University administrator | `superadmin@demo.iaue.edu.ng` |
| CS electoral officer | `officer@demo.iaue.edu.ng` |
| Mathematics electoral officer | `officer.math@demo.iaue.edu.ng` |
| Physics electoral officer | `officer.physics@demo.iaue.edu.ng` |
| CS observer | `auditor@demo.iaue.edu.ng` |
| Mathematics observer | `observer.math@demo.iaue.edu.ng` |
| Physics observer | `observer.physics@demo.iaue.edu.ng` |
| CS student | `IAUE/CS/22/001` |
| Mathematics student | `IAUE/MTH/22/001` |
| Physics student | `IAUE/PHY/22/001` |

## Scenario

### 1. Show university-wide configuration

1. Sign in as the Super Administrator.
2. Open **Departments**.
3. Show Computer Science (CSC), Mathematics (MTH) and Physics (PHY), including faculty association and
   active state.
4. Create a synthetic department (for example, `Software Engineering`, code `SWE`) to demonstrate
   uniqueness. Try the same code in lowercase; the server rejects the duplicate.
5. Deactivate and reactivate the synthetic department. Existing records are not deleted.

### 2. Show scoped elections

1. Sign in as the CS officer (`officer@...`). Open **Elections**; only Computer Science elections are
   visible. Attempting to open a Mathematics election URL returns access denied.
2. Sign in as the Mathematics officer. Only Mathematics elections are visible; CS election URLs return
   access denied.
3. Sign in as the Super Administrator; all departments/elections are visible under university-wide
   oversight.
4. Open a Mathematics draft and show the department name/code in the election workspace. The voter
   import page states that the CSV department must match Mathematics or `MTH`.

### 3. Show independent voter eligibility

1. Sign in as the CS student. Only CS-eligible elections appear; the Mathematics election is absent.
2. Sign in as the Mathematics student. Only Mathematics-eligible elections appear.
3. Open an election and show the anonymous-ballot notice, candidate/position scope and one-vote rule.

### 4. Run two independent elections (optional)

1. As the CS officer, schedule/open the CS election inside its window.
2. As the Mathematics officer, schedule/open the Mathematics election inside its window.
3. Cast one CS ballot and one Mathematics ballot using the respective students.
4. Show that a second CS ballot returns “already submitted” and that the CS student cannot access the
   Mathematics ballot.
5. Close both elections and request result review in each department.

### 5. Show observer scope and publication

1. As the Mathematics observer, open **Election review** and verify only the Mathematics result.
2. Attempt the CS result verification; the server returns access denied.
3. As the CS observer, verify the CS result and show that the Mathematics result is absent from the
   list.
4. As the Super Administrator, publish the verified CS result.
5. Open the published result as the CS student. The Mathematics student cannot read the CS result.

### 6. Show scoped audit evidence

1. Open the audit trail as the CS observer: only events associated with CS are shown.
2. Open it as the Mathematics observer: only Mathematics events are shown.
3. Verify the chain. Verification still covers the global tamper-evident chain; the **list** is
   department-scoped. Do not claim that the chain is impossible to rewrite by a privileged database
   administrator.

### 7. Explain residuals

- Ballots are anonymous in normal application data, but timing/transaction correlation by a privileged
  database administrator remains possible.
- The audit chain is tamper-evident, not tamper-proof against a database administrator who rewrites
  both the log and chain state.
- Department assignments are administrative controls; incorrect assignment is an institutional risk.
- The demo is synthetic and is not a real election.

## Expected evidence

- Three department cards and three sample elections.
- Scoped election lists for CS/Math/Physics officers and observers.
- 403/empty-list behaviour on cross-department URL tampering.
- One accepted ballot per election and duplicate rejection.
- Department-specific observer verification and Super Administrator publication.
- Department-scoped audit/review lists with global chain verification.

The automated counterpart is `DepartmentIsolationTests` plus the existing ballot/result/audit tests.
