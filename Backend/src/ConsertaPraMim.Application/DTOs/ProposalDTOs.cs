namespace ConsertaPraMim.Application.DTOs;

public record CreateProposalDto(Guid RequestId, decimal? EstimatedValue, string? Message);

public record ProposalDto(
    Guid Id, 
    Guid RequestId, 
    Guid ProviderId, 
    string ProviderName, 
    decimal? EstimatedValue, 
    bool Accepted, 
    string? Message, 
    DateTime CreatedAt);
