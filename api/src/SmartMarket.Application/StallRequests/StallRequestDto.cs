using SmartMarket.Domain.StallRequests;

namespace SmartMarket.Application.StallRequests;

public sealed record StallRequestDto(
    Guid Id, Guid RoundId, Guid SpaceId, string SpaceName, DateOnly RoundStartsOn, DateOnly RoundEndsOn,
    DateTimeOffset SubmittedAt, Category Category, StallRequestStatus Status, string? IneligibleReason);
