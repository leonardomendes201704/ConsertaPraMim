using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class LegalTermsServiceTests
{
    private readonly Mock<ILegalTermsRepository> _legalTermsRepositoryMock;
    private readonly Mock<IAdminAuditLogRepository> _adminAuditLogRepositoryMock;
    private readonly LegalTermsService _service;

    public LegalTermsServiceTests()
    {
        _legalTermsRepositoryMock = new Mock<ILegalTermsRepository>();
        _adminAuditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        _service = new LegalTermsService(
            _legalTermsRepositoryMock.Object,
            _adminAuditLogRepositoryMock.Object);
    }

    /// <summary>
    /// Cenario: admin tenta publicar nova versao sem titulo informado.
    /// Passos: chama PublishAsync com title vazio.
    /// Resultado esperado: retorna erro de validacao e nao persiste alteracoes.
    /// </summary>
    [Fact(DisplayName = "Termos legais | Publish | Deve falhar quando titulo ausente")]
    public async Task PublishAsync_ShouldFail_WhenTitleIsMissing()
    {
        // Arrange
        var payload = new LegalTermsPublishPayloadDto(
            Title: "   ",
            HtmlContent: "<p>Termo</p>",
            ChangeSummary: "ajuste");

        // Act
        var result = await _service.PublishAsync(
            LegalTermsAudience.Client,
            payload,
            actorUserId: Guid.NewGuid(),
            actorEmail: "admin@consertapramim.com");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("legal_terms_title_required", result.ErrorCode);
        _legalTermsRepositoryMock.Verify(r => r.AddAsync(It.IsAny<LegalTermsDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        _legalTermsRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _adminAuditLogRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }

    /// <summary>
    /// Cenario: existe versao ativa anterior e admin publica uma nova versao.
    /// Passos: mocka lista com termo publicado e executa PublishAsync.
    /// Resultado esperado: versao anterior despublicada, nova versao criada e auditada.
    /// </summary>
    [Fact(DisplayName = "Termos legais | Publish | Deve criar nova versao e despublicar versao anterior")]
    public async Task PublishAsync_ShouldCreateNewVersion_AndUnpublishPrevious()
    {
        // Arrange
        var previousPublished = new LegalTermsDocument
        {
            Id = Guid.NewGuid(),
            Audience = LegalTermsAudience.Provider,
            Version = 1,
            Title = "Termo Prestador v1",
            HtmlContent = "<p>v1</p>",
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow.AddDays(-7),
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            UpdatedAt = DateTime.UtcNow.AddDays(-7)
        };

        var versions = new List<LegalTermsDocument> { previousPublished };
        LegalTermsDocument? addedDocument = null;

        _legalTermsRepositoryMock
            .Setup(r => r.ListByAudienceAsync(LegalTermsAudience.Provider, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);
        _legalTermsRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LegalTermsDocument>(), It.IsAny<CancellationToken>()))
            .Callback<LegalTermsDocument, CancellationToken>((doc, _) => addedDocument = doc)
            .Returns(Task.CompletedTask);
        _legalTermsRepositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _adminAuditLogRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AdminAuditLog>()))
            .Returns(Task.CompletedTask);

        var payload = new LegalTermsPublishPayloadDto(
            Title: "Termo Prestador v2",
            HtmlContent: "<h1>Termo v2</h1><p>conteudo</p>",
            ChangeSummary: "Atualizacao de clausulas operacionais");

        // Act
        var result = await _service.PublishAsync(
            LegalTermsAudience.Provider,
            payload,
            actorUserId: Guid.NewGuid(),
            actorEmail: "admin@consertapramim.com");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.Document!.Version);
        Assert.True(result.Document.IsPublished);
        Assert.False(previousPublished.IsPublished);
        Assert.NotNull(addedDocument);
        Assert.Equal(2, addedDocument!.Version);
        Assert.True(addedDocument.IsPublished);

        _legalTermsRepositoryMock.Verify(r => r.AddAsync(It.IsAny<LegalTermsDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        _legalTermsRepositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _adminAuditLogRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Once);
    }

    /// <summary>
    /// Cenario: API recebe audience em formatos validos e invalidos.
    /// Passos: executa TryParseAudience com entradas conhecidas.
    /// Resultado esperado: apenas client/provider (ou aliases) sao aceitos.
    /// </summary>
    [Theory(DisplayName = "Termos legais | Parse Audience | Deve reconhecer audience valido")]
    [InlineData("client", true, LegalTermsAudience.Client)]
    [InlineData("cliente", true, LegalTermsAudience.Client)]
    [InlineData("provider", true, LegalTermsAudience.Provider)]
    [InlineData("prestador", true, LegalTermsAudience.Provider)]
    [InlineData("invalid", false, LegalTermsAudience.Client)]
    [InlineData("", false, LegalTermsAudience.Client)]
    public void TryParseAudience_ShouldParseExpectedAudience(
        string rawAudience,
        bool expectedSuccess,
        LegalTermsAudience expectedAudience)
    {
        // Act
        var success = LegalTermsService.TryParseAudience(rawAudience, out var parsedAudience);

        // Assert
        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedAudience, parsedAudience);
    }
}
