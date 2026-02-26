FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build

WORKDIR /code

COPY SQL-Script-Flatten.API.sln .

COPY src src
COPY test test

RUN dotnet restore -s https://api.nuget.org/v3/index.json -s https://nexus.build.zuto.cloud/nexus/service/local/nuget/cl4u/

RUN dotnet test -c Release --no-restore
RUN dotnet publish --no-restore src/SQL-Script-Flatten.API/SQL-Script-Flatten.API.csproj -o /build -c Release

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime

RUN apt-get update && apt-get install -y \
    tzdata

WORKDIR /app
COPY --from=build /build /app

EXPOSE 8080

ENV TZ=Europe/London

USER app

ENTRYPOINT ["dotnet", "SQL-Script-Flatten.API.dll"]