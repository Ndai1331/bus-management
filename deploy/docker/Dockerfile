FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG PROJECT
ARG APP_DLL
ARG PUBLISH_PROPERTIES=""
WORKDIR /src
COPY . .
# appsettings.secrets.json is intentionally excluded from the Docker context.
# DbMigrator declares an empty development file as publish content, so hydrate
# only that required placeholder inside the disposable build stage.
RUN printf '{}\n' > src/HCS.DbMigrator/appsettings.secrets.json
RUN dotnet publish "$PROJECT" --configuration Release --output /out /p:UseAppHost=false $PUBLISH_PROPERTIES

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG APP_DLL
ARG INSTALL_LIBREOFFICE=false
ENV APP_DLL=$APP_DLL \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
USER root
RUN apt-get update \
    && apt-get install --no-install-recommends -y libgssapi-krb5-2 \
    && if [ "$INSTALL_LIBREOFFICE" = "true" ]; then apt-get install --no-install-recommends -y libreoffice-writer; fi \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet ${APP_DLL}"]
