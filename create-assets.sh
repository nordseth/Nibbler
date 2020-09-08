#!/bin/bash
set -ex

PUBLISHARGS="-p:PublishSingleFile=true -p:PublishTrimmed=true -p:InvariantGlobalization=true"
FRAMEWORK=netcoreapp3.1
CONFIGURATION=Release
PROJECT=Nibbler
VERSION=$(minver --tag-prefix v -v error)
FOLDER=assets

dotnet pack $PROJECT -o $FOLDER

WIN=win-x64
dotnet publish $PROJECT $PUBLISHARGS -c $CONFIGURATION -f $FRAMEWORK -r $WIN
zip $FOLDER/$PROJECT.${VERSION}_$WIN.zip $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$WIN/publish/$PROJECT.exe

LINUX=linux-x64
dotnet publish $PROJECT $PUBLISHARGS -c $CONFIGURATION -f $FRAMEWORK -r $LINUX
tar -czvf $FOLDER/$PROJECT.${VERSION}_$LINUX.tar.gz --mode='a+x' --owner=0 -C $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$LINUX/publish/ $PROJECT

ls $FOLDER/