#!/bin/sh
set -eu
cd "$(dirname "$0")"
printf 'Host LAN/VPN IPv4 address [127.0.0.1]: '
IFS= read -r address
address=${address:-127.0.0.1}
exec "./DockCoop.app/Contents/MacOS/Dock Co-op" --transport utp --join --profile mac-client --name MacClient --address "$address" "$@"
