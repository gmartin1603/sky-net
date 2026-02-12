# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY SkyNet.sln ./

COPY simulator/SkyNet.Simulator.Contracts/SkyNet.Simulator.Contracts.csproj simulator/SkyNet.Simulator.Contracts/
COPY simulator/SkyNet.Simulator.Core/SkyNet.Simulator.Core.csproj simulator/SkyNet.Simulator.Core/
COPY simulator/SkyNet.Simulator.Daemon/SkyNet.Simulator.Daemon.csproj simulator/SkyNet.Simulator.Daemon/

RUN dotnet restore simulator/SkyNet.Simulator.Daemon/SkyNet.Simulator.Daemon.csproj

COPY . .

RUN dotnet publish simulator/SkyNet.Simulator.Daemon/SkyNet.Simulator.Daemon.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV urls=http://0.0.0.0:5070
EXPOSE 5070

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "SkyNet.Simulator.Daemon.dll"]
