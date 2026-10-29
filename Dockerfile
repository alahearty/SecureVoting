# syntax=docker/dockerfile:1.7

FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/SecureVoting.slnx ./
COPY backend/src/SecureVoting.Domain/SecureVoting.Domain.csproj src/SecureVoting.Domain/
COPY backend/src/SecureVoting.Application/SecureVoting.Application.csproj src/SecureVoting.Application/
COPY backend/src/SecureVoting.Infrastructure/SecureVoting.Infrastructure.csproj src/SecureVoting.Infrastructure/
COPY backend/src/SecureVoting.Api/SecureVoting.Api.csproj src/SecureVoting.Api/
RUN dotnet restore src/SecureVoting.Api/SecureVoting.Api.csproj
COPY backend/src/ ./src/
COPY --from=web /web/dist/ ./src/SecureVoting.Api/wwwroot/
RUN dotnet publish src/SecureVoting.Api/SecureVoting.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DataProtection__KeysPath=/data/keys \
    Storage__CandidatePhotosPath=/data/photos
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/keys /data/photos
COPY --from=build /app/publish ./
RUN chown -R app:app /app /data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "SecureVoting.Api.dll"]
