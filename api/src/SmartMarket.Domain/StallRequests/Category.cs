namespace SmartMarket.Domain.StallRequests;

/// <summary>Quota group of a stall request, snapshotted at submission (see CONTEXT.md › Category).</summary>
public enum Category
{
    Legacy,
    Vulnerable,
    New,
}
