using System.Text.RegularExpressions;

namespace SmartMarket.Application.Reporting;

/// <summary>Which days of the week a space trades, read from its opening hours ("ทุกวัน …", "ศ–อา …", "พฤ …").</summary>
public static partial class SellingDays
{
    public static readonly DayOfWeek[] EveryDay = Enum.GetValues<DayOfWeek>();

    private static readonly Dictionary<string, DayOfWeek> Thai = new()
    {
        ["อา"] = DayOfWeek.Sunday, ["จ"] = DayOfWeek.Monday, ["อ"] = DayOfWeek.Tuesday, ["พ"] = DayOfWeek.Wednesday,
        ["พฤ"] = DayOfWeek.Thursday, ["ศ"] = DayOfWeek.Friday, ["ส"] = DayOfWeek.Saturday,
    };

    /// <summary>A day range wraps past Saturday ("ศ–อา" = Fri, Sat, Sun). Hours that name no days count as every day.</summary>
    public static DayOfWeek[] Parse(string openHours)
    {
        var m = DayRange().Match(openHours.Trim());
        if (!m.Success || m.Groups["all"].Success) return EveryDay;
        var from = Thai[m.Groups["from"].Value];
        if (!m.Groups["to"].Success) return [from];
        var to = Thai[m.Groups["to"].Value];
        var days = new List<DayOfWeek> { from };
        for (var d = from; d != to;)
        {
            d = (DayOfWeek)(((int)d + 1) % 7);
            days.Add(d);
        }
        return [.. days];
    }

    [GeneratedRegex(@"^(?:(?<all>ทุกวัน)|(?<from>พฤ|อา|จ|อ|พ|ศ|ส)(?:\s*[–-]\s*(?<to>พฤ|อา|จ|อ|พ|ศ|ส))?(?=\s|$))")]
    private static partial Regex DayRange();
}
