FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Domovoy.Dashboard.Service/Domovoy.Dashboard.Service.csproj", "Domovoy.Dashboard.Service/"]
RUN dotnet restore "Domovoy.Dashboard.Service/Domovoy.Dashboard.Service.csproj"
COPY . .
WORKDIR "/src/Domovoy.Dashboard.Service"
RUN dotnet build "Domovoy.Dashboard.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Domovoy.Dashboard.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.Dashboard.Service.dll"]

