FROM node:24.12.0-bookworm-slim AS frontend
WORKDIR /frontend
COPY PortalTarefas.Frontend/package*.json ./
RUN npm ci
COPY PortalTarefas.Frontend/ ./
COPY Contratos/ /Contratos/
RUN npm test && npm run build
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY PortalTarefas.Web/ ./PortalTarefas.Web/
RUN dotnet restore PortalTarefas.Web/PortalTarefas.Web.csproj
RUN dotnet publish PortalTarefas.Web/PortalTarefas.Web.csproj -c Release -o /out --no-restore
ARG RELEASE_SHA=development
RUN printf '{"commit":"%s"}\n' "$RELEASE_SHA" > /out/wwwroot/deployment-version.json
COPY --from=frontend /frontend/dist/ /out/wwwroot/app/
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
ARG RELEASE_SHA=development
LABEL org.opencontainers.image.revision=$RELEASE_SHA
USER app
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=4s --start-period=30s CMD curl --fail http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet","PortalTarefas.Web.dll"]
