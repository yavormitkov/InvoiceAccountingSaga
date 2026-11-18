FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Invoicing.Api/Invoicing.Api.csproj", "src/Invoicing.Api/"]
COPY ["src/Shared.Events/Shared.Events.csproj", "src/Shared.Events/"]
RUN dotnet restore "src/Invoicing.Api/Invoicing.Api.csproj"
COPY . .
WORKDIR /src/src/Invoicing.Api
RUN dotnet publish -c Release -o /app/publish /p:DisableImplicitNuGetFallbackFolder=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Invoicing.Api.dll"]
