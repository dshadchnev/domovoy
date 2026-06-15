FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Domovoy.Shared/Domovoy.Shared.csproj", "Domovoy.Shared/"]
COPY ["Domovoy.CommandDispatcher.Service/Domovoy.CommandDispatcher.Service.csproj", "Domovoy.CommandDispatcher.Service/"]
RUN dotnet restore "Domovoy.CommandDispatcher.Service/Domovoy.CommandDispatcher.Service.csproj"
COPY . .
WORKDIR "/src/Domovoy.CommandDispatcher.Service"
RUN dotnet build "Domovoy.CommandDispatcher.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Domovoy.CommandDispatcher.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.CommandDispatcher.Service.dll"]


