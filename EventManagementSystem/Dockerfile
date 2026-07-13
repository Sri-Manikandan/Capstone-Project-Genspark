# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first so `restore` is cached independently of source changes.
COPY EMS.sln .
COPY EMSApplicationLayer/EMSApplicationLayer.csproj EMSApplicationLayer/
COPY EMSBLLLibrary/EMSBLLLibrary.csproj             EMSBLLLibrary/
COPY EMSDALLibrary/EMSDALLibrary.csproj             EMSDALLibrary/
COPY EMSModelLibrary/EMSModelLibrary.csproj         EMSModelLibrary/
COPY EMSTests/EMSTests.csproj                       EMSTests/
RUN dotnet restore EMSApplicationLayer/EMSApplicationLayer.csproj

COPY . .
RUN dotnet publish EMSApplicationLayer/EMSApplicationLayer.csproj \
    -c Release -o /app --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Run as non-root. The aspnet image ships an "app" user.
USER app

COPY --from=build --chown=app:app /app .

# Kestrel binds 8080; ports below 1024 need root, which we do not have.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EMSApplicationLayer.dll"]
