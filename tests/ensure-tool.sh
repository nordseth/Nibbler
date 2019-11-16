#!/bin/bash
set -e

tool=nibbler
version=1.0.0-beta.5
#echo "ensure tool: $tool, version $version"

if [ -f $HOME/.dotnet/tools/$tool ]; then
	if [ -d $HOME/.dotnet/tools/.store/$tool/$version ]; then	
		echo "$tool $version is installed"
	else
		echo "$tool has incorrect version - will reinstall"
		dotnet tool uninstall -g $tool
		dotnet tool install -g $tool --version $version
	fi
else
	echo "$tool is NOT installed - installing"
	dotnet tool install -g $tool --version $version
fi