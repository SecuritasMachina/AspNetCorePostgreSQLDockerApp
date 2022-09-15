#FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS build-env

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app
ENV DOTNET_EnableDiagnostics=1



# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

#RUN mkdir -p /mnt/offsite
# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0

#
#RUN dotnet dev-certs https
#RUN dotnet dev-certs https --trust
#RUN dotnet dev-certs https --clean
WORKDIR /app

COPY --from=build-env /app/out .

RUN apt-get update && \ 
    apt-get install ca-certificates -y 
   
RUN update-ca-certificates && \
    apt-get install openssl -y && \
    apt-get install git -y && \
    apt-get clean && \
    echo export SSL_CERT_DIR=/etc/ssl/certs >> /etc/bash.bashrc && \
    echo export CURL_CA_BUNDLE=/etc/ssl/certs/ca-certificates.crt >> /etc/bash.bashrc
    
#COPY --from=build /etc/ssl/certs/ /etc/ssl/certs/
#COPY ["/etc/ssl/certs", "/etc/ssl/certs"]
#RUN sed -i "s|TLSv1.2|TLSv1.0|g" /etc/ssl/openssl.cnf

ENTRYPOINT ["dotnet", "SecuritasMachinaOffsiteAgent.dll"]

#docker build -t securitasmachinaoffsiteagent .
#docker tag securitasmachinaoffsiteagent  securitasmachina2022/securitasmachinaoffsiteagent:latest
#docker push securitasmachina2022/securitasmachinaoffsiteagent:latest