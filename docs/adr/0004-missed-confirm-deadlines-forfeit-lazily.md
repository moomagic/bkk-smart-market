# Missed confirm deadlines are forfeited lazily, as system commands, with a fresh deadline for whoever moves up

A winner who does not confirm within `Rules:ConfirmWithinDays` loses the lock. There is no background job (the prototype runs on one `docker compose up` and a movable demo clock). So:

- **When**: any read or command that shows a round's outcome (the vendor's own requests, the officer's round and run views, confirm, forfeit) first forfeits that round's expired assignments, using `IClock`. It only touches the rounds being read, never the whole database.
- **Who**: each expiry, together with the promotion it causes, is its own command attributed to actor `system` and filed under the Round (`Round.ExpireAssignment`), so the audit trail does not blame whoever happened to open the page.
- **Order**: winners of a run share one deadline, so expiries in a round are processed by lock code (B2 before B10). The promotion chain then does not depend on read order.
- **Deadline for whoever moves up**: the time the expiry is processed plus `ConfirmWithinDays`, not the forfeiter's deadline plus the days. Otherwise one clock jump would cascade through the whole waitlist before anyone could confirm. The cost is that the promoted vendor's `ConfirmBy` depends on when the round was next read. The audit entry records that time.
- **The draw record never changes**: `AllocationRun.ResultJson` stays exactly what the engine produced, so a replay from seed + rules still matches. The state after forfeits and promotions lives in `AllocationRun.CurrentResultJson`.

A confirmed vendor cannot forfeit in the app; giving up a confirmed lock goes through the district office.
