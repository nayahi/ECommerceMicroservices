# =============================================
# ETAPA 1: Base - Imagen de runtime
# =============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# =============================================
# ETAPA 2: Build - Compilación
# =============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copiar archivos de proyecto para restore
COPY ["UserService/UserService.csproj", "UserService/"]
COPY ["ECommerce.Common/ECommerce.Common.csproj", "ECommerce.Common/"]

# Restaurar dependencias
RUN dotnet restore "UserService/UserService.csproj"

# Copiar todo el código fuente
COPY . .

# Compilar
WORKDIR "/src/UserService"
RUN dotnet build "UserService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# =============================================
# ETAPA 3: Publish - Publicación
# =============================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UserService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# =============================================
# ETAPA 4: Final - Imagen final
# =============================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UserService.dll"]