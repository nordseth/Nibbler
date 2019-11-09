# Nibbler

Nibbler is a dotnet tool for doing simple changes to OCI images.
The tool will read image meta data from a registry, change the meta data and add folders as new layers in the image.
It can not read image layers or execute anything inside the image.
Also it is assumed that the unmodified layers (the file system of the image) already exists in the destination registry, and will fail the push if they don't. 

Typical use case is adding build artifacts to create a new image from a existing base image created with another tool.
It does not need root or any other privileges, so is well suited for running in a Kubernetes pod.

## Usage

```
dotnet tool install -g Nibbler
nibbler --help
```

## Example build script

```
dotnet publish -o $PWD/artifacts
nibbler \
	--base-image my-registry.com/repo/baseimage:latest \
	--destination my-registy.com/repo/image:latest  \
	--add "artifacts:/app" \
	--workdir /app \
	--entrypoint "dotnet MyApp.dll" 
```

## Features

- uses docker registry api v2
  - https://docs.docker.com/registry/spec/api/
- supports oci image manifest and ocean image config spec
  - https://github.com/opencontainers/image-spec/blob/master/manifest.md
  - https://github.com/opencontainers/image-spec/blob/master/config.md
- layers and image is created as "reproducible", that means dates in image config and in file system layers are always the same.
- uses docker-config.json (with auth set) or username and password as authentication.
  - docker credential helpers are not supported
- uses "./.nibbler" to store files while working
  - directory is wiped on `nibbler init`
  - on push, image digest is written to the file "digest" in this folder
