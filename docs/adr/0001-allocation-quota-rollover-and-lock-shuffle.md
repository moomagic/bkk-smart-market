# Allocation: small-N quotas, cyclic rollover, per-group waitlist, shuffled lock codes

The literal quota formula (`ceil(N×0.25)` for Vulnerable and New) breaks at N≤2, and drawing groups in a fixed order hands the lowest lock codes (the best spots) to Legacy every round. We decided to:

- **N=1**: one draw pooled across all groups.
- **N=2**: 1 Vulnerable + 1 from New-or-Legacy, depending on which has applicants.
- **Rollover**: cycle Vulnerable → New → Legacy → Vulnerable. Legacy may go over 50% **only through rollover**, because an empty lock hurts occupancy more than a soft breach of the Legacy cap.
- **Waitlist**: kept per group. A forfeit promotes from the same group first and falls back to rollover, so quotas still hold after approval.
- **Lock codes**: after winners are picked, shuffle their lock codes with the same seeded RNG instead of handing them out in draw order.

## Consequences

- Any change to the draw order or RNG consumption invalidates the demo seed (สมศรี ใ. → B2). A test pins this.
- Input is sorted by stable VendorId before drawing, so the result does not depend on insertion order.
