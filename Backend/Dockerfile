# Estágio de Base / Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Estágio de SDK para Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto e restaurar dependências
COPY ["src/ConsertaPraMim.API/ConsertaPraMim.API.csproj", "src/ConsertaPraMim.API/"]
COPY ["src/ConsertaPraMim.Application/ConsertaPraMim.Application.csproj", "src/ConsertaPraMim.Application/"]
COPY ["src/ConsertaPraMim.Domain/ConsertaPraMim.Domain.csproj", "src/ConsertaPraMim.Domain/"]
COPY ["src/ConsertaPraMim.Infrastructure/ConsertaPraMim.Infrastructure.csproj", "src/ConsertaPraMim.Infrastructure/"]

RUN dotnet restore "src/ConsertaPraMim.API/ConsertaPraMim.API.csproj"

# Copiar todo o código e compilar
COPY . .
WORKDIR "/src/src/ConsertaPraMim.API"
RUN dotnet build "ConsertaPraMim.API.csproj" -c Release -o /app/build

# Estágio de Publicação
FROM build AS publish
RUN dotnet publish "ConsertaPraMim.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsertaPraMim.API.dll"]
