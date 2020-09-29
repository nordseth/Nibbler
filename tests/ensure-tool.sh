#!/bin/bash
set -e

###################################
# Script to ensure that correct Nibbler version is installed
# As there no easy way of doing this with dotnet cli
# Not currently used.
###################################

tool=nibbler
version=1.4.0
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