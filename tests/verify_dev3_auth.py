
"""Compatibilidad con el comando anterior: ejecuta la batería común de los seis proveedores."""
from verify_auth import main

"""Prueba HTTPS local, sin login real ni canje de códigos. Requiere API compilada.

Ejecutar desde cualquier directorio: python3 tests/verify_dev3_auth.py
El puerto 7133 debe estar libre. La segunda prueba consulta metadata pública OIDC.
"""

import html
import http.cookiejar
import json
import os
from pathlib import Path
import re
import ssl
import subprocess
import threading
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
BASE = "https://localhost:7133"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        return None


def verify(configured):
    # No utilizar secretos reales heredados del entorno para estas pruebas.
    environment = {k: v for k, v in os.environ.items() if not k.startswith("Authentication__")}
    environment["ASPNETCORE_ENVIRONMENT"] = "Development"
    environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information"
    for provider in ("Dropbox", "Microsoft"):
        environment[f"Authentication__{provider}__ClientId"] = ""
        environment[f"Authentication__{provider}__ClientSecret"] = ""
    if configured:
        environment.update({
            "Authentication__Dropbox__ClientId": "dev3-test-not-a-real-app",
            "Authentication__Dropbox__ClientSecret": "dev3-test-not-a-real-secret",
            "Authentication__Microsoft__ClientId": "00000000-0000-0000-0000-000000000000",
            "Authentication__Microsoft__ClientSecret": "dev3-test-not-a-real-secret",
            "Authentication__Microsoft__TenantId": "common",
        })
    process = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--project", "src/FinanzasPersonales.Api",
         "--no-launch-profile", "--urls", BASE],
        cwd=ROOT, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    ready = threading.Event()
    logs = []

    def read_logs():
        for line in process.stdout:
            logs.append(line)
            if "Now listening on:" in line:
                ready.set()

    reader = threading.Thread(target=read_logs, daemon=True)
    reader.start()
    try:
        assert ready.wait(30), "La API no inició: verificar certificado HTTPS y puerto 7133 libre."
        # Solo se omite la confianza del certificado autofirmado de localhost en ESTE cliente.
        # No modifica ni omite TLS de los middleware hacia Dropbox/Microsoft.
        opener = urllib.request.build_opener(
            NoRedirect(), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),
            urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))

        def request(path, data=None):
            body = None if data is None else urllib.parse.urlencode(data).encode()
            try:
                response = opener.open(BASE + path, data=body, timeout=60)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                return response.code, response.headers, response.read().decode()

        status, headers, page = request("/auth/dev3")
        assert status == 200
        assert "Iniciar sesión con Dropbox" in page and "Iniciar sesión con Microsoft" in page
        assert "no-store" in headers.get("Cache-Control", "")
        token = html.unescape(re.search(
            r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1))
        cookie = headers.get("Set-Cookie", "").lower()
        assert all(flag in cookie for flag in ("secure", "httponly", "samesite=strict"))
        assert json.loads(request("/auth/dev3/session")[2])["authenticated"] is False

        for provider in ("dropbox", "microsoft"):
            assert request(f"/auth/{provider}/login", {})[0] == 400
            assert request(f"/auth/{provider}/login")[0] == 405
            status, headers, _ = request(f"/auth/{provider}/login", {"__RequestVerificationToken": token})
            assert status == 302
            if not configured:
                assert headers["Location"] == f"/auth/dev3?error={provider}-not-configured"
                continue
            correlation = " ".join(headers.get_all("Set-Cookie", [])).lower()
            assert all(flag in correlation for flag in ("secure", "httponly", "samesite=none"))
            parts = urllib.parse.urlsplit(headers["Location"])
            query = urllib.parse.parse_qs(parts.query)
            host = "www.dropbox.com" if provider == "dropbox" else "login.microsoftonline.com"
            assert parts.scheme == "https" and parts.netloc == host
            assert query["response_type"] == ["code"]
            assert query["redirect_uri"] == [BASE + f"/auth/{provider}/callback"]
            assert query["code_challenge_method"] == ["S256"] and query.get("code_challenge")
            assert query.get("state")
            assert not any(key in query for key in ("client_secret", "access_token", "id_token"))
            if provider == "microsoft":
                assert query.get("nonce") and query["response_mode"] == ["form_post"]
            callback = f"/auth/{provider}/callback"
            cancellation = {"error": "access_denied", "state": query["state"][0]}
            if provider == "dropbox":
                status, headers, _ = request(callback + "?" + urllib.parse.urlencode(cancellation))
            else:
                status, headers, _ = request(callback, cancellation)
            assert status == 302 and headers["Location"] == f"/auth/dev3?error={provider}-cancelled"
            # El callback de cancelación consumió la correlación: no se puede reutilizar state.
            replay = urllib.parse.urlencode({"code": "not-a-real-code", "state": query["state"][0]})
            status, headers, _ = request(callback + "?" + replay)
            assert status == 302 and headers["Location"] == f"/auth/dev3?error={provider}-failed"
            status, headers, _ = request(callback + "?code=not-a-real-code&state=tampered")
            assert status == 302 and headers["Location"] == f"/auth/dev3?error={provider}-failed"
            assert ".FinanzasPersonales.Auth=" not in headers.get("Set-Cookie", "")
            assert json.loads(request("/auth/dev3/session")[2])["authenticated"] is False

        assert request("/auth/dev3/logout", {})[0] == 400
        assert request("/auth/dev3/logout")[0] == 405
        status, headers, _ = request("/auth/dev3/logout", {"__RequestVerificationToken": token})
        assert status == 302 and headers["Location"] == "/auth/dev3"
        assert ".FinanzasPersonales.Auth=" in headers.get("Set-Cookie", "")
        assert json.loads(request("/auth/dev3/session")[2])["authenticated"] is False
        status, _, document = request("/openapi/v1.json")
        assert status == 200 and "/api/Usuario" in json.loads(document)["paths"]
        mode = "challenge, cancelación y state alterado (IDs ficticios)" if configured else "sin credenciales"
        print(f"OK HTTPS: {mode}. No se prueba login real.")
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        reader.join(timeout=5)
        assert not any("dev3-test-not-a-real-secret" in line for line in logs), "Se filtró un secreto de prueba."


if __name__ == "__main__":
    main()
