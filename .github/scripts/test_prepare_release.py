#!/usr/bin/env python3
"""Integration checks using disposable repositories and a local bare origin."""

import json
import os
from pathlib import Path
import shlex
import subprocess
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("prepare_release.py").resolve()
PACKAGE = "luticalab.texturecocktail"
CORE = "luticalab.core"


class PrepareReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="texturecocktail-release-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repo = self.root / "work"
        self.origin = self.root / "origin.git"
        self.repo.mkdir()
        self.git("init", "--bare", str(self.origin))
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Release Test")
        self.git("config", "user.email", "release-test@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.git("config", "tag.gpgsign", "false")
        self.git("remote", "add", "origin", str(self.origin))
        self.package_path = self.repo / "Packages" / PACKAGE / "package.json"
        self.core_path = self.repo / "Packages" / CORE / "package.json"
        self.lock_path = self.repo / "Packages" / "packages-lock.json"
        self.write_json(self.package_path, {"name": PACKAGE, "version": "1.2.3",
            "dependencies": {CORE: "0.4.5", "other.package": "7.8.9"},
            "vpmDependencies": {CORE: "0.4.5"}, "description": "keep me"})
        self.write_json(self.core_path, {"name": CORE, "version": "0.4.5"})
        self.write_json(self.lock_path, {"dependencies": {
            PACKAGE: {"version": "file:" + PACKAGE, "source": "embedded",
                      "dependencies": {CORE: "0.4.5", "other.package": "7.8.9"}},
            CORE: {"version": "file:" + CORE, "source": "embedded", "dependencies": {}},
            "other.package": {"version": "7.8.9", "source": "registry"}}})
        self.core_source = self.core_path.with_name("Core.cs")
        self.core_source.write_text("// original core\n", encoding="utf-8")
        self.commit("Initial source")
        self.git("push", "-u", "origin", "main")

    def git(self, *args, check=True, input=None):
        result = subprocess.run(["git", *args], cwd=self.repo, text=True,
                                capture_output=True, input=input)
        if check and result.returncode:
            self.fail(f"git {args}: {result.stderr}")
        return result.stdout.strip() if check else result

    def write_json(self, path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

    def update_json(self, path, **updates):
        data = json.loads(path.read_text(encoding="utf-8"))
        data.update(updates)
        self.write_json(path, data)

    def commit(self, message):
        self.git("add", "--all")
        self.git("commit", "-m", message)
        return self.git("rev-parse", "HEAD")

    def release_tag(self, tag="1.2.3"):
        self.git("tag", "-a", tag, "-m", "Existing release")
        self.git("push", "origin", "main", "--tags")

    def invoke(self, *extra, run_id="100", source=None, success=True, env=None):
        command = [sys.executable, str(SCRIPT), "--package-name", PACKAGE,
                   "--core-name", CORE, "--run-id", run_id, "--source-sha",
                   source or self.git("rev-parse", "HEAD"), *extra]
        environment = dict(os.environ)
        environment.pop("GITHUB_OUTPUT", None)
        if env:
            environment.update(env)
        result = subprocess.run(command, cwd=self.repo, text=True, capture_output=True,
                                env=environment)
        if success:
            self.assertEqual(result.returncode, 0, result.stderr)
            return json.loads(result.stdout)
        self.assertNotEqual(result.returncode, 0, result.stdout)
        return result.stderr

    def state(self):
        return (self.git("rev-parse", "HEAD"), self.git("show-ref"),
                self.git("status", "--porcelain", "--untracked-files=all"),
                {str(path.relative_to(self.repo)): path.read_bytes()
                 for path in (self.package_path, self.core_path, self.lock_path)})

    def assert_pins(self, expected):
        package = json.loads(self.package_path.read_text())
        self.assertEqual(package["dependencies"][CORE], expected)
        self.assertEqual(package["vpmDependencies"][CORE], expected)
        lock = json.loads(self.lock_path.read_text())["dependencies"]
        self.assertEqual(lock[PACKAGE]["dependencies"][CORE], expected)
        self.assertEqual(lock[CORE]["version"], "file:" + CORE)
        self.assertEqual(lock["other.package"]["version"], "7.8.9")

    def test_initial_patch_preserves_declared_version_and_ignores_legacy_tag(self):
        self.git("tag", "v9.9.9")
        result = self.invoke()
        self.assertEqual(result["version"], "1.2.3")
        self.assertEqual(result["core_version"], "0.4.5")
        self.assertEqual(result["changed_paths"], [])
        self.assertEqual(self.git("cat-file", "-t", "refs/tags/1.2.3"), "tag")
        self.assertIn("TextureCocktail-Release-Run: 100", self.git("cat-file", "-p", "1.2.3"))

    def test_patch_minor_major_from_latest_release(self):
        self.release_tag()
        before = self.state()
        for kind, expected in (("patch", "1.2.4"), ("minor", "1.3.0"), ("major", "2.0.0")):
            with self.subTest(kind=kind):
                result = self.invoke("--bump", kind, "--dry-run")
                self.assertEqual(result["version"], expected)
                self.assertEqual(self.state(), before)
        result = self.invoke("--bump", "minor")
        self.assertEqual(result["version"], "1.3.0")
        self.assertEqual(self.git("ls-remote", "origin", "refs/heads/main").split()[0], result["commit"])
        self.assertEqual(self.git("diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD"),
                         f"Packages/{PACKAGE}/package.json")
        self.assert_pins("0.4.5")

    def test_initial_minor_major_use_declared_as_baseline(self):
        self.assertEqual(self.invoke("--bump", "minor", "--dry-run")["version"], "1.3.0")
        self.assertEqual(self.invoke("--bump", "major", "--dry-run")["version"], "2.0.0")

    def test_manual_package_and_core_versions_are_respected(self):
        self.release_tag()
        self.update_json(self.package_path, version="3.0.0")
        self.update_json(self.core_path, version="1.0.0")
        self.commit("Manually prepare versions")
        result = self.invoke("--bump", "minor")
        self.assertEqual((result["version"], result["core_version"]), ("3.0.0", "1.0.0"))
        self.assert_pins("1.0.0")

    def test_changed_core_gets_patch_and_only_exact_json_values_change(self):
        self.release_tag()
        self.core_source.write_text("// changed core\n", encoding="utf-8")
        self.commit("Change core implementation")
        originals = {path: path.read_bytes() for path in (self.package_path, self.core_path, self.lock_path)}
        result = self.invoke()
        self.assertEqual(result["core_version"], "0.4.6")
        for path, original in originals.items():
            expected = original.replace(b'"0.4.5"', b'"0.4.6"')
            if path == self.package_path:
                expected = expected.replace(b'"1.2.3"', b'"1.2.4"')
            self.assertEqual(path.read_bytes(), expected)
        self.assert_pins("0.4.6")

    def test_unchanged_core_keeps_version_and_existing_pins_are_repaired(self):
        self.release_tag()
        package = json.loads(self.package_path.read_text())
        package["dependencies"][CORE] = "0.0.1"
        package["vpmDependencies"][CORE] = "0.0.2"
        self.write_json(self.package_path, package)
        self.commit("Stale pins")
        result = self.invoke()
        self.assertEqual(result["core_version"], "0.4.5")
        self.assert_pins("0.4.5")

    def test_missing_dependency_objects_are_added_without_requiring_a_lock(self):
        package = json.loads(self.package_path.read_text())
        del package["dependencies"], package["vpmDependencies"]
        self.write_json(self.package_path, package)
        self.lock_path.unlink()
        self.commit("Package without generated lock or pins")
        self.invoke()
        package = json.loads(self.package_path.read_text())
        self.assertEqual(package["dependencies"][CORE], "0.4.5")
        self.assertEqual(package["vpmDependencies"][CORE], "0.4.5")
        self.assertFalse(self.lock_path.exists())

    def test_same_run_recovers_original_tag_after_main_advances_then_fresh_run_increments(self):
        self.release_tag()
        original_source = self.git("rev-parse", "HEAD")
        first = self.invoke()
        self.core_source.write_text("// next release changes\n", encoding="utf-8")
        main_tip = self.commit("Later main work")
        self.git("push", "origin", "main")
        output = self.root / "github-output"
        recovered = self.invoke(source=original_source, env={"GITHUB_OUTPUT": str(output)})
        self.assertEqual((recovered["version"], recovered["core_version"], recovered["commit"]),
                         (first["version"], first["core_version"], first["commit"]))
        self.assertTrue(recovered["reused"])
        self.assertEqual(self.git("rev-parse", "HEAD"), first["commit"])
        self.assertEqual(self.git("rev-parse", "main"), main_tip)
        self.assertEqual(self.git("symbolic-ref", "--quiet", "HEAD", check=False).returncode, 1)
        self.assertIn("reused=true\n", output.read_text())
        self.assertIn("commit=" + first["commit"] + "\n", output.read_text())
        self.git("checkout", "main")
        fresh = self.invoke(run_id="101")
        self.assertEqual((fresh["version"], fresh["core_version"]), ("1.2.5", "0.4.6"))
        self.assertFalse(fresh["reused"])

    def test_dry_run_recovery_does_not_checkout_or_mutate_refs(self):
        self.invoke()
        self.core_source.write_text("// later\n", encoding="utf-8")
        self.commit("Later main")
        before = self.state()
        self.assertTrue(self.invoke("--dry-run")["reused"])
        self.assertEqual(self.state(), before)

    def test_dispatch_sha_mismatch_rejects_before_mutation(self):
        before = self.state()
        self.assertIn("dispatch source SHA", self.invoke(source="0" * 40, success=False))
        self.assertEqual(self.state(), before)

    def test_package_and_core_rollbacks_are_rejected(self):
        self.release_tag()
        self.update_json(self.package_path, version="1.2.2")
        self.commit("Invalid package rollback")
        before = self.state()
        self.assertIn("rolls back", self.invoke(success=False))
        self.assertEqual(self.state(), before)
        self.update_json(self.package_path, version="1.2.3")
        self.update_json(self.core_path, version="0.4.4")
        self.commit("Invalid core rollback")
        before = self.state()
        self.assertIn("core version rolls back", self.invoke(success=False))
        self.assertEqual(self.state(), before)

    def test_invalid_numeric_tag_is_ignored_but_collision_is_not_overwritten(self):
        self.release_tag()
        self.git("tag", "99.0.0")  # Manifest version does not match this tag.
        self.assertEqual(self.invoke("--dry-run")["version"], "1.2.4")
        self.git("tag", "1.2.4")
        before = self.state()
        self.assertIn("already exists", self.invoke(success=False))
        self.assertEqual(self.state(), before)

    def test_invalid_arguments_and_versions_cannot_mutate_repository(self):
        before = self.state()
        for extra in (("--package-name", "../outside"), ("--core-name", PACKAGE),
                      ("--package-name", "UPPER"), ("--run-id", "12\n34"),
                      ("--source-sha", "main")):
            with self.subTest(extra=extra):
                self.invoke(*extra, success=False)
                self.assertEqual(self.state(), before)
        for bad in ("1.2", "v1.2.3", "1.2.3-beta.1", "01.2.3", 123):
            self.update_json(self.package_path, version=bad)
            self.commit("Invalid manifest version")
            before = self.state()
            self.assertIn("stable X.Y.Z", self.invoke(success=False))
            self.assertEqual(self.state(), before)
        self.update_json(self.package_path, version="1.2.3", name="wrong.package")
        self.commit("Wrong manifest name")
        self.assertIn("Manifest name", self.invoke(success=False))

    def test_dirty_worktree_and_symlinked_inputs_are_rejected(self):
        self.core_source.write_text("// uncommitted\n", encoding="utf-8")
        self.assertIn("clean working tree", self.invoke(success=False))
        self.commit("Commit changes")
        target = self.root / "outside-package.json"
        target.write_bytes(self.package_path.read_bytes())
        self.package_path.unlink()
        self.package_path.symlink_to(target)
        self.commit("Symlinked manifest")
        before = target.read_bytes()
        self.assertIn("Symlinks", self.invoke(success=False))
        self.assertEqual(target.read_bytes(), before)

    def test_atomic_push_rejects_main_race_without_publishing_tag(self):
        self.release_tag()
        original = self.git("rev-parse", "HEAD")
        tree = self.git("rev-parse", "HEAD^{tree}")
        racing_commit = self.git("commit-tree", tree, "-p", original, input="Concurrent main update\n")
        self.git("push", "origin", racing_commit + ":refs/heads/concurrent")
        hook = self.repo / ".git" / "hooks" / "pre-push"
        hook.write_text("#!/bin/sh\ngit --git-dir=" + shlex.quote(str(self.origin)) +
                        " update-ref refs/heads/main " + racing_commit + "\n", encoding="utf-8")
        hook.chmod(0o755)
        self.assertIn("git push --atomic", self.invoke(success=False))
        self.assertEqual(self.git("ls-remote", "origin", "refs/heads/main").split()[0], racing_commit)
        self.assertEqual(self.git("ls-remote", "origin", "refs/tags/1.2.4"), "")
        self.assertIn("not published", self.invoke(success=False))

    def test_atomic_push_rejects_server_without_atomic_support(self):
        self.release_tag()
        original = self.git("rev-parse", "HEAD")
        self.git("--git-dir=" + str(self.origin), "config", "receive.advertiseAtomic", "false")
        self.assertIn("atomic", self.invoke(success=False))
        self.assertEqual(self.git("ls-remote", "origin", "refs/heads/main").split()[0], original)
        self.assertEqual(self.git("ls-remote", "origin", "refs/tags/1.2.4"), "")

    def test_recovery_rejects_tampered_pins_and_duplicate_ownership(self):
        package = json.loads(self.package_path.read_text())
        package["dependencies"][CORE] = "9.9.9"
        self.write_json(self.package_path, package)
        self.commit("Invalid release pins")
        marker = "TextureCocktail-Release-Run: 100\nTextureCocktail-Package: " + PACKAGE
        self.git("tag", "-a", "1.2.3", "-m", marker)
        self.git("push", "origin", "main", "--tags")
        self.assertIn("inconsistent dependencies", self.invoke(success=False))
        self.git("tag", "-a", "1.2.4", "-m", marker)
        self.assertIn("Multiple release tags", self.invoke(success=False))


if __name__ == "__main__":
    unittest.main(verbosity=2)
