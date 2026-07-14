FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Domovoy.MockDevice.Server/Domovoy.MockDevice.Server.csproj", "Domovoy.MockDevice.Server/"]
RUN dotnet restore "Domovoy.MockDevice.Server/Domovoy.MockDevice.Server.csproj"
COPY . .
WORKDIR "/src/Domovoy.MockDevice.Server"
RUN dotnet build "Domovoy.MockDevice.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Domovoy.MockDevice.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.MockDevice.Server.dll"]


