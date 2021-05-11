# Integration tests

Run ./setup-test.sh to setup a test environment.
This requries docker running locally, and will start a docker registry, pull som images and push them to the local registry.

It also creates a folder "TestData", downloads and creates some test data in that folder.
Tests in solution also write som files to this directory.

The tests should result in a image localhost:5000/test/nibbler-test:latest, that can be pulled and run to verify.

```
docker pull localhost:5000/test/nibbler-test:latest
docker run -it --rm -p 8080:80 localhost:5000/test/nibbler-test:latest
```

to verify goto http://localhost:8080

## cleanup

```
docker stop registry
rm -r ./TestData
docker image rm registry:2
docker image rm mcr.microsoft.com/dotnet/aspnet:5.0
```
