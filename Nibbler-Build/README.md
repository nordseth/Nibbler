# Nibbler-Build

[![NuGet (Nibbler-Build)](https://img.shields.io/nuget/v/Nibbler-Build)](https://www.nuget.org/packages/Nibbler-Build/)

Inspired by https://github.com/tmds/build-image

usage: nibbler-build [Project] --to-image <image>

todo:
- support setting correct user based on base image meta data
- to image options
- consider supporting to-file
- update to-file, to docker compadible tar bundle https://github.com/moby/moby/tree/master/image/spec
- read base image from project property "BaseImage"
- output digest file