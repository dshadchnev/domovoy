FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Domovoy.DeviceManager.Service/Domovoy.DeviceManager.Service.csproj", "Domovoy.DeviceManager.Service/"]
RUN dotnet restore "Domovoy.DeviceManager.Service/Domovoy.DeviceManager.Service.csproj"
COPY . .
WORKDIR "/src/Domovoy.DeviceManager.Service"
RUN dotnet build "Domovoy.DeviceManager.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Domovoy.DeviceManager.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.DeviceManager.Service.dll"]