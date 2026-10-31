using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.CheckIns;
using SmartMarket.Application.Common;

namespace SmartMarket.Api.Tests;

/// <summary>Every test draws a round of its own — see <see cref="AllocationArrange"/>. Tests that move the clock put it back.</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckInTests(ApiFactory api) : IDisposable
{
    public void Dispose() => api.Clock.Reset();

    private sealed record CheckInDto(Guid Id, Guid StallRequestId, string SpaceName, string LockCode, DateTimeOffset At, bool WithinGeofence);

    [Fact]
    public async Task A_confirmed_winner_checks_in_with_the_qr_of_their_lock()
    {
        var (roundId, vendorId, request) = await ConfirmedWinner();
        var since = await api.AuditMark();

        var response = await CheckIn(vendorId, request.Id, Qr(request), withinGeofence: true);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var checkIn = (await response.Content.ReadFromJsonAsync<CheckInDto>())!;
        checkIn.At.Should().BeCloseTo(TestClock.DemoNow, TimeSpan.FromSeconds(1));
        checkIn.LockCode.Should().Be(request.LockCode);
        checkIn.WithinGeofence.Should().BeTrue();
        (await api.Mine(vendorId, roundId)).LastCheckInAt.Should().BeCloseTo(TestClock.DemoNow, TimeSpan.FromSeconds(1));

        var entry = (await api.AuditSince(since)).Should().ContainSingle().Subject;
        entry.Action.Should().Be("Round.CheckIn");
        entry.EntityType.Should().Be("Round");
        entry.EntityId.Should().Be(roundId.ToString());
        entry.Actor.Should().Be(vendorId.ToString());
    }

    [Fact]
    public async Task Checking_in_outside_the_geofence_is_recorded_and_flagged_not_refused()
    {
        var (_, vendorId, request) = await ConfirmedWinner();

        var response = await CheckIn(vendorId, request.Id, Qr(request), withinGeofence: false);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<CheckInDto>())!.WithinGeofence.Should().BeFalse();
    }

    [Fact]
    public async Task Only_one_check_in_per_selling_day()
    {
        var (_, vendorId, request) = await ConfirmedWinner();
        (await CheckIn(vendorId, request.Id, Qr(request))).StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await CheckIn(vendorId, request.Id, Qr(request));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(again)).Should().Contain("เช็คอินวันนี้แล้ว");
        api.Clock.Set(TestClock.DemoNow.AddDays(1));
        (await CheckIn(vendorId, request.Id, Qr(request))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_qr_from_another_lock_is_refused()
    {
        var (_, vendorId, request) = await ConfirmedWinner();
        var otherLock = request.LockCode == "B1" ? "B2" : "B1";

        var response = await CheckIn(vendorId, request.Id, CheckInQr.For(request.SpaceId, otherLock));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Contain("QR ไม่ตรงกับล็อค");
    }

    [Fact]
    public async Task Only_a_confirmed_winner_can_check_in()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 2, @new: 2);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var waiting = run.Outcomes.First(o => o.Kind == "Waitlisted");
        var unconfirmed = await api.Mine(winner.VendorId, roundId);
        var waitlisted = await api.Mine(waiting.VendorId, roundId);

        var notYet = await CheckIn(winner.VendorId, unconfirmed.Id, Qr(unconfirmed));
        notYet.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(notYet)).Should().Contain("ยืนยันสิทธิ์ก่อน");
        (await CheckIn(waiting.VendorId, waitlisted.Id, CheckInQr.For(waitlisted.SpaceId, "B1")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_winner_past_the_confirm_deadline_is_told_the_right_has_ended()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 2);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var request = await api.Mine(winner.VendorId, roundId);
        api.Clock.Set(TestClock.DemoNow.AddDays(4));

        var response = await CheckIn(winner.VendorId, request.Id, Qr(request));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().NotContain("ยืนยันสิทธิ์ก่อน");
    }

    [Fact]
    public async Task A_vendor_cannot_check_in_on_someone_elses_request()
    {
        var (roundId, vendorId, request) = await ConfirmedWinner();

        (await CheckIn(Guid.NewGuid(), request.Id, Qr(request))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "unknown vendor");
        var stranger = (await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 1)).Run.Outcomes[0].VendorId;
        (await CheckIn(stranger, request.Id, Qr(request))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await api.Officer().PostAsJsonAsync("/api/check-ins", new { stallRequestId = request.Id, qrCode = Qr(request), withinGeofence = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task No_check_in_after_the_round_has_ended()
    {
        var (_, vendorId, request) = await ConfirmedWinner();
        api.Clock.Set(new DateTimeOffset(2027, 5, 1, 9, 0, 0, TimeSpan.FromHours(7)));

        var response = await CheckIn(vendorId, request.Id, Qr(request));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Contain("รอบนี้สิ้นสุดแล้ว");
    }

    [Fact]
    public async Task The_device_is_kept_as_a_hash_and_masked_in_the_audit_trail()
    {
        var (_, vendorId, request) = await ConfirmedWinner();
        const string device = "device-7f3a91c2";
        var since = await api.AuditMark();

        var response = await CheckIn(vendorId, request.Id, Qr(request), deviceId: device);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<CheckInDto>())!.Id;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().CheckIns.SingleAsync(c => c.Id == id);
            stored.DeviceHash.Should().Be(DeviceHash.Of(device)).And.MatchRegex("^[0-9a-f]{64}$").And.NotContain(device);
        }
        var entry = (await api.AuditSince(since)).Should().ContainSingle().Subject;
        var added = JsonDocument.Parse(entry.Changes).RootElement.EnumerateArray()
            .Single(c => c.GetProperty("Entity").GetString() == "CheckIn");
        added.GetProperty("After").GetProperty("DeviceHash").GetString().Should().Be("***");
        entry.Changes.Should().NotContain(device).And.NotContain(DeviceHash.Of(device)!);
    }

    [Fact]
    public async Task A_check_in_without_a_device_id_still_goes_through()
    {
        var (_, vendorId, request) = await ConfirmedWinner();

        var response = await CheckIn(vendorId, request.Id, Qr(request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<CheckInDto>())!.Id;
        await using var scope = api.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IAppDbContext>().CheckIns.SingleAsync(c => c.Id == id)).DeviceHash.Should().BeNull();
    }

    [Fact]
    public async Task An_overlong_device_id_is_a_bad_request()
    {
        var (_, vendorId, request) = await ConfirmedWinner();

        var response = await CheckIn(vendorId, request.Id, Qr(request), deviceId: new string('x', CheckInAtLockHandler.MaxDeviceIdLength + 1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A winner of a fresh round who has confirmed their lock.</summary>
    private async Task<(Guid RoundId, Guid VendorId, ApproveAllocationTests.MyRequestDto Request)> ConfirmedWinner()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 2);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var request = await api.Mine(winner.VendorId, roundId);
        (await api.Vendor(winner.VendorId).PostAsync($"/api/stall-requests/{request.Id}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return (roundId, winner.VendorId, await api.Mine(winner.VendorId, roundId));
    }

    private static string Qr(ApproveAllocationTests.MyRequestDto request) => CheckInQr.For(request.SpaceId, request.LockCode!);

    private Task<HttpResponseMessage> CheckIn(Guid vendorId, Guid requestId, string qrCode, bool withinGeofence = true, string? deviceId = null) =>
        api.Vendor(vendorId).PostAsJsonAsync("/api/check-ins", new { stallRequestId = requestId, qrCode, withinGeofence, deviceId });

    private static async Task<string> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;
}
