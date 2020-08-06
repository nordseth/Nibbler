# Nibbler

[![NuGet (Nibbler)](https://img.shields.io/nuget/v/Nibbler)](https://www.nuget.org/packages/Nibbler/)

Nibbler is a dotnet tool for doing simple changes to OCI images.
The tool will read image meta data from a registry, change the meta data and add folders as new layers in the image.
It can not read image layers or execute anything inside the image.

Typical use case is adding build artifacts to create a new image from a existing base image created with another tool.
It does not need root or any other privileges, so is well suited for running in a Kubernetes pod.

## Status

Nibbler is beta software. Its probably not feature complete or robust.

## Why Nibbler

Why use Nibbler instead of other tools?

Nibbler was created because only simple changes to images are needed when building in a secure environment. 
Solutions based on Dockerfile (like [docker](https://docs.docker.com/engine/reference/commandline/build/), [Kaniko  ](https://github.com/GoogleContainerTools/kaniko) and partly [Builda](https://github.com/containers/buildah)) are built around the Dockerfile and running operations inside the container that is being built.
When running on a build server this functionality is not needed, the artifacts are already created and only need to be copied into a new layer in the image.
Nibbler is inspired by tools like [Jib](https://github.com/GoogleContainerTools/jib). But instead of being opinionated lets the user decide how to create the image.
Bazel might be a alternative, but does a lot more than just creating images.

The use case was building dotnet images, so its packaged as a dotnet tool. In the future it might be packaged as a stand alone executable.

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
	--from-image mcr.microsoft.com/dotnet/core/aspnet:3.1 \
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