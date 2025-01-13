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
RUN git clone --depth=1 https://github.com/NotEnoughUpdates/NotEnoughUpdates-REPO.git NEU-REPO \
    && rm -rf NEU-REPO/.git NEU-REPO/items 
COPY SkyBFCS.csproj SkyBFCS.csproj
RUN dotnet restore
COPY . .
RUN mkdir Mock && mkdir -p /app/Mock && cp /build/SkySniper/Mock/ Mock/ -r \
    && mkdir -p /build/sky/bin/Debug/net8.0/Mock /build/sky/bin/release/net8.0/Mock
RUN dotnet test && dotnet publish -c release -o /app && rm -r /app/items.json /app/Mock

FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8000

RUN useradd --uid $(shuf -i 2000-65000 -n 1) random && dotnet tool install --global dotnet-counters
USER random

ENTRYPOINT ["dotnet", "SkyBFCS.dll", "--hostBuilder:reloadConfigOnChange=false"]
