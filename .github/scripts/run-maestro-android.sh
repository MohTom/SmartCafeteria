#!/usr/bin/env bash
set -euo pipefail

mkdir -p "$GITHUB_WORKSPACE/maestro-results"
adb install -r "$GITHUB_WORKSPACE/Mobile Application Cloud-Based Remote Management/CafeteriaRemotePlatform/Mobile/CafeteriaManager/android/app/build/outputs/apk/debug/app-debug.apk"
adb shell am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS >/dev/null 2>&1 || true

for _ in 1 2 3 4 5; do
  adb shell uiautomator dump /sdcard/uidump.xml >/dev/null 2>&1 || true
  adb pull /sdcard/uidump.xml "$GITHUB_WORKSPACE/maestro-results/uidump-check.xml" >/dev/null 2>&1 || true
  if [ -f "$GITHUB_WORKSPACE/maestro-results/uidump-check.xml" ] && grep -q "isn't responding" "$GITHUB_WORKSPACE/maestro-results/uidump-check.xml"; then
    python3 - <<'PY' || true
import os, re, subprocess, xml.etree.ElementTree as ET
path = os.path.join(os.environ["GITHUB_WORKSPACE"], "maestro-results", "uidump-check.xml")
try:
    root = ET.parse(path).getroot()
except Exception:
    raise SystemExit(0)
for label in ("Wait", "Close app"):
    for node in root.iter("node"):
        if node.attrib.get("text") == label:
            m = re.match(r"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", node.attrib.get("bounds", ""))
            if m:
                x1, y1, x2, y2 = map(int, m.groups())
                subprocess.run(["adb", "shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2)], check=False)
                raise SystemExit(0)
PY
    sleep 2
  else
    break
  fi
done

status=0
maestro test --format junit --output "$GITHUB_WORKSPACE/maestro-results/report.xml" --env E2E_EMAIL="$E2E_EMAIL" --env E2E_PASSWORD="$E2E_PASSWORD" "$GITHUB_WORKSPACE/.maestro/flows" || status=$?
adb shell uiautomator dump /sdcard/window.xml > "$GITHUB_WORKSPACE/maestro-results/uiautomator.txt" 2>&1 || true
adb pull /sdcard/window.xml "$GITHUB_WORKSPACE/maestro-results/window.xml" || true
adb shell dumpsys window > "$GITHUB_WORKSPACE/maestro-results/window-dumpsys.txt" 2>&1 || true
adb logcat -d > "$GITHUB_WORKSPACE/maestro-results/logcat.txt" 2>&1 || true
exit "$status"
