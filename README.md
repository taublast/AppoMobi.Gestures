# AppoMobi.Gestures

Cross-framework gesture recognition ecosystem for .NET. One interface (`IGestureListener`), shared data models, multiple platform implementations.

> **Repo rename incoming** — this repo will become `AppoMobi.Gestures` to reflect the broader scope.

Used by [DrawnUI](https://github.com/taublast/DrawnUi).

---

## Packages

| Package | Target | Description |
|---------|--------|-------------|
| **AppoMobi.Gestures** | netstandard2.0 | Core contracts: `IGestureListener`, all event/data types. Zero dependencies. |
| **AppoMobi.Maui.Gestures** | net9.0 / net10 multi-platform | .NET MAUI implementation via `RoutingEffect`. |
| **AppoMobi.Blazor.Gestures** | net9.0 / net10.0 | Blazor implementation via JS pointer/wheel interop. |

```bash
# MAUI
dotnet add package AppoMobi.Maui.Gestures

# Blazor
dotnet add package AppoMobi.Blazor.Gestures

# Shared contracts only (for library authors building their own platform impl)
dotnet add package AppoMobi.Gestures
```

> **v2 migration**: shared types (`IGestureListener`, `TouchActionEventArgs`, enums…) moved from `AppoMobi.Maui.Gestures` namespace to `AppoMobi.Gestures`. Add `using AppoMobi.Gestures;` to files that reference them.

---

## The Interface

Both MAUI and Blazor implementations route all gesture events through one interface. Implement it to receive processed gesture results regardless of platform:

```csharp
public interface IGestureListener
{
    void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action);
    bool InputTransparent { get; }
}
```

`TouchActionResult` is the processed high-level result:

| Value | Meaning |
|-------|---------|
| `Down` | First contact |
| `Up` | Contact ended |
| `Tapped` | Quick tap within move threshold |
| `LongPressing` | Held past long-press duration |
| `Panning` | Moving with contact |
| `Wheel` | Mouse wheel / trackpad scroll |
| `Pointer` | Mouse/pen hover (no button held) |

`TouchActionEventArgs` carries everything: pixel location, velocity, distance totals, multi-touch manipulation (scale/rotation), mouse button state, pointer device type.

---

## .NET MAUI

### Setup

```csharp
// MauiProgram.cs
builder.UseGestures();
```

### XAML

```xml
<ContentPage xmlns:touch="clr-namespace:AppoMobi.Gestures;assembly=AppoMobi.Maui.Gestures">

    <Label Text="Tap Me!"
           touch:TouchEffect.CommandTapped="{Binding TapCommand}" />

    <Frame touch:TouchEffect.CommandTapped="{Binding ItemTappedCommand}"
           touch:TouchEffect.CommandTappedParameter="{Binding .}"
           touch:TouchEffect.CommandLongPressing="{Binding LongPressCommand}">
        <Label Text="Long press or tap me!" />
    </Frame>

</ContentPage>
```

### Code-Behind

```csharp
TouchEffect.SetCommandTapped(myView, TapCommand);
TouchEffect.SetCommandTappedParameter(myView, itemData);
TouchEffect.SetCommandLongPressing(myView, LongPressCommand);
TouchEffect.SetForceAttach(myView, true);
TouchEffect.SetShareTouch(myView, TouchHandlingStyle.Lock);
```

### IGestureListener in MAUI

```csharp
public class MyControl : ContentView, IGestureListener
{
    public MyControl()
    {
        TouchEffect.SetForceAttach(this, true);
    }

    public bool InputTransparent => false;

    public void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action)
    {
        switch (action)
        {
            case TouchActionResult.Down:
                _startPoint = args.Location;
                break;

            case TouchActionResult.Panning:
                var delta = args.Distance.Delta;
                var velocity = args.Distance.Velocity;
                if (args.Manipulation != null)
                {
                    var scale = args.Manipulation.Scale;
                    var rotation = args.Manipulation.Rotation;
                }
                break;

            case TouchActionResult.Tapped:
                ExecuteTapAction();
                break;

            case TouchActionResult.LongPressing:
                ShowContextMenu();
                break;
        }
    }
}
```

---

## Blazor

### Setup

```csharp
// Program.cs
builder.Services.AddBlazorGestures();
```

No JS imports needed — the package serves `canvasGestures.js` automatically from `_content/AppoMobi.Blazor.Gestures/canvasGestures.js`.

### Usage

Inject `BlazorGestureEffect`, attach it to an `ElementReference` after render, pass your `IGestureListener`:

```razor
@inject BlazorGestureEffect Gestures
@implements IAsyncDisposable
@using AppoMobi.Gestures

<div @ref="_container" style="width:400px;height:300px;touch-action:none;">
    <!-- content -->
</div>

@code {
    private ElementReference _container;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await Gestures.AttachAsync(_container, new MyGestureListener());
    }

    public async ValueTask DisposeAsync() => await Gestures.DisposeAsync();
}
```

> **Important**: set `touch-action: none` on the element so the browser does not consume pointer events before JS sees them.

### IGestureListener in Blazor

Same interface, same event args — only the host differs:

```csharp
using AppoMobi.Gestures;

public class MyGestureListener : IGestureListener
{
    public bool InputTransparent => false;

    public void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action)
    {
        switch (action)
        {
            case TouchActionResult.Tapped:
                Console.WriteLine($"Tapped at {args.Location}");
                break;

            case TouchActionResult.Panning:
                Console.WriteLine($"Panning delta {args.Distance.Delta}");
                break;

            case TouchActionResult.LongPressing:
                Console.WriteLine("Long press!");
                break;

            case TouchActionResult.Wheel:
                Console.WriteLine($"Wheel delta {args.Wheel?.Delta}");
                break;

            case TouchActionResult.Pointer:
                // Mouse/pen hover — no button held
                Console.WriteLine($"Hover at {args.Location}");
                break;
        }
    }
}
```

### Blazor Configuration

```csharp
// Per-instance tuning (before AttachAsync)
Gestures.LongPressTimeMs = 1000;
Gestures.Density = 1f;          // CSS pixels = points on web; set to devicePixelRatio for physical-pixel canvases
Gestures.Draggable = true;      // Don't cancel Moved when pointer leaves element bounds
```

### Pointer and Wheel Data

Mouse, pen and touch events all populate `args.Pointer`:

```csharp
if (args.Pointer != null)
{
    var device = args.Pointer.DeviceType;       // Mouse / Pen / Touch
    var button = args.Pointer.Button;           // Left / Right / Middle / XButton1…
    var pressed = args.Pointer.PressedButtons;  // Flags of all currently held buttons
    var pressure = args.Pointer.Pressure;       // 0.0–1.0 (pen), 1.0 (mouse)
}

if (args.Wheel != null)
{
    var delta = args.Wheel.Delta;   // Positive = scroll down
    var center = args.Wheel.Center; // Location of wheel event
}
```

---

## Touch Handling Modes (MAUI)

| Mode | Description |
|------|-------------|
| `Default` | Normal behavior |
| `Lock` | Blocks all parent input — use for canvases, drawing surfaces |
| `Manual` | Dynamic control via `WIllLock` at runtime — use for carousels inside ScrollView |
| `Disabled` | Same as `InputTransparent = true` |

### Manual Mode Example

```csharp
public class MyCarousel : ContentView, IGestureListener
{
    private bool _isHandling = false;

    public MyCarousel()
    {
        TouchEffect.SetShareTouch(this, TouchHandlingStyle.Manual);
        TouchEffect.SetForceAttach(this, true);
    }

    public bool InputTransparent => false;

    public void OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult action)
    {
        var effect = TouchEffect.GetFrom(this);

        switch (action)
        {
            case TouchActionResult.Down:
                _isHandling = false;
                break;

            case TouchActionResult.Panning:
                var dx = Math.Abs(args.Distance.Delta.X);
                var dy = Math.Abs(args.Distance.Delta.Y);

                if (!_isHandling)
                {
                    if (dx > dy && dx > 5)
                    {
                        _isHandling = true;
                        effect.WIllLock = ShareLockState.Locked;    // block parent ScrollView
                    }
                    else if (dy > 5)
                    {
                        effect.WIllLock = ShareLockState.Unlocked;  // let parent scroll
                        return;
                    }
                }

                if (_isHandling)
                    ScrollBy(args.Distance.Delta.X);
                break;

            case TouchActionResult.Up:
                if (_isHandling)
                    SnapToNearestItem();
                break;
        }
    }
}
```

---

## Gesture Data Reference

```csharp
// TouchActionEventArgs
args.Location          // PointF — current hit position in pixels (CSS pixels on web)
args.StartingLocation  // PointF — where the gesture began
args.NumberOfTouches   // int   — active touch/pointer count
args.IsInContact       // bool  — gesture started inside the view
args.IsInsideView      // bool  — current hit is inside view bounds
args.PreventDefault    // bool  — set true in LongPressing to suppress Tapped

// Distance info (all in pixels)
args.Distance.Delta        // PointF — movement since last event
args.Distance.Total        // PointF — total movement from start
args.Distance.Velocity     // PointF — pixels/second
args.Distance.TotalVelocity

// Multi-touch (null for single touch)
args.Manipulation?.Scale         // double — scale change this frame
args.Manipulation?.ScaleTotal    // double — scale from gesture start
args.Manipulation?.Rotation      // double — rotation change this frame (degrees)
args.Manipulation?.RotationTotal // double — rotation from start
args.Manipulation?.Center        // PointF — centroid of all active pointers
```

---

## Static Configuration

```csharp
// MAUI — global defaults
TouchEffect.LongPressTimeMsDefault = 1500;          // ms
TouchEffect.TappedCancelMoveThresholdPoints = 16f;  // points; movement above this cancels tap
TouchEffect.LogEnabled = true;

// Blazor — per-instance (or change the static default before creating instances)
BlazorGestureEffect.LongPressTimeMsDefault = 1500;
BlazorGestureEffect.TappedCancelMoveThresholdPoints = 16f;
```

---

## Building Your Own Platform Implementation

Reference `AppoMobi.Gestures` only, implement `IGestureListener` on your controls, and bridge your platform's pointer/touch events to `TouchActionEventArgs`. Use `TouchActionEventArgs.FillDistanceInfo` for velocity and distance tracking, and `MultitouchTracker` for pinch/rotation:

```csharp
// On each raw platform pointer event:
var args = new TouchActionEventArgs(pointerId, TouchActionType.Moved, new PointF(x, y), context);
TouchActionEventArgs.FillDistanceInfo(args, previousArgs);
var manipulation = _tracker.AddMovement(pointerId, args.Location);
args.Manipulation = manipulation;
// ... route to your listener
```

---

## MAUI — What's New

* **1.11.9.2** — Android: built-in programmatic tap for sensitive screens (Galaxy S sends microscopic pans instead of tap). `TappedCancelMoveThresholdPoints` defaults to 16 (was 5).
* **2.0** — Multi-package architecture. Core types extracted to `AppoMobi.Gestures` (netstandard2.0). `AppoMobi.Blazor.Gestures` added.

---

## Contributing

1. 💬 [Discussion](https://github.com/taublast/AppoMobi.Maui.Gestures/discussions)
2. 🐛 [Issue](https://github.com/taublast/AppoMobi.Maui.Gestures/issues)
3. 🔧 [Pull Request](https://github.com/taublast/AppoMobi.Maui.Gestures/pulls)
