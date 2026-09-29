FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0-bookworm

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl jq nodejs npm \
    && npm install --global azure-functions-core-tools@4 --unsafe-perm=true \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

USER vscode