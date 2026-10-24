#!/bin/sh
#
# Runs dev/AppleTouchTests on an iOS simulator and reports the TOUCHTEST lines.
#
# Needs, all of which ship with a normal Mac dev setup:
#   - Xcode:  xcrun simctl, xcodebuild, swiftc   (no idb, no cliclick)
#   - .NET MAUI workload
#   - the Mac logged in with the screen UNLOCKED, because taps are posted as real
#     mouse events: a locked screen puts loginwindow in front and swallows them
#   - Accessibility permission for whatever runs this script (System Settings >
#     Privacy & Security > Accessibility), otherwise CGEventPost is ignored
#
# Exit code 0 only when every check passed.

set -u
ROOT=$(cd "$(dirname "$0")/.." && pwd)
PROJ=$ROOT/dev/AppleTouchTests/AppleTouchTests.csproj
BID=com.appomobi.appletouchtests
WORK=$(mktemp -d /tmp/appletouchtests.XXXXXX)
LOG=$WORK/app.log
# KEEP=1 leaves the work directory (app log, build log) in place for inspection
KEEP=${KEEP:-}
[ -n "$KEEP" ] || trap 'rm -rf "$WORK"' EXIT

# PLATFORM=ios (default) runs on a simulator; PLATFORM=maccatalyst runs the same app on this Mac.
# MSBuild reads environment variables as properties, and an exported PLATFORM collides with its own
# $(Platform), which silently moves the build output to bin/<platform>/. Take a copy and unset it.
TARGET=${PLATFORM:-ios}
unset PLATFORM
DEVICE_NAME=${DEVICE_NAME:-"AppleTouchTests iPhone"}
DEVICE_TYPE=${DEVICE_TYPE:-com.apple.CoreSimulator.SimDeviceType.iPhone-16}

say() { printf '%s\n' "$*"; }
die() { printf 'apple-touch-tests: %s\n' "$*" >&2; exit 2; }
[ -n "$KEEP" ] && say "keeping $WORK"

# ---------------------------------------------------------------- tap tool
cat > "$WORK/uitap.swift" <<'SWIFT'
import Cocoa

func post(_ type: CGEventType, _ p: CGPoint) {
    let src = CGEventSource(stateID: .hidSystemState)
    CGEvent(mouseEventSource: src, mouseType: type, mouseCursorPosition: p, mouseButton: .left)?
        .post(tap: .cghidEventTap)
}

