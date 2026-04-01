#!/bin/bash
# Injects a new release version into the permanent manifest.json and pushes it to master.

if [ "$#" -ne 4 ]; then
    echo "Usage: $0 <TagName> <MD5Checksum> <Repository> <GithubToken>"
    exit 1
fi

RAW_VERSION="$1"
MD5="$2"
REPO="$3"
GITHUB_TOKEN="$4"

# Strip leading 'v' from tag name
CLEAN_VERSION="${RAW_VERSION#v}"
TIMESTAMP=$(date -u +'%Y-%m-%dT%H:%M:%SZ')

echo "Injecting ${CLEAN_VERSION} into manifest.json"

# Generate new nested JSON object
NEW_VERSION_JSON=$(jq -n \
  --arg abi "10.11.0.0" \
  --arg url "https://github.com/${REPO}/releases/download/${RAW_VERSION}/Jellyfin.Plugin.ForceIntros.zip" \
  --arg checksum "${MD5}" \
  --arg timestamp "${TIMESTAMP}" \
  --arg version "${CLEAN_VERSION}" \
  '{
    targetAbi: $abi,
    sourceUrl: $url,
    checksum: $checksum,
    timestamp: $timestamp,
    version: $version
  }'
)

# Prepend our new version object to exactly index 0 of the versions array
jq --argjson new_ver "$NEW_VERSION_JSON" '.[0].versions = [$new_ver] + .[0].versions' manifest.json > tmp_manifest.json
mv tmp_manifest.json manifest.json

echo "Committing manifest.json to Master"
git config --global user.name "github-actions[bot]"
git config --global user.email "github-actions[bot]@users.noreply.github.com"
git add manifest.json
git commit -m "chore: inject ${RAW_VERSION} into manifest.json [skip ci]" || echo "No changes to commit"

echo "Pushing manifest.json upstream"
git push "https://x-access-token:${GITHUB_TOKEN}@github.com/${REPO}.git" HEAD:master || true

echo "Successfully updated and pushed manifest.json"
