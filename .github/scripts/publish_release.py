#!/usr/bin/env python3
"""Validate and publish immutable package releases; only draft assets may be replaced."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile


class ReleaseError(Exception):
    pass


def gh(*arguments):
    return subprocess.run(["gh", *arguments], capture_output=True, text=True, check=False)


def require_success(result, operation):
    if result.returncode:
        # Do not echo subprocess output: authentication diagnostics may contain credentials.
        status = re.search(r"\bHTTP (\d{3})\b", result.stderr or "")
        detail = " (HTTP " + status.group(1) + ")" if status else ""
        raise ReleaseError(operation + " failed" + detail + "; existing releases were not published by this step.")


def get_release(repository, version):
    result = gh("api", "--include", f"repos/{repository}/releases/tags/{version}")
    headers = list(re.finditer(r"(?m)^HTTP/\S+\s+(\d{3})[^\r\n]*\r?$", result.stdout))
    status = int(headers[-1].group(1)) if headers else None
    if status == 404:
        # GitHub may omit drafts from the tag endpoint. Find authenticated drafts before
        # deciding the release is missing; pagination also covers older draft retries.
        listed = gh("api", "--paginate", f"repos/{repository}/releases?per_page=100")
        require_success(listed, "Listing releases")
        try:
            matches = []
            decoder = json.JSONDecoder()
            remaining = listed.stdout.lstrip()
            if not remaining:
                raise ValueError("Missing listing response")
            while remaining:
                page, end = decoder.raw_decode(remaining)
                if not isinstance(page, list):
                    raise ValueError("Release page must be an array")
                matches.extend(release for release in page if release.get("tag_name") == version)
                remaining = remaining[end:].lstrip()
        except (ValueError, TypeError, AttributeError):
            raise ReleaseError("GitHub returned an invalid release listing.")
        if len(matches) > 1:
            raise ReleaseError("Multiple releases use this tag; refusing to choose one to modify.")
        release = matches[0] if matches else None
    else:
        require_success(result, "Reading release")
        if status != 200:
            raise ReleaseError("GitHub did not return an explicit HTTP 200 or 404 release response.")
        try:
            body = re.split(r"\r?\n\r?\n", result.stdout[headers[-1].start():], maxsplit=1)[1]
            release = json.loads(body)
        except (ValueError, IndexError):
            raise ReleaseError("GitHub returned invalid release metadata.")
    if release is not None:
        if (not isinstance(release, dict) or release.get("tag_name") != version
                or not isinstance(release.get("draft"), bool) or not isinstance(release.get("id"), int)):
            raise ReleaseError("GitHub returned inconsistent release identity or draft state.")
    return release


def asset_paths(args):
    directory = Path(args.artifact_dir)
    return {
        f"{args.package_name}-{args.version}.zip": directory / f"{args.package_name}-{args.version}.zip",
        f"{args.core_name}-{args.core_version}.zip": directory / f"{args.core_name}-{args.core_version}.zip",
        f"{args.package_name}-{args.version}.unitypackage": directory / f"{args.package_name}-{args.version}.unitypackage",
        "package.json": Path("Packages") / args.package_name / "package.json",
    }


def file_digest(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return "sha256:" + digest.hexdigest()


def validate_remote(release, paths, compare_local=False):
    assets = release.get("assets")
    if not isinstance(assets, list):
        raise ReleaseError("Release assets are missing from the GitHub response.")
    for name, path in paths.items():
        matches = [asset for asset in assets if asset.get("name") == name]
        if (len(matches) != 1 or not isinstance(matches[0].get("size"), int)
                or matches[0]["size"] <= 0 or matches[0].get("state", "uploaded") != "uploaded"):
            raise ReleaseError(f"Release asset {name} must exist exactly once and contain uploaded bytes.")
        asset = matches[0]
        if compare_local:
            if asset["size"] != path.stat().st_size:
                raise ReleaseError(f"Uploaded size does not match local artifact: {name}.")
            if asset.get("digest") and asset["digest"] != file_digest(path):
                raise ReleaseError(f"Uploaded digest does not match local artifact: {name}.")


def zip_manifest(path):
    try:
        with zipfile.ZipFile(path) as archive:
            if archive.namelist().count("package.json") != 1:
                raise ReleaseError(f"{path.name} must contain exactly one root package.json.")
            manifest = json.loads(archive.read("package.json"))
    except (OSError, ValueError, zipfile.BadZipFile, KeyError):
        raise ReleaseError(f"Cannot read the package manifest in {path.name}.")
    if not isinstance(manifest, dict):
        raise ReleaseError(f"The manifest in {path.name} must be a JSON object.")
    return manifest


def validate_local(args, paths):
    for name, path in paths.items():
        if not path.is_file() or path.stat().st_size <= 0:
            raise ReleaseError(f"Required artifact is missing or empty: {name} ({path}).")
    package = zip_manifest(paths[f"{args.package_name}-{args.version}.zip"])
    core = zip_manifest(paths[f"{args.core_name}-{args.core_version}.zip"])
    for manifest, name, version in ((package, args.package_name, args.version), (core, args.core_name, args.core_version)):
        if manifest.get("name") != name or manifest.get("version") != version:
            raise ReleaseError(f"ZIP manifest name/version mismatch for {name}.")
    for key in ("dependencies", "vpmDependencies"):
        dependencies = package.get(key)
        if not isinstance(dependencies, dict) or dependencies.get(args.core_name) != args.core_version:
            raise ReleaseError(f"Package {key} must pin {args.core_name} to {args.core_version}.")
    try:
        attachment = json.loads(paths["package.json"].read_text(encoding="utf-8"))
    except (OSError, ValueError):
        raise ReleaseError("Cannot read the package.json attachment.")
    if attachment != package:
        raise ReleaseError("The package.json attachment differs from the packaged manifest.")


def output(args, release, published):
    url = release.get("html_url", "") if release else ""
    values = {"published": str(published).lower(), "version": args.version, "url": url}
    if any(not isinstance(value, str) or "\n" in value or "\r" in value for value in values.values()):
        raise ReleaseError("Invalid release output metadata.")
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as destination:
            destination.writelines(f"{name}={value}\n" for name, value in values.items())
    state = "published" if published else "not published"
    print(f"Release {args.version}: {state}" + (f" — {url}" if url else ""))
    return 0


def execute(args, repository):
    paths = asset_paths(args)
    release = get_release(repository, args.version)
    if release is not None and not release["draft"]:
        validate_remote(release, paths)
        return output(args, release, True)
    if args.command == "status":
        return output(args, release, False)

    validate_local(args, paths)
    if release is None:
        result = gh("release", "create", args.version, "--repo", repository, "--verify-tag", "--draft",
                    "--generate-notes", "--title", f"{args.package_name} {args.version}")
        require_success(result, "Creating draft release")
        release = get_release(repository, args.version)
        if release is None:
            raise ReleaseError("Created draft could not be found; retry after GitHub metadata becomes available.")
    release_id = release["id"]
    for path in paths.values():
        # Recheck before EACH upload: never intentionally clobber public assets, including
        # when a previous failed run's draft was published by someone else in the meantime.
        current = get_release(repository, args.version)
        if current is None or current["id"] != release_id:
            raise ReleaseError("Release identity changed during publishing; refusing to upload.")
        if not current["draft"]:
            validate_remote(current, paths)
            return output(args, current, True)
        require_success(gh("release", "upload", args.version, str(path), "--repo", repository, "--clobber"),
                        "Uploading draft artifact")
    current = get_release(repository, args.version)
    if current is None or current["id"] != release_id:
        raise ReleaseError("Release identity changed after uploading; draft was not published.")
    validate_remote(current, paths, compare_local=True)
    if current["draft"]:
        require_success(gh("release", "edit", args.version, "--repo", repository, "--draft=false", "--latest"),
                        "Publishing verified draft")
        current = get_release(repository, args.version)
        if current is None or current["id"] != release_id or current["draft"]:
            raise ReleaseError("GitHub did not confirm publication; inspect the release before retrying.")
        validate_remote(current, paths, compare_local=True)
    return output(args, current, True)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("status", "publish"))
    for flag in ("version", "package-name", "core-name", "core-version"):
        parser.add_argument("--" + flag, required=True)
    parser.add_argument("--artifact-dir", default=".")
    args = parser.parse_args(argv)
    try:
        stable = r"(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)"
        if any(not re.fullmatch(stable, version) for version in (args.version, args.core_version)):
            raise ReleaseError("Package versions must be stable major.minor.patch values.")
        if any(not re.fullmatch(r"[a-z0-9][a-z0-9._-]*", name) for name in (args.package_name, args.core_name)):
            raise ReleaseError("Package names must be lowercase package identifiers.")
        if args.package_name == args.core_name:
            raise ReleaseError("The package and core names must differ.")
        repository = os.environ.get("GITHUB_REPOSITORY", "")
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
            raise ReleaseError("GITHUB_REPOSITORY must identify owner/repository.")
        return execute(args, repository)
    except (ReleaseError, OSError) as error:
        print("Release publishing stopped: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
