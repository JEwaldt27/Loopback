# Loopback container image.
#
# Two stages: the first has the full .NET SDK and compiles the app; the second is a much
# smaller runtime-only image that receives just the compiled output. Only the second stage
# is shipped, so the ~800MB SDK never ends up in the published image.
#
# Build:  docker build -t loopback .
# Run:    docker run -d -p 8080:8080 -v loopback-data:/data --name loopback loopback
# Easier: docker compose up -d   (see docker-compose.yml)

# ---------------------------------------------------------------------------
# Stage 1 — build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy ONLY the project files first, then restore NuGet packages. Docker caches each
# instruction as a layer and reuses it while its inputs are unchanged — so editing a
# .razor file invalidates the source copy below but NOT this restore, making rebuilds
# much faster. (Copying everything up front would re-download packages every time.)
COPY Client/Client.csproj Client/
COPY Server/Server.csproj Server/
RUN dotnet restore Server/Server.csproj

# Now the actual source. Server.csproj references Client.csproj, so building the server
# also builds and bundles the Blazor WebAssembly client.
#
# Note this deliberately builds Server.csproj rather than the solution: the solution also
# contains Desktop/ (a .NET MAUI Windows app), which cannot build on Linux and isn't part
# of serving the web app.
COPY Client/ Client/
COPY Server/ Server/
RUN dotnet publish Server/Server.csproj -c Release -o /publish --no-restore

# ---------------------------------------------------------------------------
# Stage 2 — runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publish .

# Mutable data (device library + user accounts) lives OUTSIDE the app directory so a
# volume can be mounted at /data without hiding the application files. Without this the
# app would write devices.json/users.json next to its own binaries, and everything would
# be lost the moment the container is recreated (e.g. on any upgrade).
ENV LINEFLOW_DATA_DIR=/data

# Run as the image's built-in non-root user rather than root. /data is created and handed
# to that user here; a *named volume* (as in docker-compose.yml) inherits this ownership
# automatically. A *bind mount* to a host folder does not — see the README if you mount a
# host path and hit "permission denied".
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data
USER $APP_UID
VOLUME ["/data"]

# Kestrel listens here inside the container. This is the container's own port; map it to
# whatever host port you like with -p <host>:8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Server.dll"]
