# Immutable Microsoft base manifests are supplied only by build-bff-development.py.
ARG SDK_IMAGE_DIGEST
ARG RUNTIME_IMAGE_DIGEST
FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:${SDK_IMAGE_DIGEST} AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/server/hosts/BffFoundation/ src/server/hosts/BffFoundation/
COPY src/server/hosts/BffDevelopmentHost/ src/server/hosts/BffDevelopmentHost/
COPY src/server/modules/IdentitySessions/ src/server/modules/IdentitySessions/
COPY migrations/identity-sessions/001-initial.sql migrations/identity-sessions/001-initial.sql
RUN dotnet restore src/server/hosts/BffDevelopmentHost/BffDevelopmentHost.csproj --locked-mode \
    && dotnet publish src/server/hosts/BffDevelopmentHost/BffDevelopmentHost.csproj \
        -c Release --no-restore --no-self-contained -o /out /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled-extra@sha256:${RUNTIME_IMAGE_DIGEST} AS runtime
WORKDIR /app
COPY --from=build /out/ ./
USER 1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "BffDevelopmentHost.dll"]
CMD ["--bff-development-disabled"]
# Runtime identity/database inputs are not image build arguments. Product readiness
# always fails. This hard-disabled package cannot be activated as a production BFF.
