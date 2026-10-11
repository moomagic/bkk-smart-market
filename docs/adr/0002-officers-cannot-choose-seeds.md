# Officers cannot choose seeds; rejected runs stay visible

The spec allows rejecting a run and re-running it with a new seed. On its own, that lets an officer keep re-running until they get the result they want, which defeats the fairness claim. So:

- The seed comes from a crypto RNG. Officers cannot type one in. The only exception is the demo seed, which is injected through seed data.
- A reject must carry a reason.
- A round allows at most 2 rejects; beyond that needs executive approval. A reject that comes from a Review marked Corrected (an eligibility mistake) does not count toward this limit.
- Every run of a round, with its Reference No, is shown on the vendor result page.
- `InputHash = SHA256(canonical JSON of [VendorId, Category, Weight] sorted by VendorId, N, RulesSnapshot)`. The RulesSnapshot (quota percentages, waitlist size) is stored on the AllocationRun, so a config change cannot quietly change results.
- A reject returns the round from Drawn to Closed: eligibility can be re-checked (to fix the mistake behind the reject) and the round drawn again with a fresh seed, but it never reopens to new requests — the field was fixed by the first draw. Past the limit, the executive is the one who rejects.
