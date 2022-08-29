docker build -t securitasmachinaoffsiteagent . --progress plain
docker tag securitasmachinaoffsiteagent  securitasmachina2022/securitasmachinaoffsiteagent:latest
docker push securitasmachina2022/securitasmachinaoffsiteagent:latest

#sudo openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout /etc/ssl/private/apache-selfsigned.key -out /etc/ssl/certs/apache-selfsigned.crt