# Build Stage
FROM ://microsoft.com AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build the release
COPY . ./
RUN dotnet publish -c Release -o out

# Runtime Stage
FROM ://microsoft.com AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Render dynamically assigns a port, ASP.NET 8 listens on 8080 by default
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MoneyTracker.dll"]
