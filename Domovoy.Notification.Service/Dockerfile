FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Domovoy.Notification.Service/Domovoy.Notification.Service.csproj", "Domovoy.Notification.Service/"]
RUN dotnet restore "Domovoy.Notification.Service/Domovoy.Notification.Service.csproj"
COPY . .
WORKDIR "/src/Domovoy.Notification.Service"
RUN dotnet build "Domovoy.Notification.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Domovoy.Notification.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.Notification.Service.dll"]


