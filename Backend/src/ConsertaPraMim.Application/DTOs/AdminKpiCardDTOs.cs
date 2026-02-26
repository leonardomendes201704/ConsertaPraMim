namespace ConsertaPraMim.Application.DTOs;

public record AdminKpiDetailLineDto(
    string Label,
    string Value);

public record AdminKpiCardDto(
    string Key,
    string Title,
    string Value,
    string? Caption,
    IReadOnlyList<AdminKpiDetailLineDto> Details,
    DateTime GeneratedAtUtc);
