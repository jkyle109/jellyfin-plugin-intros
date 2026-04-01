#!/bin/bash
# Generates the manifest.json for Jellyfin plugins

if [ "$#" -ne 4 ]; then
    echo "Usage: $0 <TagName> <MD5Checksum> <Repository> <Owner>"
    exit 1
fi

RAW_VERSION="$1"
MD5="$2"
REPO="$3"
OWNER="$4"

# Strip leading 'v' from tag name if present for strict version checking
CLEAN_VERSION="${RAW_VERSION#v}"
TIMESTAMP=$(date -u +'%Y-%m-%dT%H:%M:%SZ')

cat <<EOF > manifest.json
[
  {
    "category": "MoviesAndShows",
    "guid": "B6FB4817-524D-4AD0-A067-8A66260FD432",
    "name": "Force Intros",
    "description": "Select a flashy pre-roll from local storage to run before any video content.",
    "owner": "${OWNER}",
    "overview": "A modified intros plugin that forcibly overrides the client to play intros before binge-watched episodes.",
    "versions": [
      {
        "targetAbi": "10.11.0.0",
        "sourceUrl": "https://github.com/${REPO}/releases/download/${RAW_VERSION}/Jellyfin.Plugin.ForceIntros.zip",
        "checksum": "${MD5}",
        "timestamp": "${TIMESTAMP}",
        "version": "${CLEAN_VERSION}"
      }
    ]
  }
]
EOF

echo "Successfully generated manifest.json"
