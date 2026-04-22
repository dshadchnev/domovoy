FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app; EXPOSE 8080
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Domovoy.Shared/Domovoy.Shared.csproj", "Domovoy.Shared/"]
COPY ["Domovoy.Domain/Domovoy.Domain.csproj", "Domovoy.Domain/"]
COPY ["Domovoy.ApiGateway/Domovoy.ApiGateway.csproj", "Domovoy.ApiGateway/"]
RUN dotnet restore "Domovoy.ApiGateway/Domovoy.ApiGateway.csproj"
COPY . .
WORKDIR "/src/Domovoy.ApiGateway"
RUN dotnet publish -c Release -o /app/publish
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.ApiGateway.dll"]