# One unfinished stall request per vendor per overlapping round period

The spec says a vendor may hold only one unfinished stall request in the district ("1 คน 1 ล็อค ทั้งเขต"). Read literally, a vendor who holds a lock this round (request Confirmed until the round ends) could not apply for the next round until the current one is over, after its request window has already closed. สมศรี ใ. holds a lock at ปากซอยจรัญฯ 79 until 31 ต.ค. 2569 and must apply for the round starting 1 พ.ย. 2569.

We count a request against the limit only when its round period overlaps the target round's period. Ineligible, NotSelected and Forfeited requests are finished. This keeps "one lock across the district at any one time" while letting vendors apply for the next round. The limit itself comes from `Rules:MaxOpenRequestsPerVendorInDistrict`.

## Consequences

- The eligibility check (#12) must reuse the same `Round.Overlapping` rule, or the two will disagree.
