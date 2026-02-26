namespace ConsertaPraMim.Application.DTOs;

public record AdminDashboardWidgetMetricDto(
    string Label,
    string Value);

public record AdminDashboardWidgetItemDto(
    string Title,
    string Value,
    string? Subtitle = null,
    string Tone = "secondary");

public record AdminDashboardWidgetTableColumnDto(
    string Key,
    string Label,
    string Alignment = "start");

public record AdminDashboardWidgetTableCellDto(
    string Value,
    string? Tone = null,
    bool IsMuted = false,
    bool IsEmphasis = false);

public record AdminDashboardWidgetTableRowDto(
    IReadOnlyList<AdminDashboardWidgetTableCellDto> Cells);

public record AdminDashboardWidgetDto(
    string Key,
    string Title,
    string WidgetKind,
    string? Subtitle = null,
    string? PrimaryValue = null,
    string? PrimaryCaption = null,
    string? SecondaryValue = null,
    string? SecondaryCaption = null,
    IReadOnlyList<AdminDashboardWidgetMetricDto>? SummaryMetrics = null,
    IReadOnlyList<AdminDashboardWidgetItemDto>? Items = null,
    IReadOnlyList<AdminDashboardWidgetTableColumnDto>? Columns = null,
    IReadOnlyList<AdminDashboardWidgetTableRowDto>? Rows = null,
    IReadOnlyList<AdminRecentEventDto>? RecentEvents = null,
    DateTime? GeneratedAtUtc = null);
