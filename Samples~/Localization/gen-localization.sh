#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
config="$script_dir/Localization.ini"
if [[ ! -f "$config" ]]; then
    echo "Localization config not found: $config" >&2
    exit 1
fi

unity_project="$(awk '/^unity_project=/{sub(/\r$/, ""); sub(/^[^=]*=/, ""); print; exit}' "$config")"
if [[ -z "$unity_project" || ! -d "$script_dir/$unity_project/Assets" ]]; then
    echo "unity_project is missing or is not a Unity project: $unity_project" >&2
    exit 1
fi
project_root="$(cd "$script_dir/$unity_project" && pwd)"

package_root="$project_root/Packages/com.alicizax.unity.localization"
if [[ ! -f "$package_root/Editor/Generation/Cli~/Localization.Cli.dll" ]]; then
    package_root=""
    for candidate in "$project_root"/Library/PackageCache/com.alicizax.unity.localization@*; do
        if [[ -f "$candidate/Editor/Generation/Cli~/Localization.Cli.dll" ]]; then
            if [[ -n "$package_root" ]]; then
                echo "Multiple localization packages found in PackageCache." >&2
                exit 1
            fi
            package_root="$candidate"
        fi
    done
fi
if [[ -z "$package_root" ]]; then
    echo "Localization CLI not found in $project_root/Packages or PackageCache." >&2
    exit 1
fi
if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet was not found on PATH." >&2
    exit 1
fi

dotnet "$package_root/Editor/Generation/Cli~/Localization.Cli.dll" generate --config "$config" --project "$project_root" --package "$package_root"
