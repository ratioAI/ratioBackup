#!/usr/bin/env python3
"""Fasst ein CI-Ergebnis als Markdown zusammen.
Aufruf: summarize.py JOBNAME OUTCOME LOGDATEI [ERGEBNISORDNER]
Liest JUnit-XML (Gradle) und TRX (dotnet) und listet Fehlschläge auf."""
import pathlib, re, sys
import xml.etree.ElementTree as ET

job, outcome, log = sys.argv[1], sys.argv[2], pathlib.Path(sys.argv[3])
results = pathlib.Path(sys.argv[4]) if len(sys.argv) > 4 else None
out = [f"## {job}: {outcome}"]
passed = failed = 0
failures = []

if results and results.exists():
    for f in results.rglob("*.xml"):
        try:
            root = ET.parse(f).getroot()
        except ET.ParseError:
            continue
        for case in root.iter("testcase"):
            fail = case.find("failure")
            if fail is None:
                fail = case.find("error")
            name = f"{case.get('classname')}.{case.get('name')}"
            if fail is not None:
                failed += 1
                failures.append((name, (fail.get("message") or "") + "\n" + (fail.text or "")))
            elif case.find("skipped") is None:
                passed += 1
    ns = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
    for f in results.rglob("*.trx"):
        root = ET.parse(f).getroot()
        for r in root.iter(ns + "UnitTestResult"):
            if r.get("outcome") == "Passed":
                passed += 1
            elif r.get("outcome") == "Failed":
                failed += 1
                msg = r.find(f"{ns}Output/{ns}ErrorInfo/{ns}Message")
                st = r.find(f"{ns}Output/{ns}ErrorInfo/{ns}StackTrace")
                failures.append((r.get("testName"), ((msg.text if msg is not None else "") or "") + "\n" + ((st.text if st is not None else "") or "")[:600]))

out.append(f"Tests: {passed} bestanden, {failed} fehlgeschlagen")
for name, text in failures[:40]:
    out.append(f"- FAIL {name}\n```\n{text.strip()[:1200]}\n```")

if outcome != "success" and log.exists():
    lines = log.read_text(errors="replace").splitlines()
    errs = [l for l in lines if re.search(r"(^e: |error|FAILED|Exception|What went wrong)", l, re.I)]
    out.append("Fehlerzeilen:\n```\n" + "\n".join(errs[:80]) + "\n```")
    out.append("Logende:\n```\n" + "\n".join(lines[-40:]) + "\n```")
print("\n".join(out))
