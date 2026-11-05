"""Package freshly built players from a clean release checkout, with source provenance."""
import argparse
import hashlib
import importlib.util
import pathlib
import re
import shutil
import stat
import struct
import subprocess
import sys
import zipfile


ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.dont_write_bytecode = True


def git(*arguments):
    return subprocess.check_output(["git", *arguments], cwd=ROOT, text=True).strip()


def copy(source, target):
    if source.is_dir():
        shutil.copytree(source, target, symlinks=True,
                        ignore=shutil.ignore_patterns("*DoNotShip*"))
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)


def validate(archive_path, platform, sha):
    with zipfile.ZipFile(archive_path) as archive:
        assert archive.testzip() is None, "ZIP CRC failure"
        names = set(archive.namelist())
        assert archive.read("steam_appid.txt").strip() == b"480"
        assert sha in archive.read("BUILD-INFO.txt").decode("utf-8")
        assert "README.md" in names and "docs/VERIFICATION.md" in names
        assert not any("DoNotShip" in name for name in names)
        native = 0
        for info in archive.infolist():
            name = info.filename
            assert not any(word in name.lower() for word in
                           ("learning.uk", "preparation-plan", "network-debugging-original"))
            data = archive.read(info)
            if platform == "macOS":
                if data[:4] == b"\xca\xfe\xba\xbe":
                    count = struct.unpack_from(">I", data, 4)[0]
                    cpus = {struct.unpack_from(">I", data, 8 + index * 20)[0]
                            for index in range(count)}
                    assert cpus == {0x1000007, 0x100000c}, (name, cpus)
                    assert stat.S_IMODE(info.external_attr >> 16) == 0o755, name
                    native += 1
                if name.endswith(".command"):
                    assert b"\r" not in data, name
                    assert stat.S_IMODE(info.external_attr >> 16) == 0o755, name
                if stat.S_ISLNK(info.external_attr >> 16):
                    target = data.decode("utf-8")
                    assert not target.startswith("/"), (name, target)
                    import posixpath
                    resolved = posixpath.normpath(posixpath.join(posixpath.dirname(name), target))
                    assert resolved in names or any(n.startswith(resolved + "/") for n in names), (name, target)
            if name.endswith(".md"):
                for link in re.findall(r"\]\(([^)]+)\)", data.decode("utf-8")):
                    link = link.split("#", 1)[0]
                    if not link or "://" in link or link.startswith("mailto:"):
                        continue
                    import posixpath
                    target = posixpath.normpath(posixpath.join(posixpath.dirname(name), link))
                    assert target in names or any(n.startswith(target.rstrip("/") + "/") for n in names), (name, link)
        if platform == "macOS":
            assert native >= 7, f"Expected Universal Unity and Steam binaries, got {native}"
            assert any(n.endswith("libsteam_api.dylib") for n in names)
            assert any(n.endswith("Facepunch.Steamworks.Posix.dll") for n in names)
            assert not any(n.endswith("steam_api64.dll") for n in names)
        else:
            assert "DockCoop.exe" in names and "Host-Local.cmd" in names
            assert "Client-1-Local.cmd" in names and "Client-2-Local.cmd" in names
            assert any(n.endswith("steam_api64.dll") for n in names)
            assert any(n.endswith("Facepunch.Steamworks.Win64.dll") for n in names)
    print(f"PASS archive structure, CRC, documentation links and provenance: {archive_path.name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source-sha", required=True, help="Full SHA used for both fresh Unity builds")
    parser.add_argument("--video", type=pathlib.Path)
    args = parser.parse_args()
    assert re.fullmatch(r"v\d+\.\d+\.\d+", args.version), "Expected vMAJOR.MINOR.PATCH"
    assert re.fullmatch(r"[0-9a-f]{40}", args.source_sha), "Expected full source SHA"
    assert git("rev-parse", "HEAD") == args.source_sha, "Checkout differs from build source"
    assert not git("status", "--porcelain"), "Release checkout must be clean"
    unity = re.search(r"m_EditorVersion: (.+)", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text())[1].strip()
    output = ROOT / "Artifacts" / args.version
    output.mkdir(parents=True, exist_ok=True)
    spec = importlib.util.spec_from_file_location("package_macos", ROOT / "tools/package-macos.py")
    mac = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mac)
    assets = []
    for platform in ("Windows", "macOS"):
        source = ROOT / "Builds" / platform
        stage = output / f"stage-{platform}"
        # Never erase old staging or local builds: a repeated packaging run needs a new directory.
        stage.mkdir()
        if platform == "Windows":
            for name in ("DockCoop.exe", "DockCoop_Data", "MonoBleedingEdge", "UnityPlayer.dll",
                         "UnityCrashHandler64.exe", "WinPixEventRuntime.dll", "D3D12"):
                if (source / name).exists():
                    copy(source / name, stage / name)
            launchers = ROOT / "tools/launchers"
        else:
            copy(source / "DockCoop.app", stage / "DockCoop.app")
            launchers = ROOT / "tools/launchers-macos"
        for launcher in launchers.iterdir():
            copy(launcher, stage / launcher.name)
        (stage / "steam_appid.txt").write_text("480\n", encoding="ascii")
        copy(ROOT / "README.md", stage / "README.md")
        copy(ROOT / "docs", stage / "docs")
        package_documents = {
            "com.unity.transport": ("LICENSE.md", "UPSTREAM-PATCH.md"),
            "com.community.netcode.transport.facepunch": ("LICENSE.md", "FACEPUNCH-LICENSE.txt", "UPSTREAM.md"),
        }
        for package, documents in package_documents.items():
            for name in documents:
                copy(ROOT / "Packages" / package / name, stage / "Packages" / package / name)
        (stage / "BUILD-INFO.txt").write_text(
            f"Dock Co-op {args.version}\nSource commit: {args.source_sha}\n"
            f"Unity: {unity}\nPlatform: {platform}"
            + (" x64" if platform == "Windows" else " Universal x86_64 + arm64, macOS 12+")
            + "\nTransport: UTP / Steam (AppID 480)\nBuild: Development, Mono\n"
            + "Source: https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission\n",
            encoding="utf-8")
        archive = output / f"DockCoop-{platform}-{args.version}.zip"
        if platform == "macOS":
            mac.package(stage, archive)
        else:
            with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as target:
                for path in sorted(stage.rglob("*")):
                    if path.is_file():
                        target.write(path, path.relative_to(stage).as_posix())
        validate(archive, platform, args.source_sha)
        assets.append(archive)
    if args.video:
        video = output / f"DockCoop-Demo-{args.version}.mp4"
        shutil.copy2(args.video, video)
        assets.append(video)
    sums = []
    for asset in assets:
        with asset.open("rb") as stream:
            sums.append(f'{hashlib.file_digest(stream, "sha256").hexdigest()}  {asset.name}\n')
    (output / "SHA256SUMS.txt").write_text("".join(sums), encoding="ascii")
    print(f"RELEASE_PACKAGE_PASS {output}")


if __name__ == "__main__":
    main()
