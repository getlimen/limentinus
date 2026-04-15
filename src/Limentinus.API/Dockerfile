FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Limentinus.slnx ./
COPY src/ ./src/
RUN dotnet publish src/Limentinus.API/Limentinus.API.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
RUN apt-get update && apt-get install -y wireguard-tools iproute2 iptables && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
VOLUME ["/var/lib/limentinus"]
ENV LIMEN_IDENTITY_PATH=/var/lib/limentinus/identity.json
ENTRYPOINT ["dotnet", "Limentinus.API.dll"]
