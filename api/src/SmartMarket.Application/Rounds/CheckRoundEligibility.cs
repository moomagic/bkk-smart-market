using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Application.StallRequests;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.Rounds;

/// <summary>
/// Officer checks every request of a round in one go: Eligible (with carry-over weight) or Ineligible (with reason).
/// Re-runnable until the draw. One audit entry per run, filed under the Round.
/// </summary>
public sealed class CheckRoundEligibilityHandler(IAppDbContext db, IClock clock, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.CheckEligibility";

    public async Task<Result<IReadOnlyList<RoundRequestRow>>> Handle(Guid roundId, CancellationToken ct)
    {
        var target = await (
            from r in db.Rounds
            join s in db.Spaces on r.SpaceId equals s.Id
            where r.Id == roundId
            select new { Round = r, Space = s }).SingleOrDefaultAsync(ct);
        if (target is null)
            return Error.NotFound("round.not_found", "ไม่พบรอบนี้");
        if (!target.Round.CanCheckEligibility)
            return Error.Conflict("round.already_drawn", "รอบนี้จับฉลากแล้ว ตรวจสิทธิใหม่ไม่ได้");

        var round = target.Round;
        var now = clock.Now;
        var space = target.Space;
        var requests = await db.StallRequests
            // A request a Corrected review fixed stays as the review left it (its window and weight too).
            .Where(q => q.RoundId == roundId && StallRequest.AwaitingDraw.Contains(q.Status) && !q.CorrectedByReview)
            .ToListAsync(ct);
        var vendorIds = requests.Select(q => q.VendorId).ToList();
        var vendors = await db.Vendors.AsNoTracking().Where(v => vendorIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);

        var previousRoundId = await RoundQueries.PreviousRoundId(db, space.Id, round.StartsOn, ct);
        var previousOutcome = await db.StallRequests
            .Where(q => q.RoundId == previousRoundId && vendorIds.Contains(q.VendorId))
            .ToDictionaryAsync(q => q.VendorId, q => q.Status, ct);

        // ADR-0003: other unfinished requests in the district whose round overlaps this one.
        var otherOpen = await (
            from q in db.StallRequests
            join r in db.Rounds.Where(Round.Overlapping(round.StartsOn, round.EndsOn)) on q.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where vendorIds.Contains(q.VendorId) && q.RoundId != roundId
                  && s.DistrictId == space.DistrictId && !StallRequest.Finished.Contains(q.Status)
            group q by q.VendorId into g
            select new { VendorId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.VendorId, x => x.Count, ct);

        // Earlier TrialArea rounds this year only, so the result doesn't depend on which round is checked first.
        var trialThisYear = space.Type != SpaceType.TrialArea ? [] : await (
            from q in db.StallRequests
            join r in db.Rounds on q.RoundId equals r.Id
            join s in db.Spaces on r.SpaceId equals s.Id
            where vendorIds.Contains(q.VendorId) && q.RoundId != roundId
                  && s.Type == SpaceType.TrialArea && r.StartsOn.Year == round.StartsOn.Year && r.StartsOn < round.StartsOn
                  && q.Status != StallRequestStatus.Ineligible
            group q by q.VendorId into g
            select new { VendorId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.VendorId, x => x.Count, ct);

        var limits = new EligibilityLimits(
            rules.Value.Rounds.TrialAreaRequestsPerYear, rules.Value.MaxOpenRequestsPerVendorInDistrict, rules.Value.CarryOverWeight);
        foreach (var request in requests)
        {
            var vendor = vendors.GetValueOrDefault(request.VendorId);
            var decision = EligibilityChecker.Check(new EligibilityFacts(
                HasIdentity: vendor?.HasIdentity ?? false,
                FoodHygieneTrained: vendor?.FoodHygieneTrained ?? false,
                BannedUntil: vendor?.BannedUntil,
                SpaceSellsFood: space.SellsFood,
                RoundStartsOn: round.StartsOn,
                IsTrialArea: space.Type == SpaceType.TrialArea,
                TrialAreaRequestsThisYear: trialThisYear.GetValueOrDefault(request.VendorId),
                OtherOpenRequestsInPeriod: otherOpen.GetValueOrDefault(request.VendorId),
                PreviousRoundOutcomeHere: previousOutcome.TryGetValue(request.VendorId, out var p) ? p : null), limits);

            if (decision.Eligible) request.MarkEligible(decision.Weight, now);
            else request.MarkIneligible(decision.Reason!, now);
        }

        await db.CommitAsync(Action, AuditSubject.For<Round>(roundId), ct);
        return await ListRoundRequestsHandler.Rows(db, roundId, ct);
    }
}
