FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /build
RUN git clone --depth=1 https://github.com/Coflnet/HypixelSkyblock.git dev \
    && git clone --depth=1 https://github.com/Coflnet/SkyUpdater.git \
    && git clone --depth=1 -b implementation https://github.com/Coflnet/SkySniper.git \
    && git clone --depth=1 https://github.com/Coflnet/SkyModCommands.git \
    && git clone --depth=1 https://github.com/Coflnet/SkyFilter.git \
    && git clone --depth=1 https://github.com/Ekwav/websocket-sharp.git \
    && git clone --depth=1 https://github.com/Ekwav/Hypixel.NET.git \
    && git clone --depth=1 https://github.com/Coflnet/SkyBackendForFrontend.git
WORKDIR /build/sky
COPY SkyBFCS.csproj SkyBFCS.csproj
RUN dotnet restore
COPY . .
RUN dotnet publish -c release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8000

RUN useradd --uid $(shuf -i 2000-65000 -n 1) random
USER random

ENTRYPOINT ["dotnet", "SkyBFCS.dll", "--hostBuilder:reloadConfigOnChange=false"]
