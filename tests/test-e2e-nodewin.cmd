@echo off
set current_dir=%cd%
cd %~dp0

rmdir /S /Q .\tmp-node-test 2>nul
mkdir tmp-node-test
cd tmp-node-test
copy ..\node-app\package.json .
copy ..\node-app\*.js .

echo -- npm install --
call npm install 

echo -- run nibbler --
dotnet run --framework net6.0 -p ../../Nibbler -- --from-image registry.hub.docker.com/library/node:12 --to-image localhost:5000/node-win-test --to-insecure --add ".:/app" --addFolder "/app:1001:1001:777" --workdir /app --cmd "node server.js" -v

cd %current_dir% 

echo "-------- Pull and run built image locally at http://localhost:8086/ --------"
echo "-- remember to stop the running image: docker ps, docker stop nibbler-node-win-test-app"

docker pull localhost:5000/node-win-test
docker run -d --rm -p 8086:8080 --name nibbler-node-win-test-app localhost:5000/node-win-test
docker ps -a -f name=nibbler-node-win-test-app
