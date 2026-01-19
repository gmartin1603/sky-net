# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY SkyNet.sln ./

COPY simulator/SkyNet.Simulator.Contracts/SkyNet.Simulator.Contracts.csproj simulator/SkyNet.Simulator.Contracts/
COPY simulator/SkyNet.Simulator.Dashboard/SkyNet.Simulator.Dashboard.csproj simulator/SkyNet.Simulator.Dashboard/

RUN dotnet restore simulator/SkyNet.Simulator.Dashboard/SkyNet.Simulator.Dashboard.csproj

COPY . .

RUN dotnet publish simulator/SkyNet.Simulator.Dashboard/SkyNet.Simulator.Dashboard.csproj -c Release -o /app/publish

FROM nginx:1.27-alpine AS runtime

COPY simulator/SkyNet.Simulator.Dashboard/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
