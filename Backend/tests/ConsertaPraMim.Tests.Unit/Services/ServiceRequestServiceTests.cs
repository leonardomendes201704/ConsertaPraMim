using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ServiceRequestServiceTests
{
    private readonly Mock<IServiceRequestRepository> _requestRepoMock;
    private readonly Mock<IServiceCategoryRepository> _serviceCategoryRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IZipGeocodingService> _zipGeocodingServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ServiceRequestService _service;

    public ServiceRequestServiceTests()
    {
        _requestRepoMock = new Mock<IServiceRequestRepository>();
        _serviceCategoryRepoMock = new Mock<IServiceCategoryRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _zipGeocodingServiceMock = new Mock<IZipGeocodingService>();
        _notificationServiceMock = new Mock<INotificationService>();

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid userId) => new User
            {
                Id = userId,
                Role = UserRole.Client,
                IsActive = true
            });
        _serviceCategoryRepoMock
            .Setup(r => r.GetFirstActiveByLegacyAsync(It.IsAny<ServiceCategory>()))
            .ReturnsAsync((ServiceCategory category) => new ServiceCategoryDefinition
            {
                Id = Guid.NewGuid(),
                Name = category.ToPtBr(),
                Slug = category.ToString().ToLowerInvariant(),
                LegacyCategory = category,
                IsActive = true
            });
        _service = new ServiceRequestService(
            _requestRepoMock.Object,
            _serviceCategoryRepoMock.Object,
            _userRepoMock.Object,
            _zipGeocodingServiceMock.Object,
            _notificationServiceMock.Object);
    }

    /// <summary>
    /// Cenario: cliente valido abre uma solicitacao de servico com dados consistentes.
    /// Passos: resolve coordenadas via CEP, cria request e executa CreateAsync para persistir o pedido.
    /// Resultado esperado: retorno de Guid nao vazio e chamada ao repositorio com o mesmo cliente/descricao informados.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Criar | Deve retornar guid quando sucesso")]
    public async Task CreateAsync_ShouldReturnGuid_WhenSuccess()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var dto = new CreateServiceRequestDto(
            CategoryId: null,
            Category: ServiceCategory.Electrical,
            Description: "Fix my lamp",
            Street: "Street",
            City: "City",
            Zip: "123",
            Lat: -23.5,
            Lng: -46.6);
        _zipGeocodingServiceMock
            .Setup(x => x.ResolveCoordinatesAsync(dto.Zip, dto.Street, dto.City))
            .ReturnsAsync(("123", -23.5, -46.6, dto.Street, dto.City));

        // Act
        var result = await _service.CreateAsync(clientId, dto);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _requestRepoMock.Verify(r => r.AddAsync(It.Is<ServiceRequest>(req => 
            req.ClientId == clientId && req.Description == dto.Description)), Times.Once);
    }

    /// <summary>
    /// Cenario: cliente tenta abrir solicitacao escolhendo categoria explicitamente inativa.
    /// Passos: monta DTO com CategoryId existente, repositorio retorna definicao com IsActive=false e chama CreateAsync.
    /// Resultado esperado: InvalidOperationException e nenhuma gravacao de pedido no repositorio.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Criar | Deve throw quando selected category inactive")]
    public async Task CreateAsync_ShouldThrow_WhenSelectedCategoryIsInactive()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var dto = new CreateServiceRequestDto(
            CategoryId: categoryId,
            Category: null,
            Description: "Fix my sink",
            Street: "Street",
            City: "City",
            Zip: "11704150",
            Lat: -23.5,
            Lng: -46.6);

        _zipGeocodingServiceMock
            .Setup(x => x.ResolveCoordinatesAsync(dto.Zip, dto.Street, dto.City))
            .ReturnsAsync((dto.Zip, dto.Lat, dto.Lng, dto.Street, dto.City));

        _serviceCategoryRepoMock
            .Setup(r => r.GetByIdAsync(categoryId))
            .ReturnsAsync(new ServiceCategoryDefinition
            {
                Id = categoryId,
                Name = "Eletrica",
                Slug = "eletrica",
                LegacyCategory = ServiceCategory.Electrical,
                IsActive = false
            });

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(clientId, dto));
        _requestRepoMock.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>()), Times.Never);
    }

    /// <summary>
    /// Cenario: token informa cliente inexistente para criacao da solicitacao.
    /// Passos: mocka GetByIdAsync retornando nulo e tenta executar CreateAsync com payload valido.
    /// Resultado esperado: UnauthorizedAccessException e bloqueio completo do fluxo de persistencia.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Criar | Deve throw nao autorizado quando cliente nao exist")]
    public async Task CreateAsync_ShouldThrowUnauthorized_WhenClientDoesNotExist()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var dto = new CreateServiceRequestDto(
            CategoryId: null,
            Category: ServiceCategory.Electrical,
            Description: "Fix my lamp",
            Street: "Street",
            City: "City",
            Zip: "123",
            Lat: -23.5,
            Lng: -46.6);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(clientId))
            .ReturnsAsync((User?)null);

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(clientId, dto));
        _requestRepoMock.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>()), Times.Never);
    }

    /// <summary>
    /// Cenario: cliente consulta sua lista de pedidos.
    /// Passos: repositorio devolve pedidos vinculados ao ClientId e o servico executa GetAllAsync com role Client.
    /// Resultado esperado: retorno somente das solicitacoes do cliente autenticado.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter all | Deve retornar cliente requisicoes quando usuario cliente")]
    public async Task GetAllAsync_ShouldReturnClientRequests_WhenUserIsClient()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requests = new List<ServiceRequest> 
        { 
            new ServiceRequest { Id = Guid.NewGuid(), ClientId = userId, Description = "Req 1", Status = ServiceRequestStatus.Created, Category = ServiceCategory.Plumbing } 
        };
        _requestRepoMock.Setup(r => r.GetByClientIdAsync(userId)).ReturnsAsync(requests);

        // Act
        var result = await _service.GetAllAsync(userId, "Client");

        // Assert
        Assert.Single(result);
        Assert.Equal("Req 1", result.First().Description);
    }

    /// <summary>
    /// Cenario: prestador com perfil completo busca oportunidades compativeis.
    /// Passos: carrega base/radius/categorias do perfil e chama busca de matching no repositorio.
    /// Resultado esperado: lista contem apenas pedidos elegiveis para o raio e categorias do prestador.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter all | Deve retornar matching requisicoes quando usuario prestador com profile")]
    public async Task GetAllAsync_ShouldReturnMatchingRequests_WhenUserIsProviderWithProfile()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User 
        { 
            Id = providerId, 
            ProviderProfile = new ProviderProfile 
            { 
                BaseLatitude = -23.0, 
                BaseLongitude = -46.0, 
                RadiusKm = 10, 
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical } 
            } 
        };
        
        var matchingReqs = new List<ServiceRequest> 
        { 
            new ServiceRequest { Id = Guid.NewGuid(), Description = "Matching", Status = ServiceRequestStatus.Created, Category = ServiceCategory.Electrical } 
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock.Setup(r => r.GetMatchingForProviderAsync(
            provider.ProviderProfile.BaseLatitude.Value, 
            provider.ProviderProfile.BaseLongitude.Value, 
            provider.ProviderProfile.RadiusKm, 
            provider.ProviderProfile.Categories,
            null))
            .ReturnsAsync(matchingReqs);

        // Act
        var result = await _service.GetAllAsync(providerId, "Provider");

        // Assert
        Assert.Single(result);
        Assert.Equal("Matching", result.First().Description);
    }

    /// <summary>
    /// Cenario: prestador sem ProviderProfile acessa listagem de pedidos.
    /// Passos: servico identifica ausencia de perfil e cai no fallback de leitura global de requests.
    /// Resultado esperado: retorno apenas de pedidos em status Created, filtrando status nao captaveis.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter all | Deve retornar all criado quando prestador tem no profile")]
    public async Task GetAllAsync_ShouldReturnAllCreated_WhenProviderHasNoProfile()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var requests = new List<ServiceRequest> 
        { 
            new ServiceRequest { Id = Guid.NewGuid(), Status = ServiceRequestStatus.Created },
            new ServiceRequest { Id = Guid.NewGuid(), Status = ServiceRequestStatus.InProgress }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(new User { Id = providerId, ProviderProfile = null });
        _requestRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(requests);

        // Act
        var result = await _service.GetAllAsync(providerId, "Provider");

        // Assert
        Assert.Single(result); // Only 'Created' one
    }

    /// <summary>
    /// Cenario: prestador possui perfil, mas nao encontra pedidos compativeis.
    /// Passos: busca de matching retorna colecao vazia para categorias/raio informados.
    /// Resultado esperado: GetAllAsync devolve lista vazia sem erro, indicando ausencia de oportunidades.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter all | Deve retornar vazio quando prestador tem no matching categories")]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenProviderHasNoMatchingCategories()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User 
        { 
            ProviderProfile = new ProviderProfile { Categories = new List<ServiceCategory> { ServiceCategory.Electrical }, BaseLatitude = 0, BaseLongitude = 0 } 
        };
        
        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock.Setup(r => r.GetMatchingForProviderAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), provider.ProviderProfile.Categories, null))
            .ReturnsAsync(new List<ServiceRequest>()); // No matching

        // Act
        var result = await _service.GetAllAsync(providerId, "Provider");

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Cenario: cliente solicita detalhes de um pedido existente que ele possui acesso.
    /// Passos: repositorio retorna entidade com o mesmo Id solicitado e servico projeta para DTO.
    /// Resultado esperado: DTO preenchido e com identificador igual ao pedido consultado.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter por id | Deve retornar dto quando requisicao existe")]
    public async Task GetByIdAsync_ShouldReturnDto_WhenRequestExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = id,
            ClientId = clientId,
            Description = "Test",
            Status = ServiceRequestStatus.Created,
            Category = ServiceCategory.Other
        };
        _requestRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(request);

        // Act
        var result = await _service.GetByIdAsync(id, clientId, UserRole.Client.ToString());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
    }

    /// <summary>
    /// Cenario: consulta de pedido por Id inexistente.
    /// Passos: repositorio responde null para o identificador buscado e o servico processa o retorno.
    /// Resultado esperado: metodo retorna null de forma segura, sem excecao desnecessaria.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter por id | Deve retornar nulo quando requisicao nao exist")]
    public async Task GetByIdAsync_ShouldReturnNull_WhenRequestDoesNotExist()
    {
        // Arrange
        _requestRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ServiceRequest?)null);

        // Act
        var result = await _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Client.ToString());

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Cenario: prestador visualiza mapa com pedidos dentro e fora do seu interesse.
    /// Passos: servico calcula distancia/categoria para requests distintas e ordena pins por relevancia.
    /// Resultado esperado: primeiro pin eh o pedido dentro do raio e categoria; demais refletem flags corretas.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter map pins for prestador | Deve retornar ordered pins com inside outside e category flags")]
    public async Task GetMapPinsForProviderAsync_ShouldReturnOrderedPins_WithInsideOutsideAndCategoryFlags()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User
        {
            Id = providerId,
            ProviderProfile = new ProviderProfile
            {
                BaseLatitude = 0,
                BaseLongitude = 0,
                RadiusKm = 10,
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
            }
        };

        var insideAndMatching = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Electrical,
            Description = "Pedido dentro e categoria atendida",
            AddressStreet = "Rua A",
            AddressCity = "Cidade A",
            AddressZip = "11111-111",
            Latitude = 0.03,
            Longitude = 0
        };

        var outsideAndCategoryMiss = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Plumbing,
            Description = "Pedido fora e categoria nao atendida",
            AddressStreet = "Rua B",
            AddressCity = "Cidade B",
            AddressZip = "22222-222",
            Latitude = 0.20,
            Longitude = 0
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock
            .Setup(r => r.GetOpenWithinRadiusAsync(
                0,
                0,
                It.Is<double>(d => Math.Abs(d - 40) < 0.0001)))
            .ReturnsAsync(new List<ServiceRequest> { outsideAndCategoryMiss, insideAndMatching });

        // Act
        var result = (await _service.GetMapPinsForProviderAsync(providerId)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(insideAndMatching.Id, result[0].RequestId);
        Assert.True(result[0].IsWithinInterestRadius);
        Assert.True(result[0].IsCategoryMatch);

        Assert.Equal(outsideAndCategoryMiss.Id, result[1].RequestId);
        Assert.False(result[1].IsWithinInterestRadius);
        Assert.False(result[1].IsCategoryMatch);
    }

    /// <summary>
    /// Cenario: prestador sem coordenadas-base tenta carregar pins no mapa.
    /// Passos: perfil vem sem latitude/longitude e o servico avalia precondicoes antes de consultar requests abertas.
    /// Resultado esperado: retorno vazio e nenhuma chamada ao repositorio de geoselecao.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter map pins for prestador | Deve retornar vazio quando prestador tem no base coordinates")]
    public async Task GetMapPinsForProviderAsync_ShouldReturnEmpty_WhenProviderHasNoBaseCoordinates()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                ProviderProfile = new ProviderProfile
                {
                    BaseLatitude = null,
                    BaseLongitude = null
                }
            });

        // Act
        var result = await _service.GetMapPinsForProviderAsync(providerId);

        // Assert
        Assert.Empty(result);
        _requestRepoMock.Verify(
            r => r.GetOpenWithinRadiusAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()),
            Times.Never);
    }

    /// <summary>
    /// Cenario: prestador aplica limite de distancia e quantidade na busca de pins.
    /// Passos: repositorio retorna pedidos perto/medio/longe e a chamada define maxDistanceKm=15 e take=1.
    /// Resultado esperado: lista final contem somente o pedido mais proximo dentro do limite informado.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao servico | Obter map pins for prestador | Deve respect max distance e take")]
    public async Task GetMapPinsForProviderAsync_ShouldRespectMaxDistanceAndTake()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User
        {
            Id = providerId,
            ProviderProfile = new ProviderProfile
            {
                BaseLatitude = 0,
                BaseLongitude = 0,
                RadiusKm = 10,
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical, ServiceCategory.Plumbing }
            }
        };

        var nearRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Electrical,
            Description = "Pedido perto",
            AddressStreet = "Rua 1",
            AddressCity = "Cidade 1",
            AddressZip = "11111-111",
            Latitude = 0.02,
            Longitude = 0
        };

        var midRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Plumbing,
            Description = "Pedido medio",
            AddressStreet = "Rua 2",
            AddressCity = "Cidade 2",
            AddressZip = "22222-222",
            Latitude = 0.11,
            Longitude = 0
        };

        var farRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Plumbing,
            Description = "Pedido longe",
            AddressStreet = "Rua 3",
            AddressCity = "Cidade 3",
            AddressZip = "33333-333",
            Latitude = 0.22,
            Longitude = 0
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock
            .Setup(r => r.GetOpenWithinRadiusAsync(
                0,
                0,
                It.Is<double>(d => Math.Abs(d - 15) < 0.0001)))
            .ReturnsAsync(new List<ServiceRequest> { farRequest, midRequest, nearRequest });

        // Act
        var result = (await _service.GetMapPinsForProviderAsync(providerId, maxDistanceKm: 15, take: 1)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(nearRequest.Id, result[0].RequestId);
        Assert.True(result[0].DistanceKm <= 15);
    }
}
