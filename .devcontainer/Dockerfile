FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim-arm64v8
#FROM mcr.microsoft.com/devcontainers/dotnet:1-6.0-bullseye-arm64v8
#FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim-amd64

RUN git clone https://github.com/Azure/azure-functions-core-tools
RUN apt-get update && apt-get -y install procps
RUN cd azure-functions-core-tools \
#    #&& wget -O test/Azure.Functions.Cli.Tests/Azure.Functions.Cli.Tests.csproj https://raw.githubusercontent.com/Azure/azure-functions-core-tools/357f49b7759e6d81e91a858bce0796b4973ddece/test/Azure.Functions.Cli.Tests/Azure.Functions.Cli.Tests.csproj \
    && dotnet build Azure.Functions.Cli.sln  \
    && dotnet publish src/Azure.Functions.Cli/Azure.Functions.Cli.csproj --runtime linux-arm64 --output /usr/local/src/azure-functions-core-tools/cli -f net8.0

RUN ln -s /usr/local/src/azure-functions-core-tools/cli/func /usr/local/bin/func