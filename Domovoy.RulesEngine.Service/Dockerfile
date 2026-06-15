FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Domovoy.Shared/Domovoy.Shared.csproj", "Domovoy.Shared/"]
COPY ["Domovoy.RulesEngine.Service/Domovoy.RulesEngine.Service.csproj", "Domovoy.RulesEngine.Service/"]
RUN dotnet restore "Domovoy.RulesEngine.Service/Domovoy.RulesEngine.Service.csproj"
COPY . .
WORKDIR "/src/Domovoy.RulesEngine.Service"
RUN dotnet build "Domovoy.RulesEngine.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Domovoy.RulesEngine.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.RulesEngine.Service.dll"]