let a = CommandLine.arguments
switch a.count > 1 ? a[1] : "" {
case "locked":
    // the screen lock puts loginwindow in front, where it eats every synthetic click
    let d = CGSessionCopyCurrentDictionary() as? [String: Any]
    let locked = (d?["CGSSessionScreenIsLocked"] as? Int) == 1
    print(locked ? "locked" : "unlocked")
    exit(locked ? 1 : 0)
case "activate":
    // Bring the app to the front: a click on an unfocused window is spent activating it.
    let pid = Int(a[2])!
    if let app = NSRunningApplication(processIdentifier: pid_t(pid)) {
        app.activate(options: [.activateAllWindows])
        usleep(600_000)
        exit(0)
    }
    exit(1)
case "windowpid":
    // windowpid <pid> -> "x y w h" of that process's biggest on-screen window.
    // Needed for Mac Catalyst, where the app's own window and a simulator window can share a name.
    let pid = Int(a[2])!
    let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as! [[String: Any]]
    var best: (Double, Double, Double, Double)? = nil
    for w in list {
        guard (w[kCGWindowOwnerPID as String] as? Int) == pid,
              let b = w[kCGWindowBounds as String] as? [String: Any],
              let x = b["X"] as? Double, let y = b["Y"] as? Double,
              let ww = b["Width"] as? Double, let hh = b["Height"] as? Double else { continue }
        if best == nil || ww * hh > best!.2 * best!.3 { best = (x, y, ww, hh) }
    }
    guard let r = best else { exit(1) }
    print("\(Int(r.0)) \(Int(r.1)) \(Int(r.2)) \(Int(r.3))")
    exit(0)
case "window":
    // window <name substring> -> "x y w h" of the first matching on-screen window
    let needle = a[2]
    let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as! [[String: Any]]
    for w in list {
        let name = (w[kCGWindowName as String] as? String) ?? ""
        guard name.contains(needle),
              let b = w[kCGWindowBounds as String] as? [String: Any],
              let x = b["X"] as? Double, let y = b["Y"] as? Double,
              let ww = b["Width"] as? Double, let hh = b["Height"] as? Double else { continue }
        print("\(Int(x)) \(Int(y)) \(Int(ww)) \(Int(hh))")
        exit(0)
    }
    exit(1)
case "tap":
    // tap <x> <y> [holdMs]
    let p = CGPoint(x: Double(a[2])!, y: Double(a[3])!)
    let hold = a.count > 4 ? Double(a[4])! : 110
    post(.mouseMoved, p); usleep(40_000)
    post(.leftMouseDown, p); usleep(UInt32(hold * 1000))
    post(.leftMouseUp, p)
case "drag":
    // drag <x1> <y1> <x2> <y2> [steps]
    let p1 = CGPoint(x: Double(a[2])!, y: Double(a[3])!)
    let p2 = CGPoint(x: Double(a[4])!, y: Double(a[5])!)
    let steps = a.count > 6 ? Int(a[6])! : 12
    post(.mouseMoved, p1); usleep(40_000)
    post(.leftMouseDown, p1); usleep(120_000)
    for i in 1...steps {
        let t = Double(i) / Double(steps)
        let q = CGPoint(x: p1.x + (p2.x - p1.x) * t, y: p1.y + (p2.y - p1.y) * t)
        let src = CGEventSource(stateID: .hidSystemState)
        CGEvent(mouseEventSource: src, mouseType: .leftMouseDragged, mouseCursorPosition: q, mouseButton: .left)?
            .post(tap: .cghidEventTap)
        usleep(30_000)
    }
    usleep(120_000)
    post(.leftMouseUp, p2)
default:
    print("usage: uitap locked | window <name> | windowpid <pid> | activate <pid> | tap x y [ms] | drag x1 y1 x2 y2 [steps]")
    exit(2)
}
SWIFT
swiftc -O -o "$WORK/uitap" "$WORK/uitap.swift" 2>/dev/null || die "could not build the tap tool (is Xcode installed?)"

# ---------------------------------------------------------------- lock + display
if ! "$WORK/uitap" locked >/dev/null; then
  die "the screen is locked; unlock the Mac and run again (taps are posted as real mouse events)"
fi
# Hold the display awake so the screen cannot lock halfway through the run.
caffeinate -d -i -w $$ &

if [ "$TARGET" = "maccatalyst" ]; then
  # ------------------------------------------------------------ Mac Catalyst
  WINDOW_NAME="AppleTouchTests"
  say "building (maccatalyst)..."
  # The RID is required: without it the first build after a clean reports success and emits no
  # app bundle at all, and only a second build produces one.
  [ "$(uname -m)" = "arm64" ] && RID=maccatalyst-arm64 || RID=maccatalyst-x64
  dotnet build "$PROJ" -f net10.0-maccatalyst -c Debug -p:RuntimeIdentifier=$RID     -v q --nologo > "$WORK/build.log" 2>&1 \
    || { tail -30 "$WORK/build.log"; die "build failed"; }
  APP=$(find "$ROOT/dev/AppleTouchTests/bin/Debug/net10.0-maccatalyst" -maxdepth 2 -name '*.app' -type d | head -1)
  [ -z "$APP" ] && die "built app bundle not found"
  # started from a subshell so the shell does not print its own "Terminated" notice later
  ( "$APP/Contents/MacOS/AppleTouchTests" --run-tests > "$LOG" 2>&1 & echo $! > "$WORK/app.pid" )
  APP_PID=$(cat "$WORK/app.pid")
  kill_app() { kill "$APP_PID" 2>/dev/null; }
