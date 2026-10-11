using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.StallRequests;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

/// <summary>Every test draws a round of its own — see <see cref="AllocationArrange"/>. Tests that move the clock put it back.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReviewTests(ApiFactory api) : IDisposable
{
    public void Dispose() => api.Clock.Reset();

    internal sealed record ReviewDto(Guid Id, Guid StallRequestId, string Reason, DateTimeOffset SubmittedAt, string? Outcome,
        string? Response, DateTimeOffset? RespondedAt);
    private sealed record RowDto(Guid RequestId, Guid VendorId, string Status, int CarryOverWeight);
    private sealed record PendingDto(Guid Id, Guid StallRequestId, string VendorName, string RequestStatus, string Reason, string? Outcome);
    private sealed record MineDto(Guid Id, Guid RoundId, string Status, int? WaitlistPosition, string? ResultReason, ReviewDto? Review);
    private sealed record RoundDto(Guid Id, string Status, int RejectsLeft);

    [Fact]
    public async Task An_ineligible_vendor_files_a_review_with_a_reason_and_sees_it_pending()
    {
        var (roundId, vendorId, requestId) = await IneligibleBeforeDraw();
        var mark = await api.AuditMark();

        var response = await File(vendorId, requestId, "ถูกงดสิทธิ์ผิดคน ขอให้ตรวจใหม่");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var review = (await response.Content.ReadFromJsonAsync<ReviewDto>(Json))!;
        review.Outcome.Should().BeNull();
        (await Mine(vendorId, roundId)).Review!.Reason.Should().Be("ถูกงดสิทธิ์ผิดคน ขอให้ตรวจใหม่");
        var entry = (await api.AuditSince(mark)).Should().ContainSingle().Subject;
        entry.Action.Should().Be("Round.FileReview");
        entry.EntityId.Should().Be(roundId.ToString());
        entry.Changes.Should().NotContain("ถูกงดสิทธิ์ผิดคน", "a vendor's free text may hold personal data, so it is masked");
        (await File(vendorId, requestId, "ซ้ำ")).StatusCode.Should().Be(HttpStatusCode.Conflict, "one review per request");
    }

    [Fact]
    public async Task A_review_filed_more_than_7_days_after_the_result_is_refused()
    {
        var (_, vendorId, requestId) = await IneligibleBeforeDraw();
        api.Clock.Set(TestClock.DemoNow.AddDays(7).AddMinutes(1));

        var response = await File(vendorId, requestId, "ช้าไป");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Contain("7 วัน");
    }

    [Fact]
    public async Task A_request_without_a_result_yet_cannot_be_reviewed()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
        var vendorId = await api.AddRequest(roundId, Category.New, StallRequestStatus.Submitted);
        var request = (await Mine(vendorId, roundId));

        (await File(vendorId, request.Id, "ยังไม่ได้ตรวจ")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await File(Guid.NewGuid(), request.Id, "ไม่ใช่ของฉัน")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upheld_records_the_districts_answer_and_changes_nothing_else()
    {
        var (roundId, vendorId, requestId) = await IneligibleBeforeDraw();
        var review = await Filed(vendorId, requestId);
        var mark = await api.AuditMark();

        var response = await Respond(review.Id, "Upheld", "ตรวจแล้ว ข้อมูลงดสิทธิ์ถูกต้อง");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var mine = await Mine(vendorId, roundId);
        mine.Status.Should().Be("Ineligible");
        mine.Review!.Should().Match<ReviewDto>(r => r.Outcome == "Upheld" && r.Response == "ตรวจแล้ว ข้อมูลงดสิทธิ์ถูกต้อง" && r.RespondedAt != null);
        (await api.AuditSince(mark)).Should().ContainSingle().Which.Action.Should().Be("Round.RespondReview");
        (await Respond(review.Id, "Corrected", "เปลี่ยนใจ")).StatusCode.Should().Be(HttpStatusCode.Conflict, "answered already");
        (await File(vendorId, requestId, "ขออีกครั้ง")).StatusCode.Should().Be(HttpStatusCode.Conflict, "one review per request");
    }

    [Fact]
    public async Task An_answer_needs_a_response_text()
    {
        var (_, vendorId, requestId) = await IneligibleBeforeDraw();
        var review = await Filed(vendorId, requestId);

        (await Respond(review.Id, "Upheld", "  ")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Corrected_before_approval_rejects_the_draft_without_using_the_limit_and_the_vendor_joins_the_redraw()
    {
        var (roundId, vendorId, requestId) = await IneligibleBeforeDraw();
        var draft = (await (await api.Officer().PostAsync($"/api/rounds/{roundId}/allocation-runs", null))
            .Content.ReadFromJsonAsync<RunDto>(Json))!;
        var review = await Filed(vendorId, requestId);
        var mark = await api.AuditMark();

        var response = await Respond(review.Id, "Corrected", "ข้อมูลงดสิทธิ์เป็นของผู้ค้าชื่อซ้ำ แก้ไขแล้ว");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.AuditSince(mark)).Should().ContainSingle("the reject and the answer are one command")
            .Which.Changes.Should().Contain("Rejected");
        (await api.Officer().GetFromJsonAsync<RunDto>($"/api/allocation-runs/{draft.Id}", Json))!.Status.Should().Be("Rejected");
        (await Round(roundId)).Should().Match<RoundDto>(r => r.Status == "Closed" && r.RejectsLeft == 2, "a Corrected review does not count");
        (await Mine(vendorId, roundId)).Status.Should().Be("Eligible");

        var rechecked = await (await api.Officer().PostAsync($"/api/rounds/{roundId}/eligibility", null)).Content.ReadFromJsonAsync<RowDto[]>(Json);
        rechecked!.Single(r => r.VendorId == vendorId).Status.Should().Be("Eligible", "a re-check must not undo the correction");
        var redraw = (await (await api.Officer().PostAsync($"/api/rounds/{roundId}/allocation-runs", null))
            .Content.ReadFromJsonAsync<RunDto>(Json))!;
        redraw.Outcomes.Should().Contain(o => o.VendorId == vendorId);
    }

    [Fact]
    public async Task Corrected_after_approval_appends_the_vendor_to_the_end_of_their_groups_waitlist_and_keeps_the_winners()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 3, banned: 1);
        var vendorId = await IneligibleVendor(roundId);
        var request = await Mine(vendorId, roundId);
        var review = await Filed(vendorId, request.Id);
        var newWaitlist = run.Outcomes.Count(o => o.Category == "New" && o.Kind == "Waitlisted");

        (await Respond(review.Id, "Corrected", "ตรวจสิทธิผิด")).StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = await Mine(vendorId, roundId);
        mine.Status.Should().Be("Waitlisted");
        mine.WaitlistPosition.Should().Be(newWaitlist + 1);
        mine.ResultReason.Should().Contain("ทบทวน");
        foreach (var winner in run.Outcomes.Where(o => o.Kind == "Won"))
            (await Mine(winner.VendorId, roundId)).Status.Should().Be("Won", "existing winners stay as they are");
        (await Round(roundId)).Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Draw_outcomes_cannot_be_reviewed()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 3);
        var loser = run.Outcomes.First(o => o.Kind != "Won");
        var request = await Mine(loser.VendorId, roundId);

        var response = await File(loser.VendorId, request.Id, "ไม่ได้ล็อค ขอจับใหม่");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Contain("ผลการจับฉลากทบทวนไม่ได้");
    }

    [Fact]
    public async Task Corrected_after_approval_hands_a_vacant_lock_straight_to_the_vendor()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 0, @new: 1, banned: 1);
        run.UnassignedLockCodes.Should().HaveCount(2, "two applicants for four locks");
        var vendorId = await IneligibleVendor(roundId);
        var review = await Filed(vendorId, (await Mine(vendorId, roundId)).Id);

        (await Respond(review.Id, "Corrected", "ตรวจสิทธิผิด")).StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = await api.Mine(vendorId, roundId);
        mine.Status.Should().Be("Won");
        mine.LockCode.Should().Be(run.UnassignedLockCodes.OrderBy(c => c.Length).ThenBy(c => c, StringComparer.Ordinal).First());
        mine.ConfirmBy.Should().BeCloseTo(TestClock.DemoNow.AddDays(3), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Officers_see_the_reviews_of_a_round()
    {
        var (roundId, vendorId, requestId) = await IneligibleBeforeDraw();
        await Filed(vendorId, requestId);

        var reviews = await api.Officer().GetFromJsonAsync<PendingDto[]>($"/api/rounds/{roundId}/reviews", Json);

        reviews!.Should().ContainSingle().Which.Should().Match<PendingDto>(r =>
            r.StallRequestId == requestId && r.RequestStatus == "Ineligible" && r.Outcome == null && r.VendorName.EndsWith('.'));
        (await api.Vendor(vendorId).GetAsync($"/api/rounds/{roundId}/reviews")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A fresh round, checked, holding one banned (so Ineligible) vendor.</summary>
    private async Task<(Guid RoundId, Guid VendorId, Guid RequestId)> IneligibleBeforeDraw()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 2, banned: 1);
        var vendorId = await IneligibleVendor(roundId);
        return (roundId, vendorId, (await Mine(vendorId, roundId)).Id);
    }

    private async Task<Guid> IneligibleVendor(Guid roundId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.StallRequests.Where(q => q.RoundId == roundId && q.Status == StallRequestStatus.Ineligible)
            .Select(q => q.VendorId).SingleAsync();
    }

    private Task<HttpResponseMessage> File(Guid vendorId, Guid requestId, string reason) =>
        api.Vendor(vendorId).PostAsJsonAsync($"/api/stall-requests/{requestId}/reviews", new { reason });

    private async Task<ReviewDto> Filed(Guid vendorId, Guid requestId)
    {
        var response = await File(vendorId, requestId, "ขอให้ตรวจสอบอีกครั้ง");
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ReviewDto>(Json))!;
    }

    private Task<HttpResponseMessage> Respond(Guid reviewId, string outcome, string response) =>
        api.Officer().PostAsJsonAsync($"/api/reviews/{reviewId}/respond", new { outcome, response });

    private async Task<MineDto> Mine(Guid vendorId, Guid roundId) =>
        (await api.Vendor(vendorId).GetFromJsonAsync<MineDto[]>("/api/stall-requests/me", Json))!.Single(r => r.RoundId == roundId);

    private async Task<RoundDto> Round(Guid roundId) =>
        (await api.Officer().GetFromJsonAsync<RoundDto[]>("/api/rounds", Json))!.Single(r => r.Id == roundId);

    private static async Task<string> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;
}
