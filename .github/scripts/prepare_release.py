#!/usr/bin/env python3
"""Prepare an immutable package release; run from a clean repository root."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys


VERSION = re.compile(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\Z")
NAME = re.compile(r"[a-z0-9][a-z0-9._-]{0,213}\Z")
RUN_MARKER = "TextureCocktail-Release-Run: "
PACKAGE_MARKER = "TextureCocktail-Package: "


class ReleaseError(Exception):
    pass


def git(*args, check=True):
    result = subprocess.run(["git", *args], text=True, capture_output=True)
    if check and result.returncode:
        raise ReleaseError(f"git {' '.join(args)} failed: {result.stderr.strip()}")
    return result


def version(value):
    if not isinstance(value, str) or not VERSION.fullmatch(value):
        raise ReleaseError(f"Expected a stable X.Y.Z version, got {value!r}")
    return tuple(map(int, value.split(".")))


def format_version(parts):
    return ".".join(map(str, parts))


def bump(parts, kind):
    major, minor, patch = parts
    return {"major": (major + 1, 0, 0), "minor": (major, minor + 1, 0),
            "patch": (major, minor, patch + 1)}[kind]


def safe_path(relative, required=True):
    path = Path(relative)
    for candidate in (path, *path.parents):
        if candidate.is_symlink():
            raise ReleaseError(f"Symlinks are not allowed for release inputs: {candidate}")
    if required:
        if not path.is_file():
            raise ReleaseError(f"Missing release input: {path}")
        git("ls-files", "--error-unmatch", "--", str(path))
    return path


def manifest(text, expected_name):
    try:
        data = json.loads(text)
    except (ValueError, TypeError) as error:
        raise ReleaseError(f"Invalid manifest for {expected_name}: {error}") from error
    if not isinstance(data, dict) or data.get("name") != expected_name:
        raise ReleaseError(f"Manifest name must be {expected_name!r}")
    version(data.get("version"))
    return data


def tagged_manifest(tag, name):
    return manifest(git("show", f"refs/tags/{tag}:Packages/{name}/package.json").stdout, name)


def value_span(text, keys, start=0):
    """Find an object value without changing unrelated JSON whitespace or order."""
    decoder = json.JSONDecoder()
    start = len(text) - len(text.lstrip()) if start == 0 else start
    if text[start] != "{":
        raise KeyError(keys)
    cursor = start + 1
    while True:
        while text[cursor].isspace() or text[cursor] == ",":
            cursor += 1
        if text[cursor] == "}":
            raise KeyError(keys)
        key, cursor = decoder.raw_decode(text, cursor)
        while text[cursor].isspace() or text[cursor] == ":":
            cursor += 1
        begin = cursor
        _, cursor = decoder.raw_decode(text, cursor)
        if key == keys[0]:
            return (begin, cursor) if len(keys) == 1 else value_span(text, keys[1:], begin)


def updated_json(text, updates):
    data = json.loads(text)
    edits = []
    missing = False
    for keys, value in updates:
        cursor = data
        for key in keys[:-1]:
            cursor = cursor.setdefault(key, {})
            if not isinstance(cursor, dict):
                raise ReleaseError(f"Expected an object at {'.'.join(keys[:-1])}")
        if cursor.get(keys[-1]) == value:
            continue
        cursor[keys[-1]] = value
        try:
            begin, end = value_span(text, keys)
            edits.append((begin, end, json.dumps(value)))
        except KeyError:
            missing = True
    if missing:
        newline = "\r\n" if "\r\n" in text else "\n"
        return json.dumps(data, indent=2, ensure_ascii=False).replace("\n", newline) + (
            newline if text.endswith("\n") else "")
    for begin, end, replacement in sorted(edits, reverse=True):
        text = text[:begin] + replacement + text[end:]
    return text


def validate_pins(package, core_name, core_version):
    for field in ("dependencies", "vpmDependencies"):
        if not isinstance(package.get(field), dict) or package[field].get(core_name) != core_version:
            raise ReleaseError(f"Release tag has an inconsistent {field} pin for {core_name}")


def recover(args, tags):
    owned = []
    for tag in tags:
        ref = f"refs/tags/{tag}"
        if git("cat-file", "-t", ref).stdout.strip() != "tag":
            continue
        body = git("cat-file", "-p", ref).stdout.split("\n\n", 1)[-1].splitlines()
        if RUN_MARKER + args.run_id in body:
            if PACKAGE_MARKER + args.package_name not in body:
                raise ReleaseError("This run ID already belongs to another package")
            owned.append(tag)
    if len(owned) > 1:
        raise ReleaseError("Multiple release tags claim this run ID")
    if not owned:
        return None
    tag = owned[0]
    package = tagged_manifest(tag, args.package_name)
    core = tagged_manifest(tag, args.core_name)
    if package["version"] != tag:
        raise ReleaseError("Owned release tag does not match its package version")
    validate_pins(package, args.core_name, core["version"])
    commit = git("rev-parse", f"refs/tags/{tag}^{{commit}}").stdout.strip()
    if git("merge-base", "--is-ancestor", commit, "refs/heads/main", check=False).returncode:
        raise ReleaseError("Owned release commit is not an ancestor of main")
    remote = git("ls-remote", "--refs", "origin", f"refs/tags/{tag}").stdout.split()
    tag_object = git("rev-parse", f"refs/tags/{tag}").stdout.strip()
    if not remote or remote[0] != tag_object:
        raise ReleaseError("Owned release tag was not published to origin; refusing to claim recovery")
    if not args.dry_run:
        git("checkout", "--detach", commit)
    return {"version": tag, "core_version": core["version"], "commit": commit,
            "reused": True, "dry_run": args.dry_run, "changed_paths": []}


def prepare(args):
    if not NAME.fullmatch(args.package_name) or not NAME.fullmatch(args.core_name):
        raise ReleaseError("Package names must be safe lowercase package directory names")
    if args.package_name == args.core_name:
        raise ReleaseError("Package and core names must differ")
    if not re.fullmatch(r"[0-9]+", args.run_id):
        raise ReleaseError("Run ID must contain only digits")
    if not re.fullmatch(r"(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})", args.source_sha):
        raise ReleaseError("Source SHA must be a full Git commit hash")
    if Path(git("rev-parse", "--show-toplevel").stdout.strip()).resolve() != Path.cwd().resolve():
        raise ReleaseError("Run this script from the repository root")
    if git("status", "--porcelain", "--untracked-files=all").stdout:
        raise ReleaseError("Release preparation requires a clean working tree and index")
    git("rev-parse", "--verify", "refs/heads/main")
    git("remote", "get-url", "origin")
    package_path = safe_path(f"Packages/{args.package_name}/package.json", required=False)
    core_path = safe_path(f"Packages/{args.core_name}/package.json", required=False)
    tags = [tag for tag in git("tag", "--list").stdout.splitlines() if VERSION.fullmatch(tag)]
    recovered = recover(args, tags)
    if recovered:
        return recovered
    head = git("rev-parse", "HEAD").stdout.strip()
    if git("symbolic-ref", "--quiet", "HEAD", check=False).stdout.strip() != "refs/heads/main":
        raise ReleaseError("New releases must be prepared on the main branch")
    if head.lower() != args.source_sha.lower():
        raise ReleaseError("Current main differs from the dispatch source SHA; dispatch a new run")
    safe_path(str(package_path))
    safe_path(str(core_path))
    texts = {path: path.read_bytes().decode("utf-8") for path in (package_path, core_path)}
    package = manifest(texts[package_path], args.package_name)
    core = manifest(texts[core_path], args.core_name)
    declared = version(package["version"])
    declared_core = version(core["version"])
    valid_tags = []
    for tag in tags:
        try:
            tagged = tagged_manifest(tag, args.package_name)
        except ReleaseError:
            continue
        if tagged["version"] == tag:
            valid_tags.append(tag)
    latest = max(valid_tags, key=version) if valid_tags else None
    next_version = declared if args.bump == "patch" else bump(declared, args.bump)
    next_core = declared_core
    if latest:
        if git("merge-base", "--is-ancestor", f"refs/tags/{latest}", "HEAD", check=False).returncode:
            raise ReleaseError("Latest release is not an ancestor of main")
        released = version(latest)
        if declared < released:
            raise ReleaseError("Declared package version rolls back the latest release")
        next_version = max(declared, bump(released, args.bump))
        released_core = version(tagged_manifest(latest, args.core_name)["version"])
        if declared_core < released_core:
            raise ReleaseError("Declared core version rolls back the latest release")
        changed = git("diff", "--quiet", f"refs/tags/{latest}", "HEAD", "--",
                      f"Packages/{args.core_name}", check=False).returncode
        if changed not in (0, 1):
            raise ReleaseError("Could not compare core contents to the latest release")
        if changed and declared_core == released_core:
            next_core = bump(released_core, "patch")
    release_version, core_version = format_version(next_version), format_version(next_core)
    if git("show-ref", "--verify", "--quiet", f"refs/tags/{release_version}", check=False).returncode == 0:
        raise ReleaseError(f"Tag {release_version} already exists; refusing to overwrite it")
    proposed = {
        package_path: updated_json(texts[package_path], [(("version",), release_version),
            (("dependencies", args.core_name), core_version),
            (("vpmDependencies", args.core_name), core_version)]),
        core_path: updated_json(texts[core_path], [(("version",), core_version)]),
    }
    lock_path = safe_path("Packages/packages-lock.json", required=False)
    if lock_path.exists():
        safe_path(str(lock_path))
        texts[lock_path] = lock_path.read_bytes().decode("utf-8")
        lock = json.loads(texts[lock_path])
        if not isinstance(lock, dict) or not isinstance(lock.get("dependencies", {}), dict):
            raise ReleaseError("Invalid packages-lock.json dependencies object")
        entry = lock.get("dependencies", {}).get(args.package_name, {})
        if not isinstance(entry, dict) or not isinstance(entry.get("dependencies", {}), dict):
            raise ReleaseError("Invalid package entry in packages-lock.json")
        if entry.get("source") == "embedded" and args.core_name in entry.get("dependencies", {}):
            proposed[lock_path] = updated_json(texts[lock_path], [
                (("dependencies", args.package_name, "dependencies", args.core_name), core_version)])
    changed_paths = [str(path) for path, text in proposed.items() if text != texts[path]]
    commit = head
    if not args.dry_run:
        for path, text in proposed.items():
            if text != texts[path]:
                path.write_bytes(text.encode("utf-8"))
        message = (f"chore: release {args.package_name} {release_version}\n\n"
                   f"{RUN_MARKER}{args.run_id}\n{PACKAGE_MARKER}{args.package_name}\n"
                   f"TextureCocktail-Source: {head}")
        if changed_paths:
            git("add", "--", *changed_paths)
            git("commit", "-m", message, "--only", "--", *changed_paths)
            commit = git("rev-parse", "HEAD").stdout.strip()
        git("tag", "-a", release_version, "-m", message, commit)
        git("push", "--atomic", "origin", "refs/heads/main:refs/heads/main",
            f"refs/tags/{release_version}:refs/tags/{release_version}")
    return {"version": release_version, "core_version": core_version, "commit": commit,
            "reused": False, "dry_run": args.dry_run, "changed_paths": changed_paths}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-name", required=True)
    parser.add_argument("--core-name", required=True)
    parser.add_argument("--bump", choices=("patch", "minor", "major"), default="patch")
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--source-sha", required=True)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    try:
        result = prepare(args)
        output = os.environ.get("GITHUB_OUTPUT")
        if output:
            with open(output, "a", encoding="utf-8") as stream:
                for key in ("version", "core_version", "commit", "reused"):
                    value = str(result[key]).lower() if isinstance(result[key], bool) else result[key]
                    stream.write(f"{key}={value}\n")
        print(json.dumps(result, sort_keys=True))
    except (ReleaseError, ValueError, OSError) as error:
        print(f"Release preparation failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
