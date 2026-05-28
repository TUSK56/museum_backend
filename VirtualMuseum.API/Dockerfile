FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["VirtualMuseum.API/VirtualMuseum.API.csproj", "VirtualMuseum.API/"]
COPY ["VirtualMuseum.Application/VirtualMuseum.Application.csproj", "VirtualMuseum.Application/"]
COPY ["VirtualMuseum.Infrastructure/VirtualMuseum.Infrastructure.csproj", "VirtualMuseum.Infrastructure/"]
COPY ["VirtualMuseum.Domain/VirtualMuseum.Domain.csproj", "VirtualMuseum.Domain/"]
RUN dotnet restore "VirtualMuseum.API/VirtualMuseum.API.csproj"

COPY . .
WORKDIR /src/VirtualMuseum.API
RUN dotnet publish "VirtualMuseum.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "VirtualMuseum.API.dll"]
