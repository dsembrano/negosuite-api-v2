FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json negosuite-api.csproj ./
RUN dotnet restore negosuite-api.csproj
COPY Program.cs Startup.cs ./
COPY Controllers/ Controllers/
COPY Models/ Models/
COPY Services/ Services/
RUN dotnet publish negosuite-api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "negosuite-api.dll"]
