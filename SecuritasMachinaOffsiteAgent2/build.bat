docker build -t securitasmachinaoffsiteagent . --progress plain
docker tag securitasmachinaoffsiteagent  securitasmachina2022/securitasmachinaoffsiteagent:latest
docker push securitasmachina2022/securitasmachinaoffsiteagent:latest



rem  rsync -a * -e ssh charles@104.198.187.33:publish  -v -v --delete
rem sudo openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout /etc/ssl/private/apache-selfsigned.key -out /etc/ssl/certs/apache-selfsigned.crt