FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RelayChat.ControlPlane.Api/RelayChat.ControlPlane.Api.csproj", "RelayChat.ControlPlane.Api/"]
COPY ["RelayChat.ControlPlane.Database/RelayChat.ControlPlane.Database.csproj", "RelayChat.ControlPlane.Database/"]
RUN dotnet restore "RelayChat.ControlPlane.Api/RelayChat.ControlPlane.Api.csproj"
COPY . .
WORKDIR "/src/RelayChat.ControlPlane.Api"
RUN dotnet build "./RelayChat.ControlPlane.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RelayChat.ControlPlane.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RelayChat.ControlPlane.Api.dll"]
