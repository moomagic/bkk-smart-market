# Reviews correct eligibility only; a late correction gets a vacant lock or the end of the waitlist

A review exists to fix an eligibility mistake (CONTEXT.md › Review). Draw outcomes are seeded, replayable and cannot be reviewed. We decided:

- **What can be reviewed**: only an Ineligible request, once. The 7 days run from the approval of the round's run when there is one, otherwise from the eligibility check that found it Ineligible — a re-check with the same result keeps that date.
- **Corrected before approval**: the request becomes Eligible, marked so later eligibility checks leave it alone. A pending draft is rejected in the same command, and that reject does not count toward the limit (ADR-0002). The officer re-runs.
- **Corrected after approval**: winners stay as they are. The request takes a lock left vacant if there is one (lowest code first, with a fresh confirm deadline); otherwise it goes to the end of its group's waitlist — **past the 3-per-group cap if need be**, because it was wrongly kept out of the draw. It is added to the run's current result (and to the pooled order for a 1-lock space) so a later forfeit can promote it.
- **Weight** is recomputed by the carry-over rule, never typed in by the officer, so a correction cannot shape the draw.
- A correction is refused if the vendor, while finished as Ineligible, has opened another request in an overlapping round in the district (ADR-0003).

## Consequences

- A wrong weight on an Eligible request cannot be corrected through a review.
- The district's answer deadline (7 days) is shown to officers, not enforced.
