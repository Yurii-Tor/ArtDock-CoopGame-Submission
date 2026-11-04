#!/bin/sh
set -eu
cd "$(dirname "$0")"
exec "./DockCoop.app/Contents/MacOS/Dock Co-op" --transport utp --host --profile mac-host --name MacHost "$@"
