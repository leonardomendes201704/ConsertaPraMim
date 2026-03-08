using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.DTOs;

public class LandingLeadDtoJsonTests
{
    /// <summary>
    /// Cenario: browser da landing envia `origin` como string (`Client`/`Provider`) no JSON.
    /// Passos: desserializar o payload usando as opcoes padrao do ASP.NET Core (`System.Text.Json` case-insensitive).
    /// Resultado esperado: DTO aceita o valor textual sem exigir enum numerico.
    /// </summary>
    [Fact(DisplayName = "Landing lead dto | JSON | Deve desserializar origin textual enviado pela landing")]
    public void CaptureLandingLeadRequestDto_ShouldDeserializeStringOrigin()
    {
        const string json = """
            {
              "origin": "Client",
              "fullName": "Leonardo Silva",
              "phone": "13999999999",
              "email": "leo@exemplo.com",
              "city": "Praia Grande",
              "state": "SP",
              "neighborhood": "Ocian",
              "serviceCategory": "Hidraulica",
              "requestedService": "Troca de registro",
              "companyName": null,
              "companyDocument": null,
              "yearsOfExperience": null,
              "message": "Preciso de atendimento rapido.",
              "currentPageUrl": "https://www.consertapramim.com/#captacao",
              "referrerUrl": "",
              "queryString": "",
              "utmSource": "",
              "utmMedium": "",
              "utmCampaign": "",
              "utmTerm": "",
              "utmContent": "",
              "browserLanguage": "pt-BR",
              "screenResolution": "1920x1080",
              "devicePlatform": "Windows",
              "timeZone": "America/Sao_Paulo"
            }
            """;

        var dto = JsonSerializer.Deserialize<CaptureLandingLeadRequestDto>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Assert.NotNull(dto);
        Assert.Equal(LandingLeadOrigin.Client, dto!.Origin);
    }
}
