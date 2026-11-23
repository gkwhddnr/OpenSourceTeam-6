"""Compile against this project's Unity import cache and run managed checks without opening Unity."""
import json
import os
import re
import socket
import subprocess
import tempfile
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
WORK = HERE.parent / ".work"


class FakeRelay(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_POST(self):
        length = int(self.headers["Content-Length"])
        raw = self.rfile.read(length)
        if not raw or len(raw) != length:
            return  # The client can cancel while the request is still being sent.
        data = json.loads(raw)
        if self.path == "/api/v1/employee/evaluate-persona":
            self.persona(data)
            return
        common = {"requestId", "utterance", "employee"}
        employee = {"personality", "stress", "fatigue", "loyalty", "currentWork", "isMoving"}
        dialogue = self.path == "/api/v1/employee/dialogue"
        expected = common | ({"decision"} if dialogue else set())
        valid = (self.path in ("/api/v1/employee/classify", "/api/v1/employee/dialogue")
                 and set(data) == expected and set(data["employee"]) == employee
                 and "Authorization" not in self.headers)
        status = 200 if valid else 400
        answer = {"requestId": data["requestId"]}
        if dialogue:
            valid = data["decision"] == dict(intent="RESTOCK", target="RAMEN",
                                             tone="INSULT", reaction="REFUSE_EMOTION")
            answer["dialogue"] = "지금은 못 하겠어요." if valid else ""
            if data["utterance"] == "빈 대사":
                answer["dialogue"] = ""
        elif data["utterance"] == "한도 확인":
            status = 429
            answer["error"] = {"code": "RATE_LIMITED", "message": "PRIVATE upstream message"}
        else:
            answer.update(intent="RESTOCK", target="RAMEN", tone="INSULT", reaction="ACCEPT_COMPLAIN")
            if data["utterance"] == "다른 요청":
                answer["requestId"] = "00000000-0000-4000-b000-000000000001"
            if data["utterance"] == "잘못된 조합":
                answer["target"] = "CLICKED"
        body = json.dumps(answer, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def persona(self, data):
        valid = set(data) == {"requestId", "employeeName", "traits"} and "Authorization" not in self.headers
        answer = {"requestId": data["requestId"], "schemaVersion": "1", "hourlyWageWon": 16000,
                  "diligence": 85, "laziness": 35, "loyalty": 60, "workStressMultiplier": 1.25,
                  "workFatigueMultiplier": 1.15, "wageSensitivityMultiplier": 1.10,
                  "summaryReason": "업무 스트레스가 빨리 쌓이며 시급은 16,000원입니다."}
        traits = data.get("traits", "")
        if traits == "버전 오류": answer["schemaVersion"] = "2"
        if traits == "시급 오류": answer["hourlyWageWon"] = 30001
        if traits == "배율 오류": answer["workFatigueMultiplier"] = 1.234
        if traits == "누락 오류": del answer["loyalty"]
        if traits == "요청 오류": answer["requestId"] = "00000000-0000-4000-b000-000000000001"
        if traits == "심사지연": time.sleep(0.6)
        if not valid:
            answer = {"requestId": data.get("requestId", ""), "error": {"code": "INVALID_INPUT", "message": "PRIVATE"}}
        body = json.dumps(answer, ensure_ascii=False).encode("utf-8")
        self.send_response(200 if valid else 400)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass


def main():
    version_text = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text()
    version = re.search(r"m_EditorVersion: (\S+)", version_text).group(1)
    default = Path(os.environ["ProgramFiles"]) / "Unity/Hub/Editor" / version / "Editor/Data"
    editor = Path(os.environ.get("UNITY_EDITOR_DATA", default))
    compiler = next((editor / "DotNetSdk/sdk").glob("*/Roslyn/bincore/csc.dll"))
    rsp = max((ROOT / "Library/Bee/artifacts").glob("*.dag/Assembly-CSharp.rsp"),
              key=lambda path: path.stat().st_mtime)
    lines = rsp.read_text(encoding="utf-8-sig").splitlines()
    if any("Server~/" in line.replace("\\", "/") for line in lines):
        raise RuntimeError("Server sources were included in Unity's Assembly-CSharp inputs")
    WORK.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(dir=WORK) as folder:
        work = Path(folder)
        env = dict(os.environ, TMP=folder, TEMP=folder, DOTNET_CLI_HOME=folder,
                   DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
        env.pop("OPENAI_API_KEY", None)
        env["MONO_PATH"] = os.pathsep.join(str(p) for p in (
            ROOT / "Library/ScriptAssemblies", editor / "Managed/UnityEngine",
            editor / "BCLExtensions/runtime/netstandard2.1",
            editor / "MonoBleedingEdge/lib/mono/unityjit-win32",
            editor / "MonoBleedingEdge/lib/mono/unityjit-win32/Facades"))
        for player in (False, True):
            label = "WindowsPlayer" if player else "Editor"
            output = work / (label + ".exe")
            flags = [line for line in lines if not line.startswith(("-target:", "-out:", "-refout:"))]
            if not any("PersonaEvaluationClient.cs" in line for line in flags):
                flags.append('"Assets/_Project/LeeSeungJae/Scripts/PersonaEvaluationClient.cs"')
            if player:
                flags = [line for line in flags if not line.startswith("-define:UNITY_EDITOR")
                         and not (line.startswith('-r:"') and
                                  ("UnityEditor" in line or ".editor." in line.lower()))]
            flags.extend(["-target:exe", f'-out:"{output}"', f'"{HERE / "Checks.cs"}"'])
            args = work / (label + ".rsp")
            args.write_text("\n".join(flags), encoding="utf-8-sig")
            subprocess.run([editor / "DotNetSdk/dotnet.exe", compiler, "@" + str(args)],
                           cwd=ROOT, env=env, check=True)
            print(f"{label} C# compilation passed", flush=True)
        # Keep an unused port reserved without listening, for the stopped-server case.
        with socket.socket() as stopped, ThreadingHTTPServer(("127.0.0.1", 0), FakeRelay) as relay:
            stopped.bind(("127.0.0.1", 0))
            thread = threading.Thread(target=relay.serve_forever, daemon=True)
            thread.start()
            try:
                subprocess.run([editor / "MonoBleedingEdge/bin/mono.exe", output,
                                f"http://127.0.0.1:{relay.server_port}",
                                f"http://127.0.0.1:{stopped.getsockname()[1]}"],
                               cwd=work, env=env, check=True, timeout=60)
            finally:
                relay.shutdown()
                thread.join()


if __name__ == "__main__":
    main()
