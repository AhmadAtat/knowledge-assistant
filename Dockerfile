# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/KnowledgeAssistant.Api/KnowledgeAssistant.Api.csproj src/KnowledgeAssistant.Api/
RUN dotnet restore src/KnowledgeAssistant.Api/KnowledgeAssistant.Api.csproj

COPY src/ src/
RUN dotnet publish src/KnowledgeAssistant.Api/KnowledgeAssistant.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KnowledgeAssistant.Api.dll"]
