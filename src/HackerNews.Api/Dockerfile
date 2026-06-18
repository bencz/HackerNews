FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/HackerNews.Api/HackerNews.Api.csproj", "src/HackerNews.Api/"]
COPY ["src/HackerNews.Application/HackerNews.Application.csproj", "src/HackerNews.Application/"]
COPY ["src/HackerNews.Domain/HackerNews.Domain.csproj", "src/HackerNews.Domain/"]
COPY ["src/HackerNews.Infrastructure/HackerNews.Infrastructure.csproj", "src/HackerNews.Infrastructure/"]
RUN dotnet restore "src/HackerNews.Api/HackerNews.Api.csproj"
COPY . .
WORKDIR "/src/src/HackerNews.Api"
RUN dotnet build "./HackerNews.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HackerNews.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HackerNews.Api.dll"]
