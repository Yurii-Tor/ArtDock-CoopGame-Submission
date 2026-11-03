"""Package a Windows-cross-built macOS app with Unix executable permissions."""
import argparse
import os
import pathlib
import plistlib
import stat
import zipfile


def package(source: pathlib.Path, destination: pathlib.Path) -> None:
    contents = source / "DockCoop.app/Contents"
    with (contents / "Info.plist").open("rb") as plist:
        executable = contents / "MacOS" / plistlib.load(plist)["CFBundleExecutable"]
    if not executable.is_file():
        raise SystemExit(f"Missing app executable: {executable}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(source.rglob("*")):
            if (not path.is_file() and not path.is_symlink()) or any("DoNotShip" in part for part in path.parts):
                continue
            name = path.relative_to(source).as_posix()
            info = zipfile.ZipInfo.from_file(path, name)
            info.create_system = 3
            if path.is_symlink():
                info.external_attr = (stat.S_IFLNK | 0o777) << 16
                archive.writestr(info, os.readlink(path).replace("\\", "/"))
                continue
            with path.open("rb") as original:
                magic = original.read(4)
            # Framework executables do not necessarily have an extension or a MacOS parent.
            macho = magic in (b"\xfe\xed\xfa\xce", b"\xce\xfa\xed\xfe", b"\xfe\xed\xfa\xcf",
                              b"\xcf\xfa\xed\xfe", b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca",
                              b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca")
            is_executable = macho or path.suffix in (".command", ".dylib") or "/MacOS/" in name
            info.external_attr = (stat.S_IFREG | (0o755 if is_executable else 0o644)) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            with path.open("rb") as original, archive.open(info, "w") as target:
                while chunk := original.read(1024 * 1024):
                    target.write(chunk)
    with zipfile.ZipFile(destination) as archive:
        corrupt = archive.testzip()
        if corrupt:
            raise SystemExit(f"Archive CRC failed: {corrupt}")
    print(f"Packaged {destination} ({destination.stat().st_size} bytes)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=pathlib.Path, default=pathlib.Path("Builds/macOS"))
    parser.add_argument("--output", type=pathlib.Path, default=pathlib.Path("Artifacts/DockCoop-macOS.zip"))
    args = parser.parse_args()
    package(args.source, args.output)
