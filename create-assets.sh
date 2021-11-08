#!/bin/bash
set -ex

PUBLISHARGS="-p:PublishSingleFile=true -p:PublishTrimmed=true -p:InvariantGlobalization=true --self-contained"
FRAMEWORK=net6.0
CONFIGURATION=Release
PROJECT=Nibbler
COMMAND=nibbler
VERSION=$(minver -t v -v e)
FOLDER=assets

dotnet pack $PROJECT -o $FOLDER

WIN=win-x64
dotnet publish $PROJECT $PUBLISHARGS -c $CONFIGURATION -f $FRAMEWORK -r $WIN
mv $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$WIN/publish/$PROJECT.exe $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$WIN/publish/$COMMAND.exe
rm -f $FOLDER/$COMMAND.${VERSION}_$WIN.zip
# note: how to install zip on gitbash https://stackoverflow.com/a/55749636
#   The foler is probably $Home\AppData\Local\Programs\Git\mingw64\bin
zip -j $FOLDER/$COMMAND.${VERSION}_$WIN.zip $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$WIN/publish/$COMMAND.exe

LINUX=linux-x64
dotnet publish $PROJECT $PUBLISHARGS -c $CONFIGURATION -f $FRAMEWORK -r $LINUX
mv $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$LINUX/publish/$PROJECT $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$LINUX/publish/$COMMAND
tar -czvf $FOLDER/$COMMAND.${VERSION}_$LINUX.tar.gz --mode='a+x' --owner=0 -C $PROJECT/bin/$CONFIGURATION/$FRAMEWORK/$LINUX/publish/ $COMMAND

ls $FOLDER/