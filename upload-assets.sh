#!/bin/bash
set -e

FOLDER=assets
PROJECT=Nibbler
COMMAND=nibbler
COMMIT=$(git rev-parse HEAD)
VERSION=$(minver --tag-prefix v -v error)
TAGCOMMIT=$(git rev-list -n 1 v$VERSION)

## validate COMMIT and TAGCOMMIT are equal
if [ "$COMMIT" != "$TAGCOMMIT" ]; then
    echo "ERROR: current commit does not match version tag for $VERSION"
    exit 1
fi

REPONAME=$(git config --get remote.origin.url | sed 's/.*:\/\/github.com\///;s/.git$//')

# Create a personal access token through GitHub UI, we only need “Repo” access
# Place it in your config by "git config --global github.token YOUR_TOKEN"
TOKEN=$(git config --global github.token)
AUTH="Authorization: token $TOKEN"

generate_post_data()
{
  cat <<EOF
{
  "tag_name": "v$VERSION",
  "name": "v$VERSION",
  "draft": true,
  "prerelease": false
}
EOF
}

GH_REPO="https://api.github.com/repos/$REPONAME"
ACCEPT="Accept: application/vnd.github.v3+json"

curl -o /dev/null --fail -sH "$AUTH" -H "$ACCEPT" $GH_REPO  || { echo "ERROR: Invalid repo, token or network issue!";  exit 1; }

echo "Creating new release for $VERSION"
RELASE_RESP=$(curl -sH "$AUTH" -H "$ACCEPT" --data "$(generate_post_data)" "$GH_REPO/releases")

RELASE_ID=$(echo $RELASE_RESP | jq ".id")
RELASE_UPLOAD_URL=$(echo $RELASE_RESP | jq ".upload_url")

if [ -z "$RELASE_ID" ] || [ "$RELASE_ID" = "null" ]; then
    echo "ERROR: Release ID not found, was the release created?"
    exit 1
fi

if [ -z "$RELASE_UPLOAD_URL" ] || [ "$RELASE_UPLOAD_URL" = "null" ]; then
    echo "ERROR: Release upload url not found, was the release created?"
    exit 1
fi

GH_UPLOAD="https://uploads.github.com/repos/$REPONAME/releases/$RELASE_ID/assets"

CONTENT_TYPE_NUGET="Content-Type: application/octet-stream"
FILE_NUGET="$PROJECT.$VERSION.nupkg"
echo "Uploading $FILE_NUGET to $VERSION"
RESP_NUGET_FILE=$(curl --data-binary @"$FOLDER/$FILE_NUGET" -sH "$AUTH" -H "$ACCEPT" -H "$CONTENT_TYPE_NUGET" "$GH_UPLOAD?name=$FILE_NUGET")

CONTENT_TYPE_WIN="Content-Type: application/zip"
FILE_WIN="$COMMAND.${VERSION}_win-x64.zip"
echo "Uploading $FILE_WIN to $VERSION"
RESP_WIN_FILE=$(curl --data-binary @"$FOLDER/$FILE_WIN" -sH "$AUTH" -H "$ACCEPT" -H "$CONTENT_TYPE_WIN" "$GH_UPLOAD?name=$FILE_WIN")

CONTENT_TYPE_LINUX="Content-Type: application/gzip"
FILE_LINUX="$COMMAND.${VERSION}_linux-x64.tar.gz"
echo "Uploading $FILE_LINUX to $VERSION"
RESP_LINUX_FILE=$(curl --data-binary @"$FOLDER/$FILE_LINUX" -sH "$AUTH" -H "$ACCEPT" -H "$CONTENT_TYPE_LINUX" "$GH_UPLOAD?name=$FILE_LINUX")

