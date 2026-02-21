using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ReviewServiceTests
{
    private readonly Mock<IReviewRepository> _reviewRepoMock;
    private readonly Mock<IServiceRequestRepository> _requestRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _reviewRepoMock = new Mock<IReviewRepository>();
        _requestRepoMock = new Mock<IServiceRequestRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reviews:EvaluationWindowDays"] = "30"
            })
            .Build();

        _service = new ReviewService(
            _reviewRepoMock.Object,
            _requestRepoMock.Object,
            _userRepoMock.Object,
            configuration);
    }

    /// <summary>
    /// Cenario: o cliente conclui uma avaliacao valida para um atendimento pago e finalizado.
    /// Passos: o teste prepara requisicao concluida com proposta aceita, sem review previo, e envia nota maxima.
    /// Resultado esperado: a review eh persistida e o rating medio do prestador eh recalculado com incremento de quantidade.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit cliente review | Deve calculate average quando sucesso")]
    public async Task SubmitClientReviewAsync_ShouldCalculateAverage_WhenSuccess()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        
        var request = new ServiceRequest 
        { 
            Id = requestId, 
            ClientId = clientId, 
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        var provider = new User 
        { 
            Id = providerId, 
            ProviderProfile = new ProviderProfile { Rating = 4.0, ReviewCount = 1 } 
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock.Setup(r => r.GetByRequestAndReviewerAsync(requestId, clientId)).ReturnsAsync((Review?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);

        // Act
        // New rating 5.0. Old: (4.0 * 1) = 4.0. New: (4.0 + 5.0) / 2 = 4.5
        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 5, "Great!"));

        // Assert
        Assert.True(result);
        Assert.Equal(4.5, provider.ProviderProfile.Rating);
        Assert.Equal(2, provider.ProviderProfile.ReviewCount);
        _reviewRepoMock.Verify(r => r.AddAsync(It.Is<Review>(review =>
            review.RequestId == requestId &&
            review.ClientId == clientId &&
            review.ProviderId == providerId &&
            review.ReviewerUserId == clientId &&
            review.ReviewerRole == UserRole.Client &&
            review.RevieweeUserId == providerId &&
            review.RevieweeRole == UserRole.Provider &&
            review.Rating == 5 &&
            review.Comment == "Great!")), Times.Once);
        _userRepoMock.Verify(r => r.UpdateAsync(provider), Times.Once);
    }

    /// <summary>
    /// Cenario: o mesmo cliente tenta avaliar novamente a mesma requisicao.
    /// Passos: o teste simula existencia de review anterior do mesmo reviewer para o mesmo request.
    /// Resultado esperado: o servico rejeita a segunda avaliacao e nao grava novo registro.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit cliente review | Deve retornar falso quando same reviewer already reviewed requisicao")]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenSameReviewerAlreadyReviewedRequest()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock
            .Setup(r => r.GetByRequestAndReviewerAsync(requestId, clientId))
            .ReturnsAsync(new Review
            {
                RequestId = requestId,
                ReviewerUserId = clientId
            });

        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 4, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: o cliente tenta avaliar um servico que ainda nao foi concluido.
    /// Passos: o teste usa requisicao em status inicial e solicita envio de review.
    /// Resultado esperado: a operacao retorna falso por regra de elegibilidade de status.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit cliente review | Deve retornar falso quando requisicao nao completed")]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenRequestNotCompleted()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest { Status = ServiceRequestStatus.Created };
        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        // Act
        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 5, ""));

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Cenario: o prestador aceito quer avaliar o cliente apos finalizar o atendimento pago.
    /// Passos: o teste monta requisicao concluida com proposta aceita para o prestador e sem review anterior.
    /// Resultado esperado: a avaliacao eh criada para o cliente como reviewee, sem alterar score de prestador.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit prestador review | Deve criar review quando prestador accepted")]
    public async Task SubmitProviderReviewAsync_ShouldCreateReview_WhenProviderIsAccepted()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock.Setup(r => r.GetByRequestAndReviewerAsync(requestId, providerId)).ReturnsAsync((Review?)null);

        var result = await _service.SubmitProviderReviewAsync(providerId, new CreateReviewDto(requestId, 5, "Cliente colaborou."));

        Assert.True(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.Is<Review>(review =>
            review.RequestId == requestId &&
            review.ClientId == clientId &&
            review.ProviderId == providerId &&
            review.ReviewerUserId == providerId &&
            review.ReviewerRole == UserRole.Provider &&
            review.RevieweeUserId == clientId &&
            review.RevieweeRole == UserRole.Client &&
            review.Rating == 5 &&
            review.Comment == "Cliente colaborou.")), Times.Once);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    /// <summary>
    /// Cenario: um prestador que nao venceu a proposta tenta avaliar o cliente.
    /// Passos: o teste fornece requisicao com proposta aceita de outro prestador.
    /// Resultado esperado: a submissao de review eh negada e nenhum dado eh persistido.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit prestador review | Deve retornar falso quando prestador nao accepted")]
    public async Task SubmitProviderReviewAsync_ShouldReturnFalse_WhenProviderIsNotAccepted()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = Guid.NewGuid(), Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitProviderReviewAsync(providerId, new CreateReviewDto(requestId, 4, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: o prestador tenta duplicar avaliacao no mesmo atendimento.
    /// Passos: o teste sinaliza review existente com mesmo provider como reviewer.
    /// Resultado esperado: o servico retorna falso e impede duplicidade de avaliacao.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit prestador review | Deve retornar falso quando same reviewer already reviewed requisicao")]
    public async Task SubmitProviderReviewAsync_ShouldReturnFalse_WhenSameReviewerAlreadyReviewedRequest()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock
            .Setup(r => r.GetByRequestAndReviewerAsync(requestId, providerId))
            .ReturnsAsync(new Review
            {
                RequestId = requestId,
                ReviewerUserId = providerId
            });

        var result = await _service.SubmitProviderReviewAsync(providerId, new CreateReviewDto(requestId, 4, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: o cliente tenta avaliar uma requisicao concluida, mas com pagamento pendente.
    /// Passos: o teste configura transacao em status Pending e aciona submit de review do cliente.
    /// Resultado esperado: a avaliacao eh bloqueada por falta de pagamento confirmado.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit cliente review | Deve retornar falso quando requisicao unpaid")]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenRequestIsUnpaid()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Pending }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 5, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: o prestador tenta avaliar cliente em requisicao ainda nao paga.
    /// Passos: o teste prepara request completado com proposta aceita e transacao pendente.
    /// Resultado esperado: o submit de review do prestador retorna falso e nao cria avaliacao.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit prestador review | Deve retornar falso quando requisicao unpaid")]
    public async Task SubmitProviderReviewAsync_ShouldReturnFalse_WhenRequestIsUnpaid()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Pending }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitProviderReviewAsync(providerId, new CreateReviewDto(requestId, 5, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: um cliente sem ownership tenta avaliar atendimento de outro cliente.
    /// Passos: o teste usa actor diferente do ClientId real da requisicao.
    /// Resultado esperado: o servico rejeita a tentativa para preservar integridade da autoria.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit cliente review | Deve retornar falso quando cliente nao own requisicao")]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenClientDoesNotOwnRequest()
    {
        var realClientId = Guid.NewGuid();
        var attackerClientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = realClientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitClientReviewAsync(attackerClientId, new CreateReviewDto(requestId, 1, "fraude"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: a janela de avaliacao expira apos o periodo configurado.
    /// Passos: o teste define requisicao concluida com data de atualizacao alem de 30 dias.
    /// Resultado esperado: a review nao eh aceita por estar fora da janela temporal permitida.
    /// </summary>
    [Fact(DisplayName = "Review servico | Submit cliente review | Deve retornar falso quando review window expired")]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenReviewWindowIsExpired()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            UpdatedAt = DateTime.UtcNow.AddDays(-31),
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 4, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: o sistema precisa exibir resumo de reputacao do prestador com media e distribuicao por estrelas.
    /// Passos: o teste retorna conjunto de reviews com notas variadas para o mesmo prestador.
    /// Resultado esperado: o resumo calcula media correta, total e contagem por faixa de nota.
    /// </summary>
    [Fact(DisplayName = "Review servico | Obter prestador score summary | Deve retornar average e distribution")]
    public async Task GetProviderScoreSummaryAsync_ShouldReturnAverageAndDistribution()
    {
        var providerId = Guid.NewGuid();
        _reviewRepoMock
            .Setup(r => r.GetByRevieweeAsync(providerId, UserRole.Provider))
            .ReturnsAsync(new List<Review>
            {
                new() { Rating = 5, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider },
                new() { Rating = 4, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider },
                new() { Rating = 4, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider },
                new() { Rating = 2, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider }
            });

        var summary = await _service.GetProviderScoreSummaryAsync(providerId);

        Assert.Equal(providerId, summary.UserId);
        Assert.Equal(UserRole.Provider, summary.UserRole);
        Assert.Equal(3.75, summary.AverageRating);
        Assert.Equal(4, summary.TotalReviews);
        Assert.Equal(1, summary.FiveStarCount);
        Assert.Equal(2, summary.FourStarCount);
        Assert.Equal(0, summary.ThreeStarCount);
        Assert.Equal(1, summary.TwoStarCount);
        Assert.Equal(0, summary.OneStarCount);
    }

    /// <summary>
    /// Cenario: um cliente ainda nao possui avaliacoes recebidas.
    /// Passos: o teste configura repositorio retornando lista vazia para o usuario.
    /// Resultado esperado: o summary retorna todos os indicadores zerados sem erro.
    /// </summary>
    [Fact(DisplayName = "Review servico | Obter cliente score summary | Deve retornar zero summary quando no reviews")]
    public async Task GetClientScoreSummaryAsync_ShouldReturnZeroSummary_WhenNoReviews()
    {
        var clientId = Guid.NewGuid();
        _reviewRepoMock
            .Setup(r => r.GetByRevieweeAsync(clientId, UserRole.Client))
            .ReturnsAsync(new List<Review>());

        var summary = await _service.GetClientScoreSummaryAsync(clientId);

        Assert.Equal(clientId, summary.UserId);
        Assert.Equal(UserRole.Client, summary.UserRole);
        Assert.Equal(0, summary.AverageRating);
        Assert.Equal(0, summary.TotalReviews);
        Assert.Equal(0, summary.FiveStarCount);
        Assert.Equal(0, summary.FourStarCount);
        Assert.Equal(0, summary.ThreeStarCount);
        Assert.Equal(0, summary.TwoStarCount);
        Assert.Equal(0, summary.OneStarCount);
    }

    /// <summary>
    /// Cenario: parte relacionada ao atendimento denuncia uma review por conteudo inadequado.
    /// Passos: o teste reporta a review com ator autorizado e motivo preenchido.
    /// Resultado esperado: o status de moderacao muda para Reported e os campos de denuncia sao persistidos.
    /// </summary>
    [Fact(DisplayName = "Review servico | Report review | Deve set reported quando actor pode report")]
    public async Task ReportReviewAsync_ShouldSetReported_WhenActorCanReport()
    {
        var reviewId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var review = new Review
        {
            Id = reviewId,
            ClientId = clientId,
            ProviderId = providerId,
            ReviewerUserId = clientId,
            ModerationStatus = ReviewModerationStatus.None
        };

        _reviewRepoMock.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.ReportReviewAsync(
            reviewId,
            providerId,
            UserRole.Provider,
            new ReportReviewDto("Comentario ofensivo"));

        Assert.True(result);
        Assert.Equal(ReviewModerationStatus.Reported, review.ModerationStatus);
        Assert.Equal(providerId, review.ReportedByUserId);
        Assert.Equal("Comentario ofensivo", review.ReportReason);
        Assert.NotNull(review.ReportedAtUtc);
        _reviewRepoMock.Verify(r => r.UpdateAsync(review), Times.Once);
    }

    /// <summary>
    /// Cenario: o proprio autor tenta denunciar a review que ele escreveu.
    /// Passos: o teste usa reporter com mesmo id do ReviewerUserId.
    /// Resultado esperado: a denuncia eh recusada e nenhuma atualizacao eh aplicada.
    /// </summary>
    [Fact(DisplayName = "Review servico | Report review | Deve retornar falso quando reporter author")]
    public async Task ReportReviewAsync_ShouldReturnFalse_WhenReporterIsAuthor()
    {
        var reviewId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var review = new Review
        {
            Id = reviewId,
            ClientId = authorId,
            ProviderId = Guid.NewGuid(),
            ReviewerUserId = authorId,
            ModerationStatus = ReviewModerationStatus.None
        };

        _reviewRepoMock.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.ReportReviewAsync(
            reviewId,
            authorId,
            UserRole.Client,
            new ReportReviewDto("Nao gostei"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: um usuario externo sem relacao com a transacao tenta denunciar uma review.
    /// Passos: o teste informa ator nao participante e sem perfil administrativo.
    /// Resultado esperado: o reporte eh negado para evitar abuso por terceiros.
    /// </summary>
    [Fact(DisplayName = "Review servico | Report review | Deve retornar falso quando actor nao related e nao admin")]
    public async Task ReportReviewAsync_ShouldReturnFalse_WhenActorIsNotRelatedAndNotAdmin()
    {
        var reviewId = Guid.NewGuid();
        var outsiderUserId = Guid.NewGuid();

        var review = new Review
        {
            Id = reviewId,
            ClientId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            ReviewerUserId = Guid.NewGuid(),
            ModerationStatus = ReviewModerationStatus.None
        };

        _reviewRepoMock.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.ReportReviewAsync(
            reviewId,
            outsiderUserId,
            UserRole.Client,
            new ReportReviewDto("Denuncia indevida"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: o usuario tenta reportar review sem justificar o motivo.
    /// Passos: o teste envia razao apenas com espacos em branco.
    /// Resultado esperado: o servico falha na validacao inicial sem consultar ou alterar a review.
    /// </summary>
    [Fact(DisplayName = "Review servico | Report review | Deve retornar falso quando reason blank")]
    public async Task ReportReviewAsync_ShouldReturnFalse_WhenReasonIsBlank()
    {
        var result = await _service.ReportReviewAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            UserRole.Client,
            new ReportReviewDto("   "));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
    }

    /// <summary>
    /// Cenario: o admin modera uma review denunciada e decide ocultar o comentario.
    /// Passos: o teste executa moderacao com decisao HideComment e justificativa administrativa.
    /// Resultado esperado: a review passa para status Hidden com metadados de moderacao preenchidos.
    /// </summary>
    [Fact(DisplayName = "Review servico | Moderate review | Deve hide comment quando decision hide comment")]
    public async Task ModerateReviewAsync_ShouldHideComment_WhenDecisionIsHideComment()
    {
        var reviewId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var review = new Review
        {
            Id = reviewId,
            ModerationStatus = ReviewModerationStatus.Reported
        };

        _reviewRepoMock.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.ModerateReviewAsync(
            reviewId,
            adminId,
            new ModerateReviewDto("HideComment", "Abuso confirmado"));

        Assert.True(result);
        Assert.Equal(ReviewModerationStatus.Hidden, review.ModerationStatus);
        Assert.Equal(adminId, review.ModeratedByAdminId);
        Assert.Equal("Abuso confirmado", review.ModerationReason);
        Assert.NotNull(review.ModeratedAtUtc);
        _reviewRepoMock.Verify(r => r.UpdateAsync(review), Times.Once);
    }
}
