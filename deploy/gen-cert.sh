#!/usr/bin/env bash
# Generate a private Certificate Authority and an HTTPS server certificate so
# Loopback can be served over https on a fully-local network - no public domain,
# no external CA, no admin. The server cert is issued to the server's IP via a
# SAN, so clients browse https://<ip>:5052 directly with no DNS/hosts changes.
#
# Runs anywhere with openssl: the Linux server, or Git Bash on your Windows dev
# box (Git for Windows ships openssl).
#
# Outputs into ./certs (next to this script):
#   rootCA.crt / rootCA.key  - your private CA. rootCA.crt is handed to clients
#                              (staged into ./client-cert). NEVER share rootCA.key.
#   server.crt / server.key  - the server cert+key. Copy BOTH into <app-dir>/certs
#                              on the server (referenced by appsettings.Production.json).
#
# Usage:  ./deploy/gen-cert.sh [server-ip]        (default 192.168.1.200)
#         SERVER_IP=10.0.0.5 ./deploy/gen-cert.sh
set -euo pipefail

# Git Bash (MSYS) rewrites arguments that look like Unix paths - e.g. openssl's
# -subj "/O=../CN=.." - into Windows paths, corrupting the certificate subject.
# Exclude only the DN args (file-path args still convert correctly). No-op on Linux.
export MSYS2_ARG_CONV_EXCL='/O=;/CN='

SERVER_IP="${1:-${SERVER_IP:-192.168.1.200}}"
DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="$DIR/certs"
CLIENT="$DIR/client-cert"
mkdir -p "$OUT" "$CLIENT"

echo "==> Server IP for the certificate: $SERVER_IP"

# --- Private root CA (10 years). Reused if it already exists so re-running only
#     reissues the server leaf and doesn't invalidate certs users already trust. ---
if [ -f "$OUT/rootCA.key" ] && [ -f "$OUT/rootCA.crt" ]; then
  echo "==> Reusing existing root CA (delete $OUT/rootCA.* to start over)"
else
  echo "==> Creating private root CA"
  openssl genrsa -out "$OUT/rootCA.key" 4096
  openssl req -x509 -new -nodes -key "$OUT/rootCA.key" -sha256 -days 3650 \
    -subj "/O=Loopback/CN=Loopback Local Root CA" -out "$OUT/rootCA.crt"
fi

# --- Server certificate (leaf, IP SAN, 10 years) ---
echo "==> Issuing server certificate for $SERVER_IP"
openssl genrsa -out "$OUT/server.key" 2048
openssl req -new -key "$OUT/server.key" -subj "/CN=Loopback ($SERVER_IP)" -out "$OUT/server.csr"

cat > "$OUT/server.ext" <<EOF
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=IP:$SERVER_IP,DNS:localhost
EOF

openssl x509 -req -in "$OUT/server.csr" -CA "$OUT/rootCA.crt" -CAkey "$OUT/rootCA.key" \
  -CAcreateserial -days 3650 -sha256 -extfile "$OUT/server.ext" -out "$OUT/server.crt"

rm -f "$OUT/server.csr" "$OUT/server.ext"

# Stage the PUBLIC root cert next to the client installer for distribution.
cp "$OUT/rootCA.crt" "$CLIENT/LoopbackRootCA.crt"

echo ""
echo "Done."
echo "  Server (copy BOTH into <app-dir>/certs/ on the server):"
echo "      $OUT/server.crt"
echo "      $OUT/server.key"
echo "  Clients (distribute this whole folder, then have each user run the .bat):"
echo "      $CLIENT/"
