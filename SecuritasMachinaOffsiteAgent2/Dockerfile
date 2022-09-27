FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app
ENV DOTNET_EnableDiagnostics=1
# Copy everything
COPY *.csproj ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
#
WORKDIR /app

COPY --from=build-env /app/out .

RUN apt-get update && \ 
    apt-get install ca-certificates -y && \
    update-ca-certificates && \
    apt-get install openssl -y && \
    apt-get install git -y && \
    apt-get clean && \
    echo export SSL_CERT_DIR=/etc/ssl/certs >> /etc/bash.bashrc && \
    echo export CURL_CA_BUNDLE=/etc/ssl/certs/ca-certificates.crt >> /etc/bash.bashrc

#ENV TZ="America/New_York" apt install tzdata -y && \

ENTRYPOINT ["dotnet", "SecuritasMachinaOffsiteAgent.dll"]