else
  # ------------------------------------------------------------ iOS simulator
  WINDOW_NAME=$DEVICE_NAME
  UDID=$(xcrun simctl list devices -j 2>/dev/null \
    | python3 -c "import json,sys;d=json.load(sys.stdin)['devices'];print(next((x['udid'] for v in d.values() for x in v if x.get('name')=='$DEVICE_NAME' and x.get('isAvailable')),''))")
  if [ -z "$UDID" ]; then
    RUNTIME=$(xcrun simctl list runtimes -j | python3 -c "
import json,sys
rs=[r for r in json.load(sys.stdin)['runtimes'] if r.get('isAvailable') and 'iOS' in r['name']]
rs.sort(key=lambda r: [int(p) for p in r['version'].split('.')])
print(rs[-1]['identifier'] if rs else '')")
    [ -z "$RUNTIME" ] && die "no iOS simulator runtime is installed"
    UDID=$(xcrun simctl create "$DEVICE_NAME" "$DEVICE_TYPE" "$RUNTIME") || die "could not create a simulator"
    say "created simulator $DEVICE_NAME ($UDID)"
  fi
  say "simulator: $DEVICE_NAME ($UDID)"

  xcrun simctl boot "$UDID" >/dev/null 2>&1
  xcrun simctl bootstatus "$UDID" >/dev/null 2>&1
  open -a Simulator >/dev/null 2>&1
  sleep 4

  say "building (ios)..."
  dotnet build "$PROJ" -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64 \
    -p:CodesignKey= -p:CodesignProvision= -v q --nologo > "$WORK/build.log" 2>&1 \
    || { tail -30 "$WORK/build.log"; die "build failed"; }
  APP=$(find "$ROOT/dev/AppleTouchTests/bin/Debug/net10.0-ios" -maxdepth 2 -name '*.app' -type d | head -1)
  [ -z "$APP" ] && die "built app bundle not found"

  xcrun simctl terminate "$UDID" "$BID" >/dev/null 2>&1
  xcrun simctl install "$UDID" "$APP" >/dev/null 2>&1 || die "install failed"
  ( xcrun simctl launch --console-pty "$UDID" "$BID" --run-tests > "$LOG" 2>&1 & echo $! > "$WORK/app.pid" )
  APP_PID=$(cat "$WORK/app.pid")
  kill_app() { kill "$APP_PID" 2>/dev/null; xcrun simctl terminate "$UDID" "$BID" >/dev/null 2>&1; }
fi

waitfor() { # waitfor <pattern> <seconds>
  i=0
  while [ $i -lt "$2" ]; do
    grep -q "$1" "$LOG" 2>/dev/null && return 0
    i=$((i+1)); sleep 1
  done
  return 1
}

waitfor "CALIB READY" 90 || { kill_app; tail -20 "$LOG"; die "the app never started"; }

# Put the window we are about to tap in front, otherwise the first click only activates it.
if [ "$TARGET" = "maccatalyst" ]; then
  "$WORK/uitap" activate "$APP_PID" >/dev/null 2>&1
else
  "$WORK/uitap" activate "$(pgrep -x Simulator | head -1)" >/dev/null 2>&1
fi

if [ "$TARGET" = "maccatalyst" ]; then
  WIN=$("$WORK/uitap" windowpid "$APP_PID") || { kill_app; die "the app's window is not on screen"; }
else
  WIN=$("$WORK/uitap" window "$WINDOW_NAME") || { kill_app; die "simulator window not found on screen"; }
fi
set -- $WIN; WX=$1; WY=$2; WW=$3; WH=$4
say "window: $WX,$WY ${WW}x${WH}"

# Two taps whose screen position we know and whose device position the app reports.
# That solves the zoom and the window chrome without assuming either.
calib_count() { tr -d '\r' < "$LOG" | grep -cE "CALIB [0-9-]"; }

# Tap until two calibration points have been reported. The window can move when the app comes
# forward, so its position is read again before each tap.
C1X=0; C1Y=0; C2X=0; C2Y=0
try=0
while [ $try -lt 6 ]; do
  have=$(calib_count)
  [ "$have" -ge 2 ] && break
  if [ "$TARGET" = "maccatalyst" ]; then
    WIN=$("$WORK/uitap" windowpid "$APP_PID")
  else
    WIN=$("$WORK/uitap" window "$WINDOW_NAME")
  fi
  set -- $WIN; WX=$1; WY=$2; WW=$3; WH=$4
  if [ "$have" -eq 0 ]; then
    C1X=$((WX + WW * 40 / 100)); C1Y=$((WY + WH * 45 / 100))
    "$WORK/uitap" tap $C1X $C1Y 140
  else
    C2X=$((WX + WW * 62 / 100)); C2Y=$((WY + WH * 66 / 100))
    "$WORK/uitap" tap $C2X $C2Y 140
  fi
  try=$((try+1)); sleep 2
done
[ "$(calib_count)" -ge 2 ] || { kill_app; tail -20 "$LOG"; die "calibration failed: the taps never reached the app (screen locked, or Accessibility not granted?)"; }

waitfor "SCENE READY" 60 || { kill_app; tail -20 "$LOG"; die "the scene never became ready"; }

# the console pty ends its lines with CR, which would otherwise end up inside the numbers
MAP=$(tr -d '\r' < "$LOG" | grep -E "CALIB [0-9-]" | head -2 | sed 's/.*CALIB //' | tr '\n' ' ')
[ -z "$MAP" ] && { kill_app; die "no CALIB lines: the taps did not reach the app"; }
set -- $MAP
[ $# -lt 4 ] && { kill_app; die "only $# calibration numbers came back: $MAP"; }
D1X=$1; D1Y=$2; D2X=$3; D2Y=$4
read Z OX OY <<EOF
$(python3 -c "
d1x,d1y,d2x,d2y = $D1X,$D1Y,$D2X,$D2Y
s1x,s1y,s2x,s2y = $C1X,$C1Y,$C2X,$C2Y
z = ((s2x-s1x)/(d2x-d1x) + (s2y-s1y)/(d2y-d1y)) / 2
print(z, s1x - z*d1x, s1y - z*d1y)")
EOF
say "mapping: zoom=$Z origin=$OX,$OY"

centre() { # centre <target name> -> "x y" on screen
  rect=$(tr -d '\r' < "$LOG" | grep "TARGET $1 " | head -1 | sed "s/.*TARGET $1 //" | awk '{print $1}')
  [ -z "$rect" ] && return 1
  echo "$rect" | python3 -c "
import sys
x,y,w,h = [float(v) for v in sys.stdin.read().strip().split(',')]
print(int($OX + $Z*(x+w/2)), int($OY + $Z*(y+h/2)))"
}

tap_at() { set -- $(centre "$1") ; [ $# -eq 2 ] || die "no target $1"; "$WORK/uitap" tap "$1" "$2" 110; }

# e first: if the control outside the host does not click, the harness itself is broken
say "tapping..."
tap_at outside;       sleep 2
tap_at probeDefault;  sleep 2
tap_at probeDefault;  sleep 2
tap_at hosted;        sleep 2
tap_at hosted;        sleep 2

# f: a short drag across the hosted view of the Lock-mode host
set -- $(centre probeLock); DX=$1; DY=$2
"$WORK/uitap" drag $DX $((DY - 30)) $DX $((DY + 30)) 12; sleep 2

tap_at finish; sleep 3
waitfor "DONE passed=" 100 || { kill_app; tail -30 "$LOG"; die "the app never reported DONE"; }
kill_app

# ---------------------------------------------------------------- report
say ""
tr -d '\r' < "$LOG" | grep "TOUCHTEST " | sed 's/.*TOUCHTEST //' | grep -vE '^(CALIB|TARGET|EVENT|SCENE)'
say ""
FAILED=$(tr -d '\r' < "$LOG" | grep "TOUCHTEST DONE " | tail -1 | sed 's/.*failed=//')
[ "${FAILED:-1}" = "0" ] || exit 1
exit 0
