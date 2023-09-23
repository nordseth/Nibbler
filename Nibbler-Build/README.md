# Nibbler-Build

[![NuGet (Nibbler-Build)](https://img.shields.io/nuget/v/Nibbler-Build)](https://www.nuget.org/packages/Nibbler-Build/)

Inspired by https://github.com/tmds/build-image

usage: nibbler-build [Project] --to-image <image>

todo:
- support setting correct user based on base image meta data?
- to image options
- consider supporting to-file and/or to-archive
- output digest file?

Project file settings (defaults)
<NibblerConfiguration>Debug</NibblerConfiguration>
<NibblerRuntime>linux-x64</NibblerRuntime>
<NibblerSelfContained>true</NibblerSelfContained>
<NibblerFromImage>mcr.microsoft.com/dotnet/aspnet:6.0</NibblerFromImage>