FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MoneyTracker.Api/MoneyTracker.Api.csproj", "src/MoneyTracker.Api/"]
COPY ["src/MoneyTracker.Domain/MoneyTracker.Domain.csproj", "src/MoneyTracker.Domain/"]
COPY ["src/MoneyTracker.Infrastructure/MoneyTracker.Infrastructure.csproj", "src/MoneyTracker.Infrastructure/"]
RUN dotnet restore "src/MoneyTracker.Api/MoneyTracker.Api.csproj"

COPY . .

# 1. Install EF Core CLI Tool in the build environment
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# 2. Generate a self-contained migration bundle executable
WORKDIR /src
RUN dotnet ef migrations bundle --project src/MoneyTracker.Infrastructure/MoneyTracker.Infrastructure.csproj --startup-project src/MoneyTracker.Api/MoneyTracker.Api.csproj -o /app/publish/migrate

# 3. Publish the Web API app
WORKDIR /src/src/MoneyTracker.Api
RUN dotnet publish "MoneyTracker.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 10000

# 4. Modify ENTRYPOINT to execute the migration bundle before running the API
ENTRYPOINT ["sh", "-c", "./migrate --connection \"$ConnectionStrings__DefaultConnection\" && ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet MoneyTracker.Api.dll"]
