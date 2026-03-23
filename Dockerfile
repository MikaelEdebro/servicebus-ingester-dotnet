FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY src/ServiceBusIngester/*.csproj .
RUN dotnet restore -r linux-x64
COPY src/ServiceBusIngester/ .
RUN dotnet publish -c Release -o /app -r linux-x64 --self-contained true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./ServiceBusIngester"]
