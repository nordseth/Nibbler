#!/bin/bash
set -e

if [ -z "$1" ]; then
  echo "usage $0 [VERSION]"
  echo " example: $0 1.0.0-beta.1"
  exit 1
fi

VERSION=$1
BASE_FOLDER=$(git rev-parse --show-toplevel)
PROJECT=Nibbler

dotnet pack $BASE_FOLDER/$PROJECT -o $BASE_FOLDER/nuget -p:PackageVersion=$VERSION

echo "----------------"
echo "Consider doing: "
echo "  git tag -a v$VERSION -m \"new package version $VERSION\""
echo "  git push --tags"