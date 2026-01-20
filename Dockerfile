
# ------------------------ Build stage ------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Evita dependência de fallback folders e garante cache/local previsível
ENV DOTNET_CLI_HOME=/tmp
ENV NUGET_PACKAGES=/root/.nuget/packages

# Copia apenas o csproj e restaura com fallback desabilitado
COPY src/UsersAPI/UsersAPI.csproj src/UsersAPI/
RUN dotnet restore src/UsersAPI/UsersAPI.csproj -p:DisableImplicitNuGetFallbackFolder=true

# Copia o restante do código e publica
COPY src/UsersAPI/ src/UsersAPI/
RUN dotnet publish src/UsersAPI/UsersAPI.csproj -c Release -o /app/publish --no-restore \
    -p:DisableImplicitNuGetFallbackFolder=true

# ------------------------ Runtime stage ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "UsersAPI.dll"]

