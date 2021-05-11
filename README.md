# Nibbler

[![NuGet (Nibbler)](https://img.shields.io/nuget/v/Nibbler)](https://www.nuget.org/packages/Nibbler/)

Nibbler is a tool for doing simple changes to OCI images, often called docker images. 
It is publised as a dotnet tool and as an executable where dotnet sdk is not installed.

The tool reads image meta data from a registry, makes changes to meta data and can add folders as new layers in the image.
It can not read image layers or execute anything inside the image.

Typical use case is adding build artifacts to create a new image from a existing base image created with another tool.
It does not need root or any other privileges, so is well suited for running in a Kubernetes pod.

## Status

Nibbler should not be considered stable, but it is used for building production images.
Missing feature as mostly better error handling and messages.
The test set is limited, especially around authentication methods used with different image registries.

## Why Nibbler

Why use Nibbler instead of other tools?

Nibbler was created because no tool could do simple changes to images in a secure environemnt. 

Solutions based on Dockerfile (like [docker](https://docs.docker.com/engine/reference/commandline/build/), [Kaniko  ](https://github.com/GoogleContainerTools/kaniko) and partly [Builda](https://github.com/containers/buildah)) are built around the Dockerfile and running operations inside the container that is being built.
When running on a build server this functionality is not needed, the artifacts are already created and only need to be copied into a new layer in the image.
Nibbler is inspired by tools like [Jib](https://github.com/GoogleContainerTools/jib). But its less opinionated and lets the user decide how to create the image.
Bazel might be a alternative, but does a lot more than just creating images.

Nibbler was created for building dotnet images, as such it is publised as as a dotnet cli tool. But its also made available an executable.
Nibbler is language agnostic, and can be used for creating images for other platforms, like node and go.

## Usage

```
$ dotnet tool install --global Nibbler
$ nibbler --help
Nibbler v1.x.x
Do simple changes to OCI images

Usage: nibbler [options]

Options:
  -?|-h|--help            Show help information
  --from-image            Set from image (required)
  --from-insecure         Insecure from registry (http)
  --from-skip-tls-verify  Skip verifying from registry TLS certificate
  --from-username         From registry username
  --from-password         From registry password
  --to-image              To image (required)
  --to-insecure           Insecure to registry (http)
  --to-skip-tls-verify    Skip verifying to registry TLS certificate
  --to-username           To registry username
  --to-password           To registry password
  --add                   Add contents of a folder to the image 'sourceFolder:destFolder[:ownerId:groupId:permissions]'
  --addFolder             Add a folder to the image 'destFolder[:ownerId:groupId:permissions]'
  --label                 Add label to the image 'name=value'
  --env                   Add a environment variable to the image 'name=value'
  --git-labels            Add common git labels to image, optionally define the path to the git repo.
  --git-labels-prefix     Specify the prefix of the git labels. (default: 'nibbler.git')
  --workdir               Set the working directory in the image
  --user                  Set the user in the image
  --cmd                   Set the image cmd
  --entrypoint            Set the image entrypoint
  -v|--debug              Verbose output
  --dry-run               Does not push, only shows what would happen (use with -v)
  --docker-config         Specify docker config file for authentication with registry. (default: ~/.docker/config.json)
  --username              Registry username (deprecated)
  --password              Registry password (deprecated)
  --insecure              Insecure registry (http). Only use if base image and destination is the same registry.
  --skip-tls-verify       Skip verifying registry TLS certificate. Only use if base image and destination is the same
                          registry.
  --temp-folder           Set temp folder (default: ./.nibbler)
  --digest-file           Output image digest to file, optionally specify file
```

## Example build script

```
dotnet publish -o $PWD/artifacts
nibbler \
	--from-image mcr.microsoft.com/dotnet/aspnet:5.0 \
	--to-image my-registy.com/repo/image:latest  \
	--add "artifacts:/app" \
	--workdir /app \
	--entrypoint "dotnet MyApp.dll" 
```

## Features

- uses docker registry api v2
  - https://github.com/opencontainers/distribution-spec/blob/master/spec.md
- supports oci image manifest and ocean image config spec
  - https://github.com/opencontainers/image-spec/blob/master/manifest.md
  - https://github.com/opencontainers/image-spec/blob/master/config.md
- layers and image is created as "reproducible", that means dates in image config and in file system layers are always the same.
  - files added are always set with same modified date
- uses docker-config.json for authentication.
- uses "./.nibbler" to store layers
  - folder is not cleaned up. Can be overwritten with "--temp-folder"
  
## Work arounds

For Docker Hub use "registry.hub.docker.com" as registry.
If using a _library_ image, remember to include "library" in the url.
If credentials for "registry.hub.docker.com" isn't found in docker config, Nibbler will fallback on "https://index.docker.io/v1/" as source for credentials.