FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RelayChat.Node.Api/RelayChat.Node.Api.csproj", "RelayChat.Node.Api/"]
COPY ["RelayChat.Node.Database/RelayChat.Node.Database.csproj", "RelayChat.Node.Database/"]
COPY ["RelayChat.Node.Contracts/RelayChat.Node.Contracts.csproj", "RelayChat.Node.Contracts/"]
RUN dotnet restore "RelayChat.Node.Api/RelayChat.Node.Api.csproj"
COPY . .
WORKDIR "/src/RelayChat.Node.Api"
RUN dotnet build "./RelayChat.Node.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RelayChat.Node.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RelayChat.Node.Api.dll"]
