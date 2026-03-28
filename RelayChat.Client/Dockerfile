FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG WASM_APPLICATION_ENVIRONMENT_NAME=Production
ARG NODE_API_BASE_URL=TODO
ARG CONTROLPLANE_API_BASE_URL=TODO
WORKDIR /src
COPY ["RelayChat.Client/RelayChat.Client.csproj", "RelayChat.Client/"]
COPY ["RelayChat.Node.Contracts/RelayChat.Node.Contracts.csproj", "RelayChat.Node.Contracts/"]
RUN dotnet restore "RelayChat.Client/RelayChat.Client.csproj"
COPY . .
WORKDIR "/src/RelayChat.Client"
RUN printf '{\n  "NodeApi": {\n    "BaseUrl": "%s"\n  },\n  "ControlPlaneApi": {\n    "BaseUrl": "%s"\n  }\n}\n' "$NODE_API_BASE_URL" "$CONTROLPLANE_API_BASE_URL" > "./wwwroot/appsettings.${WASM_APPLICATION_ENVIRONMENT_NAME}.json"
RUN dotnet build "./RelayChat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG WASM_APPLICATION_ENVIRONMENT_NAME=Production
RUN dotnet publish "./RelayChat.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:WasmApplicationEnvironmentName=$WASM_APPLICATION_ENVIRONMENT_NAME

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY RelayChat.Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=publish /app/publish/wwwroot ./
EXPOSE 8080
