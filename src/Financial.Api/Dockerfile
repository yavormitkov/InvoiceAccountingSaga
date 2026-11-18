FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Financial.Api/Financial.Api.csproj", "src/Financial.Api/"]
COPY ["src/Shared.Events/Shared.Events.csproj", "src/Shared.Events/"]

RUN dotnet restore "src/Financial.Api/Financial.Api.csproj"
COPY . .
WORKDIR /src/src/Financial.Api
RUN dotnet publish "Financial.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Financial.Api.dll"]
