FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["negosuite-api.csproj", "."]

RUN dotnet restore "./negosuite-api.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "negosuite-api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "negosuite-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "negosuite-api.dll"]