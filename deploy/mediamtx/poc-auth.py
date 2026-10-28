#!/usr/bin/env python3
"""Temporary publish and HLS gate until the API owns /internal/live/authorize.

Publish is allowed only when the RTMP password matches LIVE_INGEST_SECRET.
HLS reads are allowed only from localhost; nginx checks the short token first.
This process must stay on 127.0.0.1.
"""

import hashlib
import hmac
import json
import os
import re
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PATH_RE = re.compile(r"^[A-Za-z0-9_-]{1,64}$")
TOKEN_RE = re.compile(r"^([0-9]{1,12})\.([0-9a-f]{64})$")
MAX_TTL = 180
STREAM_RE = re.compile(r"^/hls/([A-Za-z0-9_-]{1,64})(?:/|$)")


def verify_token(secret: str, path: str, token: str, now: int) -> bool:
    if not secret or not PATH_RE.fullmatch(path or ""):
        return False
    match = TOKEN_RE.fullmatch(token or "")
    if not match:
        return False
    expires = int(match.group(1))
    if expires < now or expires > now + MAX_TTL:
        return False
    expected = hmac.new(secret.encode(), f"{expires}.{path}".encode(), hashlib.sha256).hexdigest()
    return hmac.compare_digest(match.group(2), expected) if len(match.group(2)) == len(expected) else False


def stream_from_uri(uri: str) -> str:
    path = (uri or "").split("?", 1)[0]
    match = STREAM_RE.match(path)
    return match.group(1) if match else ""


def token_from_cookie(header: str) -> str:
    for part in (header or "").split(";"):
        name, _, value = part.strip().partition("=")
        if name == "situ_live":
            return value
    return ""


class Handler(BaseHTTPRequestHandler):
    server_version = "situ-live-auth"

    def log_message(self, fmt, *args):
        # Do not write the request line. It can carry the ingest password.
        return

    def do_GET(self):
        if self.path.startswith("/hls-token"):
            self.handle_hls()
            return
        self.send_error(404)

    def do_POST(self):
        if self.path.split("?", 1)[0] == "/authorize":
            self.handle_authorize()
            return
        if self.path.startswith("/hls-token"):
            self.handle_hls()
            return
        self.send_error(404)

    def handle_authorize(self):
        length = int(self.headers.get("Content-Length") or "0")
        if length < 0 or length > 8192:
            self.send_error(400)
            return
        try:
            body = json.loads(self.rfile.read(length) or b"{}")
        except json.JSONDecodeError:
            self.send_error(400)
            return
        action = body.get("action") or ""
        path = body.get("path") or ""
        password = body.get("password") or ""
        ip = body.get("ip") or ""
        secret = os.environ.get("LIVE_INGEST_SECRET", "")
        allowed = False
        if action == "publish" and secret and PATH_RE.fullmatch(path) and len(password) == len(secret) and hmac.compare_digest(password, secret):
            allowed = True
        elif action == "read" and ip in {"127.0.0.1", "::1"} and PATH_RE.fullmatch(path):
            allowed = True
        if allowed:
            self.send_response(200)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        self.send_response(401)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def handle_hls(self):
        secret = os.environ.get("LIVE_HLS_SECRET", "")
        uri = self.headers.get("X-Original-Uri") or ""
        path = stream_from_uri(uri)
        now = int(__import__("time").time())
        token = self.headers.get("X-Live-Token") or token_from_cookie(self.headers.get("Cookie") or "")
        if not verify_token(secret, path, token, now):
            self.send_response(403)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        expires = int(token.split(".", 1)[0])
        remaining = max(1, expires - now)
        cookie = f"situ_live={token}; Path=/hls/; Secure; HttpOnly; SameSite=Lax; Max-Age={remaining}"
        self.send_response(204)
        self.send_header("X-Live-Cookie", cookie)
        self.send_header("Content-Length", "0")
        self.end_headers()


def main():
    host = "127.0.0.1"
    port = int(os.environ.get("LIVE_AUTH_PORT", "5099"))
    ThreadingHTTPServer((host, port), Handler).serve_forever()


if __name__ == "__main__":
    main()
