# Runtime image for the DuckRun Control Dashboard.
#
# This Dockerfile expects its BUILD CONTEXT to be an already-published,
# framework-dependent app directory (the output of `dotnet publish -c Release`),
# NOT the source tree. The release pipeline
# (.github/workflows/release-dashboard.yml) publishes the backend first and then
# builds this image with that publish folder as the context, so the image is
# bit-for-bit the same app as the framework-dependent IIS download.
#
# To build it by hand:
#   dotnet publish "Control Dashboard - Backend/Control Dashboard - Backend.csproj" -c Release -o ./app-publish
#   docker build -f deploy/dashboard.Dockerfile -t duckrun-dashboard ./app-publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY . ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DuckRun.Dashboard.dll"]
