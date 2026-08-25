FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Domovoy.Web/Domovoy.Web.csproj", "Domovoy.Web/"]
COPY ["Domovoy.Shared/Domovoy.Shared.csproj", "Domovoy.Shared/"]
COPY ["Domovoy.Domain/Domovoy.Domain.csproj", "Domovoy.Domain/"]
RUN dotnet restore "Domovoy.Web/Domovoy.Web.csproj"
COPY . .
WORKDIR "/src/Domovoy.Web"
RUN dotnet build "Domovoy.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Domovoy.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Domovoy.Web.dll"]
