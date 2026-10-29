# Student and Voter Guide

## Before you start

Use the matriculation number on the departmental register and the one-time activation token issued by
the electoral office. Never share your password or activation token. The demo workspace uses synthetic
accounts and is not a real election.

## Activate your account

1. Open `/activate`.
2. Enter your matriculation number exactly as issued (for example `IAUE/CS/22/001`).
3. Enter the one-time activation token.
4. Choose a password of at least 12 characters with uppercase, lowercase, number and symbol.
5. Confirm the password and select **Activate account**.

If activation succeeds you are signed in automatically. If the token is invalid or expired, contact
the electoral office; do not attempt to guess tokens.

## Sign in

Open `/login`, enter your matriculation number or account email and password. Repeated failures may
temporarily lock the account. Use **Forgot password?** only if email delivery has been configured by
the institution; the response never reveals whether an account exists.

## Find an election

The **Student home** and **Elections** pages show only elections for which your student record is
eligible. Election cards show the schedule and status. A Draft or Scheduled election cannot accept a
ballot. Wait for **Open**.

## Cast your ballot

1. Open the election and select **Cast your ballot**.
2. Review the approved candidate manifestos for every position.
3. Select the permitted number of candidates for each position. Where allowed, you may choose
   **Abstain from this position**.
4. Select **Review ballot**.
5. Check every position on the review screen. Read the final submission acknowledgement and tick the
   confirmation checkbox.
6. Select **Submit final ballot**, then confirm the final dialog.

The server verifies your active student profile, eligibility, election window, approved candidates,
selection limits and one-vote entitlement. A second or concurrent submission is rejected. Once
accepted, a ballot cannot be edited or withdrawn.

## Your confirmation reference

The confirmation page shows a random, non-revealing reference. It proves submission to you and does not
contain or reveal your choices. There is intentionally no public lookup from the reference to a ballot.
Store it securely; anyone with it may know that a submission occurred, but cannot retrieve selections.

The confirmation page and student dashboard show that a ballot is recorded. You cannot vote again in
the same election.

## Results

Published results appear under **Published results** after the required process:

1. the electoral officer closes the election and requests result review;
2. an independent observer verifies the aggregate snapshot;
3. the Super Administrator publishes the official version.

Results are aggregate-only. You will not see individual ballots or any voter-to-choice mapping.

## Common problems

| Problem | Explanation |
|---|---|
| No elections visible | Your register entry may not be eligible for an election, or the election is not yet scheduled/published |
| Cast ballot unavailable | Election is not Open, you already voted, or your profile is inactive |
| Selection cannot continue | Complete every position; explicit abstention is only available where configured |
| Activation token rejected | Token is expired, already used, or the matriculation number is wrong; contact the officer |
| Sign-in locked | Wait for the lockout period or contact the electoral office; do not repeatedly guess |
| Ballot submission reports already recorded | A previous request may have succeeded; check the confirmation/status page |
| No reset email | SMTP may not be configured; contact the electoral office through the approved channel |

## Privacy and responsible use

- Vote privately; no one should ask you to show your choices.
- The system never associates your identity with your selections in ordinary reports, receipts or
  audit events.
- Do not share your session, password, activation token or confirmation reference.
- Report suspected coercion, phishing, account compromise or unexpected election behaviour to the
  electoral office immediately.
- A received SMS, connected modem or dashboard value is not proof that a room is safe; follow the
  institution's independent safety procedures.
