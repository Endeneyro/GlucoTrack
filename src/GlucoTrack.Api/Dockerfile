FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/GlucoTrack.Api/GlucoTrack.Api.csproj", "src/GlucoTrack.Api/"]
COPY ["src/GlucoTrack.Shared/GlucoTrack.Shared.csproj", "src/GlucoTrack.Shared/"]
RUN dotnet restore "src/GlucoTrack.Api/GlucoTrack.Api.csproj"
COPY . .
RUN dotnet publish "src/GlucoTrack.Api/GlucoTrack.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GlucoTrack.Api.dll"]
