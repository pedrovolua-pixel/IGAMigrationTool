# Build from repository root using this Dockerfile and its adjacent ignore file.
# Required build arguments are verified sha256 digests (64 lowercase hex digits)
# for the selected platform's approved Microsoft SDK and ASP.NET image manifests:
# docker build --platform linux/amd64 \
#   --build-arg SDK_IMAGE_DIGEST=<verified-sdk-manifest-digest> \
#   --build-arg RUNTIME_IMAGE_DIGEST=<verified-aspnet-manifest-digest> \
#   -f infra/containers/azure-development-bootstrap.Dockerfile .
# Digests must be supplied; an absent digest fails FROM resolution. No tag fallback.
ARG SDK_IMAGE_DIGEST
ARG RUNTIME_IMAGE_DIGEST
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:${SDK_IMAGE_DIGEST} AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/server/hosts/AzureDevelopmentBootstrap/ src/server/hosts/AzureDevelopmentBootstrap/
RUN dotnet restore src/server/hosts/AzureDevelopmentBootstrap/AzureDevelopmentBootstrap.csproj --locked-mode \
    && dotnet publish src/server/hosts/AzureDevelopmentBootstrap/AzureDevelopmentBootstrap.csproj \
        -c Release --no-restore --no-self-contained -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled-extra@sha256:${RUNTIME_IMAGE_DIGEST} AS runtime
WORKDIR /app
COPY --from=build /out/ ./
USER 1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "AzureDevelopmentBootstrap.dll"]
CMD ["--azure-development-bootstrap", "--role", "web"]
# Worker command replaces CMD with: --azure-development-bootstrap --role worker
# HTTP readiness always returns 503; this is not an enabled product application.
