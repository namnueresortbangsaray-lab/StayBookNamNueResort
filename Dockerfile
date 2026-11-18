# syntax=docker/dockerfile:1

# 1. Front-end: contrast gate, fonts, icon sprite, Tailwind CSS and esbuild bundle -> wwwroot
FROM node:22-bookworm-slim AS assets
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY build.mjs tailwind.config.js ./
COPY build ./build
COPY Styles ./Styles
COPY Scripts ./Scripts
COPY Views ./Views
RUN npm run build

# 2. Restore (cached on the project file only) and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props StayBook.csproj ./
RUN dotnet restore StayBook.csproj
COPY . .
COPY --from=assets /web/wwwroot/css wwwroot/css
COPY --from=assets /web/wwwroot/js wwwroot/js
COPY --from=assets /web/wwwroot/fonts wwwroot/fonts
COPY --from=assets /web/wwwroot/icons.svg wwwroot/icons.svg
RUN dotnet publish StayBook.csproj -c Release -o /app --no-restore -p:UseAppHost=false

# 3. Runtime: Ubuntu-based ASP.NET image (ICU and tzdata included), non-root user "app" (uid 1654)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Asia/Bangkok
WORKDIR /app
COPY --from=build /app ./
RUN mkdir -p storage/database storage/uploads storage/backups storage/logs storage/keys \
    && chown -R app:app storage
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && grep -q '^ok' <&3"]
ENTRYPOINT ["dotnet", "StayBook.dll"]
