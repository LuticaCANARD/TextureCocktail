"""Offline publication state-machine tests. No GitHub requests or mutations are made."""
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SPEC = importlib.util.spec_from_file_location("publish_release", Path(__file__).with_name("publish_release.py"))
publisher = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(publisher)


class FakeGitHub:
    def __init__(self, release=None):
        self.release = copy.deepcopy(release)
        self.calls = []
        self.fail_upload = False
        self.bad_digest = False
        self.draft_requires_listing = False
        self.publish_before_upload = False
        self.readback_delays = {}
        self.stale_reads = 0
        self.stale_release = None
        self.listing_release = None

    def __call__(self, *args):
        self.calls.append(args)
        if args[0] == "api":
            if "--paginate" in args:
                return self.result(json.dumps([self.listing_release] if self.listing_release else []))
            if self.publish_before_upload and self.release and len(self.calls) > 1:
                self.release["draft"] = False
            visible = self.release
            if self.stale_reads:
                self.stale_reads -= 1
                visible = self.stale_release
            self.listing_release = visible
            if visible is None or (visible["draft"] and self.draft_requires_listing):
                return self.result('HTTP/2.0 404 Not Found\ncontent-type: application/json\n\n{"message":"Not Found"}', 1, "gh: Not Found (HTTP 404)")
            return self.result("HTTP/2.0 200 OK\ncontent-type: application/json\n\n" + json.dumps(visible))
        self.stale_release = copy.deepcopy(self.release)
        self.stale_reads = self.readback_delays.get(args[1], 0)
        if args[:2] == ("release", "create"):
            self.release = make_release(True)
            return self.result(self.release["html_url"])
        if args[:2] == ("release", "upload"):
            if self.fail_upload:
                return self.result("", 1, "connection interrupted")
            assert self.release["draft"], "Attempted to upload to a published release"
            path = Path(args[3])
            self.release["assets"] = [asset for asset in self.release["assets"] if asset["name"] != path.name]
            self.release["assets"].append({"name": path.name, "size": path.stat().st_size, "state": "uploaded",
                "digest": "sha256:bad" if self.bad_digest else publisher.file_digest(path)})
            return self.result("")
        if args[:2] == ("release", "edit"):
            self.release["draft"] = False
            return self.result(self.release["html_url"])
        raise AssertionError("Unexpected gh command: " + repr(args))

    @staticmethod
    def result(stdout, code=0, stderr=""):
        return subprocess.CompletedProcess(["gh"], code, stdout, stderr)

    def mutations(self):
        return [call for call in self.calls if call[0] == "release"]


def make_release(draft=False):
    return {"id": 100, "tag_name": "0.0.10", "draft": draft, "assets": [],
        "html_url": "https://github.com/example/repository/releases/tag/0.0.10"}


class PublisherTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.previous = Path.cwd()
        os.chdir(self.temp.name)
        self.environment = patch.dict(os.environ, {"GITHUB_REPOSITORY": "example/repository", "GITHUB_OUTPUT": str(Path("outputs").resolve())})
        self.environment.start()
        self.arguments = ["--version", "0.0.10", "--package-name", "luticalab.texturecocktail", "--core-name", "luticalab.core", "--core-version", "0.0.3"]
        self.names = ["luticalab.texturecocktail-0.0.10.zip", "luticalab.core-0.0.3.zip",
            "luticalab.texturecocktail-0.0.10.unitypackage", "package.json"]
        self.manifest = {"name": "luticalab.texturecocktail", "version": "0.0.10",
            "dependencies": {"luticalab.core": "0.0.3"}, "vpmDependencies": {"luticalab.core": "0.0.3"}}
        self.errors = io.StringIO()

    def tearDown(self):
        self.environment.stop()
        os.chdir(self.previous)
        self.temp.cleanup()

    def run_helper(self, fake, command="publish", extra=()):
        with patch.object(publisher.time, "sleep") as sleeps:
            self.sleeps = sleeps
            with patch.object(publisher, "gh", fake), patch("sys.stdout", new=io.StringIO()), patch("sys.stderr", self.errors):
                return publisher.main([command, *self.arguments, *extra])

    def fixtures(self, directory=Path(".")):
        directory.mkdir(parents=True, exist_ok=True)
        self.write_zip(directory / self.names[0], self.manifest)
        self.write_zip(directory / self.names[1], {"name": "luticalab.core", "version": "0.0.3"})
        (directory / self.names[2]).write_bytes(b"native unity package fixture")
        attachment = Path("Packages/luticalab.texturecocktail/package.json")
        attachment.parent.mkdir(parents=True, exist_ok=True)
        attachment.write_text(json.dumps(self.manifest))

    @staticmethod
    def write_zip(path, manifest, root="package.json"):
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr(root, json.dumps(manifest))

    def complete_public(self):
        release = make_release()
        release["assets"] = [{"name": name, "size": 42, "state": "uploaded"} for name in self.names]
        return release

    def test_status_missing_does_not_create_or_read_artifacts(self):
        fake = FakeGitHub()
        self.assertEqual(0, self.run_helper(fake, "status"))
        self.assertEqual([], fake.mutations())
        self.assertIn("published=false", Path("outputs").read_text())

    def test_status_draft_found_in_paginated_listing(self):
        fake = FakeGitHub(make_release(True))
        fake.draft_requires_listing = True
        self.assertEqual(0, self.run_helper(fake, "status"))
        self.assertEqual([], fake.mutations())
        self.assertIn("published=false", Path("outputs").read_text())

    def test_draft_lookup_accepts_multiple_standard_json_pages(self):
        draft = make_release(True)
        unrelated = make_release()
        unrelated["tag_name"] = "0.0.9"
        calls = []
        def paginated(*args):
            calls.append(args)
            if "--include" in args:
                return FakeGitHub.result("HTTP/2.0 404 Not Found\n\n{}", 1)
            return FakeGitHub.result(json.dumps([unrelated]) + "\n" + json.dumps([draft]))
        self.assertEqual(0, self.run_helper(paginated, "status"))
        self.assertEqual(2, len(calls))
        self.assertIn("published=false", Path("outputs").read_text())

    def test_missing_release_is_created_only_after_validation(self):
        fake = FakeGitHub()
        self.assertEqual(1, self.run_helper(fake))
        self.assertEqual([], fake.mutations())
        self.fixtures()
        self.assertEqual(0, self.run_helper(fake))
        create = next(call for call in fake.mutations() if call[1] == "create")
        self.assertIn("--verify-tag", create)
        self.assertIn("--draft", create)
        self.assertFalse(fake.release["draft"])
        self.assertEqual(set(self.names), {asset["name"] for asset in fake.release["assets"]})
        self.assertIn("published=true", Path("outputs").read_text())

    def test_failed_upload_leaves_draft_and_retry_reuses_it(self):
        self.fixtures()
        fake = FakeGitHub()
        fake.fail_upload = True
        self.assertEqual(1, self.run_helper(fake))
        self.assertTrue(fake.release["draft"])
        self.assertFalse(any(call[1] == "edit" for call in fake.mutations()))
        fake.fail_upload = False
        fake.draft_requires_listing = True
        self.assertEqual(0, self.run_helper(fake))
        self.assertEqual(1, sum(call[1] == "create" for call in fake.mutations()))
        self.assertFalse(fake.release["draft"])

    def test_published_release_is_read_only_without_local_files(self):
        for command in ("status", "publish"):
            with self.subTest(command=command):
                fake = FakeGitHub(self.complete_public())
                self.assertEqual(0, self.run_helper(fake, command))
                self.assertEqual([], fake.mutations())

    def test_incomplete_published_release_fails_without_mutation(self):
        release = self.complete_public()
        release["assets"].pop()
        fake = FakeGitHub(release)
        self.assertEqual(1, self.run_helper(fake, "status"))
        self.assertEqual([], fake.mutations())
        self.assertFalse(Path("outputs").exists())

    def test_empty_or_duplicate_public_assets_fail(self):
        for case in ("empty", "duplicate"):
            with self.subTest(case=case):
                release = self.complete_public()
                if case == "empty": release["assets"][0]["size"] = 0
                else: release["assets"].append(release["assets"][0].copy())
                fake = FakeGitHub(release)
                self.assertEqual(1, self.run_helper(fake))
                self.assertEqual([], fake.mutations())

    def test_auth_network_and_server_failures_are_not_missing(self):
        for status in (401, 403, 500, None):
            with self.subTest(status=status):
                calls = []
                def failing(*args):
                    calls.append(args)
                    header = f"HTTP/2.0 {status} Error\n\n{{}}" if status else ""
                    return FakeGitHub.result(header, 1, "request failed")
                self.assertEqual(1, self.run_helper(failing, "status"))
                self.assertEqual(1, len(calls))

    def test_listing_failure_after_404_is_not_missing(self):
        calls = []
        def failing(*args):
            calls.append(args)
            if "--include" in args: return FakeGitHub.result("HTTP/2.0 404 Not Found\n\n{}", 1)
            return FakeGitHub.result("", 1, "authentication unavailable")
        self.assertEqual(1, self.run_helper(failing, "status"))
        self.assertEqual(2, len(calls))

    def test_manifest_mismatches_stop_before_mutations(self):
        for case in ("package_version", "core_version", "dependencies", "vpmDependencies", "attachment", "nested"):
            with self.subTest(case=case):
                self.fixtures()
                manifest = copy.deepcopy(self.manifest)
                if case == "package_version":
                    manifest["version"] = "0.0.9"
                    self.write_zip(Path(self.names[0]), manifest)
                elif case == "core_version":
                    self.write_zip(Path(self.names[1]), {"name": "luticalab.core", "version": "0.0.2"})
                elif case in ("dependencies", "vpmDependencies"):
                    manifest[case]["luticalab.core"] = "0.0.2"
                    self.write_zip(Path(self.names[0]), manifest)
                elif case == "attachment":
                    Path("Packages/luticalab.texturecocktail/package.json").write_text("{}")
                else:
                    self.write_zip(Path(self.names[0]), manifest, "nested/package.json")
                fake = FakeGitHub()
                self.assertEqual(1, self.run_helper(fake))
                self.assertEqual([], fake.mutations())

    def test_asset_digest_mismatch_keeps_draft(self):
        self.fixtures()
        fake = FakeGitHub(make_release(True))
        fake.bad_digest = True
        self.assertEqual(1, self.run_helper(fake))
        self.assertEqual([1, 2, 4], [call.args[0] for call in self.sleeps.call_args_list])
        self.assertTrue(fake.release["draft"])
        self.assertEqual(["upload"], [call[1] for call in fake.mutations()])

    def test_rechecks_draft_before_upload(self):
        self.fixtures()
        release = self.complete_public()
        release["draft"] = True
        fake = FakeGitHub(release)
        fake.publish_before_upload = True
        self.assertEqual(0, self.run_helper(fake))
        self.assertEqual([], fake.mutations())

    def test_created_draft_visibility_retries_reads_not_create(self):
        self.fixtures()
        fake = FakeGitHub()
        fake.readback_delays = {"create": 3}
        self.assertEqual(0, self.run_helper(fake))
        self.assertEqual([1, 2, 4], [call.args[0] for call in self.sleeps.call_args_list])
        self.assertEqual(["create", "upload", "upload", "upload", "upload", "edit"],
                         [call[1] for call in fake.mutations()])
        self.assertFalse(fake.release["draft"])

    def test_uploaded_asset_visibility_retries_reads_not_uploads(self):
        self.fixtures()
        fake = FakeGitHub(make_release(True))
        fake.readback_delays = {"upload": 2}
        self.assertEqual(0, self.run_helper(fake))
        self.assertEqual([1, 2] * 4, [call.args[0] for call in self.sleeps.call_args_list])
        self.assertEqual(["upload"] * 4 + ["edit"], [call[1] for call in fake.mutations()])
        self.assertEqual(set(self.names), {asset["name"] for asset in fake.release["assets"]})

    def test_published_state_visibility_retries_reads_not_publish(self):
        self.fixtures()
        fake = FakeGitHub(make_release(True))
        fake.readback_delays = {"edit": 2}
        self.assertEqual(0, self.run_helper(fake))
        self.assertEqual([1, 2], [call.args[0] for call in self.sleeps.call_args_list])
        self.assertEqual(["upload"] * 4 + ["edit"], [call[1] for call in fake.mutations()])
        self.assertIn("published=true", Path("outputs").read_text())

    def test_public_readback_stops_without_another_state_read_or_write(self):
        self.fixtures()
        for operation in ("create", "upload"):
            with self.subTest(operation=operation):
                release = self.complete_public()
                release["draft"] = True
                fake = FakeGitHub(None if operation == "create" else release)
                public_reads = []
                def published_by_someone_else(*args):
                    if args[0] == "api" and fake.release and not fake.release["draft"]:
                        public_reads.append(args)
                        self.assertEqual(1, len(public_reads), "A public release must not be reconsidered as a draft")
                    result = fake(*args)
                    if args[:2] == ("release", operation):
                        if operation == "create":
                            fake.release["assets"] = self.complete_public()["assets"]
                        fake.release["draft"] = False
                    return result
                self.assertEqual(0, self.run_helper(published_by_someone_else))
                self.assertEqual([operation], [call[1] for call in fake.mutations()])
                self.assertEqual(1, len(public_reads))
                self.sleeps.assert_not_called()

    def test_persistent_readback_delay_is_bounded_without_repeated_writes(self):
        self.fixtures()
        for operation in ("create", "upload", "edit"):
            with self.subTest(operation=operation):
                fake = FakeGitHub()
                fake.readback_delays = {operation: 4}
                self.assertEqual(1, self.run_helper(fake))
                self.assertEqual([1, 2, 4], [call.args[0] for call in self.sleeps.call_args_list])
                mutations = [call[1] for call in fake.mutations()]
                self.assertEqual(1, mutations.count("create"))
                self.assertEqual({"create": 0, "upload": 1, "edit": 4}[operation], mutations.count("upload"))
                self.assertEqual(int(operation == "edit"), mutations.count("edit"))
                self.assertEqual(operation != "edit", fake.release["draft"])
                self.assertFalse(Path("outputs").exists())
        self.assertIn("after four metadata reads", self.errors.getvalue())

    def test_post_write_api_errors_fail_immediately_without_retrying_writes(self):
        self.fixtures()
        for status in (401, 403, 500, None):
            with self.subTest(status=status):
                fake = FakeGitHub()
                failures = []
                def failing(*args):
                    if args[0] == "api" and fake.release is not None:
                        failures.append(args)
                        header = f"HTTP/2.0 {status} Error\n\n{{}}" if status else ""
                        return FakeGitHub.result(header, 1, "request failed")
                    return fake(*args)
                self.assertEqual(1, self.run_helper(failing))
                self.assertEqual(1, len(failures))
                self.sleeps.assert_not_called()
                self.assertEqual(["create"], [call[1] for call in fake.mutations()])
                self.assertTrue(fake.release["draft"])

    def test_changed_release_identity_fails_without_retrying_or_further_writes(self):
        self.fixtures()
        fake = FakeGitHub(make_release(True))
        def changed(*args):
            if args[0] == "api" and fake.mutations():
                fake.release["id"] = 101
            return fake(*args)
        self.assertEqual(1, self.run_helper(changed))
        self.sleeps.assert_not_called()
        self.assertEqual(["upload"], [call[1] for call in fake.mutations()])
        self.assertTrue(fake.release["draft"])

    def test_artifact_directory_and_repository_validation(self):
        self.fixtures(Path("artifacts"))
        fake = FakeGitHub()
        self.assertEqual(0, self.run_helper(fake, extra=("--artifact-dir", "artifacts")))
        with patch.dict(os.environ, {"GITHUB_REPOSITORY": ""}):
            fake = FakeGitHub()
            self.assertEqual(1, self.run_helper(fake, "status"))
            self.assertEqual([], fake.calls)


if __name__ == "__main__":
    unittest.main()
