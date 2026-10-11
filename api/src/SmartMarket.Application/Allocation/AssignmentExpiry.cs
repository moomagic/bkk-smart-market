using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Allocation;
using SmartMarket.Domain.Rounds;

namespace SmartMarket.Application.Allocation;

/// <summary>
/// Forfeits assignments left unconfirmed past their deadline, lazily: there is no background job, so every read or
/// command that shows a round's outcome calls this first (CLAUDE.md › ยืนยันสิทธิ์). Each expiry, with its promotion,
/// is its own system command with one audit entry.
/// </summary>
public sealed class AssignmentExpiry(IAppDbContext db, IClock clock, IOptions<RulesOptions> rules)
{
    public const string Action = "Round.ExpireAssignment";

    /// <summary>Only <paramref name="roundIds"/> — never the whole database, so reading one round can't rewrite others.</summary>
    public async Task ForfeitExpired(IReadOnlyCollection<Guid> roundIds, CancellationToken ct)
    {
        var now = clock.Now;
        var expired = await (
            from a in db.Assignments.Where(Assignment.ExpiredAt(now))
            join l in db.Locks on a.LockId equals l.Id
            where roundIds.Contains(a.RoundId)
            select new { Assignment = a, LockCode = l.Code }).ToListAsync(ct);

        // Several winners share one deadline; a stable order (round, then lock code as B2 < B10) keeps the promotion
        // chain the same whenever the expiry happens to be noticed (Guardrail 2).
        foreach (var x in expired
                     .OrderBy(x => x.Assignment.RoundId)
                     .ThenBy(x => x.LockCode.Length).ThenBy(x => x.LockCode, StringComparer.Ordinal))
        {
            await Forfeiture.Apply(db, x.Assignment, now, rules.Value.ConfirmWithinDays, Forfeiture.ExpiredReason, ct);
            await db.CommitAsSystemAsync(Action, AuditSubject.For<Round>(x.Assignment.RoundId), ct);
        }
    }
}
