FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/BorsaAnaliz.Web/BorsaAnaliz.Web.csproj", "src/BorsaAnaliz.Web/"]
RUN dotnet restore "src/BorsaAnaliz.Web/BorsaAnaliz.Web.csproj"

COPY . .
RUN dotnet publish "src/BorsaAnaliz.Web/BorsaAnaliz.Web.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet BorsaAnaliz.Web.dll
