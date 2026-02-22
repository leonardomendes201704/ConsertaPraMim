using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminMailboxFilterModel
{
    public string Folder { get; set; } = "inbox";
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SelectedMessageId { get; set; }
}

public class AdminMailboxIndexViewModel
{
    public AdminMailboxFilterModel Filters { get; set; } = new();
    public AdminMailboxSettingsDto Settings { get; set; } = new(
        IsConfigured: false,
        Enabled: false,
        SenderDisplayName: string.Empty,
        SenderEmail: string.Empty,
        Username: string.Empty,
        HasPassword: false,
        SmtpHost: "smtp.gmail.com",
        SmtpPort: 587,
        SmtpUseSsl: true,
        Pop3Host: "pop.gmail.com",
        Pop3Port: 995,
        Pop3UseSsl: true,
        SyncWindowSize: 40,
        PollIntervalSeconds: 120,
        LastSyncAtUtc: null,
        LastSyncStatus: null,
        LastSyncError: null);
    public AdminMailboxListResponseDto Messages { get; set; } = new(
        Items: Array.Empty<AdminMailboxMessageSummaryDto>(),
        Page: 1,
        PageSize: 20,
        TotalCount: 0,
        TotalPages: 0,
        LastSyncAtUtc: null,
        LastSyncStatus: null,
        LastSyncError: null);
    public AdminMailboxMessageDetailsDto? SelectedMessage { get; set; }
    public IReadOnlyList<AdminMailboxRecipientDto> Recipients { get; set; } = Array.Empty<AdminMailboxRecipientDto>();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class AdminMailboxSettingsWebRequest
{
    public bool Enabled { get; set; } = true;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string Pop3Host { get; set; } = "pop.gmail.com";
    public int Pop3Port { get; set; } = 995;
    public bool Pop3UseSsl { get; set; } = true;
    public int SyncWindowSize { get; set; } = 40;
    public int PollIntervalSeconds { get; set; } = 120;
}

public class AdminMailboxSendWebRequest
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; }
}

public class AdminMailboxMarkReadWebRequest
{
    public string MessageId { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string Folder { get; set; } = "inbox";
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
