"""Prueba aislada: reutiliza API/contexto y PostgreSQL local instalado, sin otro proyecto .NET.

Requiere dotnet build src/FinanzasPersonales.Api, certificado HTTPS y binarios PostgreSQL.
No lee .env, no toca el servidor de desarrollo y no efectúa login externo real.
"""
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "src/FinanzasPersonales.Api/bin/Debug/net10.0"


def run():
    sdks = subprocess.check_output(["dotnet", "--list-sdks"], text=True).splitlines()
    sdk_line = [line for line in sdks if line.startswith("10.")][-1]
    version, location = sdk_line.split(" ", 1)
    dotnet_root = Path(location.strip("[]")).parent
    pg_config = shutil.which("pg_config")
    if pg_config:
        postgres = Path(subprocess.check_output([pg_config, "--bindir"], text=True).strip())
    else:
        candidates = sorted(Path("/usr/lib/postgresql").glob("*/bin/initdb"))
        if not candidates:
            raise SystemExit("Instalar los binarios PostgreSQL locales (initdb/pg_ctl).")
        postgres = candidates[-1].parent
    assert OUTPUT.joinpath("FinanzasPersonales.Api.dll").exists(), "Compilar primero la API."
    harness = OUTPUT / "Dev3ControllersVerification.dll"
    assert not harness.exists(), "Ya existe el artefacto de pruebas; revisar antes de sobrescribirlo."
    temp_root = Path("/tmp/opencode")
    # El directorio temporal solo contiene fixtures creados por esta prueba.
    with tempfile.TemporaryDirectory(prefix="dev3-", dir=temp_root if temp_root.exists() else None) as temporary:
        directory = Path(temporary)
        data = directory / "pgdata"
        environment = os.environ.copy()
        environment.pop("PGPASSWORD", None)
        with socket.socket() as reserved:
            reserved.bind(("127.0.0.1", 0))
            port = reserved.getsockname()[1]
        started = False
        try:
            subprocess.run([str(postgres / "initdb"), "-D", str(data), "-U", "dev3_test",
                            "-A", "trust", "--no-locale", "--encoding=UTF8"],
                           env=environment, stdout=subprocess.DEVNULL, check=True)
            # Sin TCP: solo socket Unix privado dentro del directorio temporal.
            subprocess.run([str(postgres / "pg_ctl"), "-D", str(data), "-l", str(directory / "postgres.log"),
                            "-o", f"-h '' -p {port} -k {directory}", "-w", "start"],
                           env=environment, stdout=subprocess.DEVNULL, check=True)
            started = True
            refs = []
            for name in ("Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref"):
                versions = list((dotnet_root / "packs" / name).glob("10.*/ref/net10.0"))
                assert versions, f"No se encontraron referencias de {name} para .NET 10."
                newest = max(versions, key=lambda p: tuple(map(int, p.parents[1].name.split("."))))
                refs.extend(newest.glob("*.dll"))
            refs.extend(OUTPUT.glob("*.dll"))
            compile_command = ["dotnet", str(dotnet_root / "sdk" / version / "Roslyn/bincore/csc.dll"),
                               "-nologo", "-target:exe", "-nullable:enable", f"-out:{harness}"]
            compile_command.extend(f"-r:{path}" for path in refs)
            compile_command.append(str(ROOT / "tests/Dev3ControllersVerification.cs"))
            subprocess.run(compile_command, check=True)
            connection = f"Host={directory};Port={port};Database=postgres;Username=dev3_test;Include Error Detail=false"
            subprocess.run(["dotnet", "exec", "--runtimeconfig", str(OUTPUT / "FinanzasPersonales.Api.runtimeconfig.json"),
                            "--depsfile", str(OUTPUT / "FinanzasPersonales.Api.deps.json"), str(harness), connection, str(ROOT)],
                           env=environment, check=True, timeout=120)
        finally:
            if started:
                subprocess.run([str(postgres / "pg_ctl"), "-D", str(data), "-m", "fast", "-w", "stop"],
                               env=environment, stdout=subprocess.DEVNULL, check=True)
            if harness.exists():
                harness.unlink()


if __name__ == "__main__":
    run()
