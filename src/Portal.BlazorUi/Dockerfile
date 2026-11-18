FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Portal.BlazorUi/Portal.BlazorUi.csproj", "src/Portal.BlazorUi/"]
RUN dotnet restore "src/Portal.BlazorUi/Portal.BlazorUi.csproj"
COPY . .
WORKDIR /src/src/Portal.BlazorUi
RUN dotnet publish "Portal.BlazorUi.csproj" -c Debug -o /app/publish

FROM nginx:alpine AS runtime
WORKDIR /usr/share/nginx/html
RUN rm -rf ./*
COPY --from=build /app/publish/wwwroot .
COPY src/Portal.BlazorUi/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
