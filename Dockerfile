FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MoneyTracker.Api/MoneyTracker.Api.csproj", "src/MoneyTracker.Api/"]
COPY ["src/MoneyTracker.Domain/MoneyTracker.Domain.csproj", "src/MoneyTracker.Domain/"]
COPY ["src/MoneyTracker.Infrastructure/MoneyTracker.Infrastructure.csproj", "src/MoneyTracker.Infrastructure/"]
RUN dotnet restore "src/MoneyTracker.Api/MoneyTracker.Api.csproj"

COPY . .
WORKDIR /src/src/MoneyTracker.Api
RUN dotnet publish "MoneyTracker.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 10000
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet MoneyTracker.Api.dll"]