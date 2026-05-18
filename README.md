# AppoMobi.Gestures

Cross-framework gesture recognition ecosystem for .NET. One interface (`IGestureListener`), shared data models, multiple platform implementations.

Used by [DrawnUI](https://github.com/taublast/DrawnUi).

---

## Packages

| Package | Target | Description |
|---------|--------|-------------|
| **AppoMobi.Gestures** | netstandard2.0 | Core contracts: `IGestureListener`, all event/data types. Zero dependencies. |
| **AppoMobi.Maui.Gestures** | net9.0 / net10 multi-platform | .NET MAUI implementation via `RoutingEffect`. |
| **AppoMobi.Blazor.Gestures** | net9.0 / net10.0 blazor web | Blazor implementation via JS pointer/wheel interop. |

```bash
# MAUI
dotnet add package AppoMobi.Maui.Gestures

# Blazor
dotnet add package AppoMobi.Blazor.Gestures
```

> **v2 migration**: shared types (`IGestureListener`, `TouchActionEventArgs`, enums…) moved from `AppoMobi.Maui.Gestures` namespace to `AppoMobi.Gestures`. Add `using AppoMobi.Gestures;` to files that reference them.

---

## Samples

The repo includes 2 sample apps:

* `samples/MauiSample` — .NET MAUI sample app for `AppoMobi.Maui.Gestures`
* `samples/BlazorWasmSample` — Blazor WebAssembly sample app for `AppoMobi.Blazor.Gestures`

There is also `dev/GesturesTester`, which is a manual validation app used for gesture testing during development.

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

`TouchActionEventArgs` carries everything: pixel location, source coordinate scale, velocity, distance totals, multi-touch manipulation (scale/rotation), mouse button state, pointer device type.

---

## .NET MAUI

Gestures are handled by a MAUI effect. Use attached properties for command-driven scenarios, or force-attach the effect when your control implements `IGestureListener` directly.

### Setup

```csharp
// MauiProgram.cs
builder.UseGestures();
```

### Basic Usage in XAML

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

### Basic Usage in Code-Behind

```csharp
TouchEffect.SetCommandTapped(myView, TapCommand);
TouchEffect.SetCommandTappedParameter(myView, itemData);
TouchEffect.SetCommandLongPressing(myView, LongPressCommand);
TouchEffect.SetForceAttach(myView, true);
TouchEffect.SetShareTouch(myView, TouchHandlingStyle.Lock);
```

### Enhanced Usage for Custom Controls

Attach the effect without command bindings when your control handles gestures itself:

```xml
<draw:Canvas
    touch:TouchEffect.ForceAttach="True" />
```

Or in code-behind:

```csharp
TouchEffect.SetForceAttach(myView, true);
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

Inject `TouchEffect`, attach it to an `ElementReference` after render, pass your `IGestureListener`:

```razor
@inject TouchEffect Gestures
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
Gestures.Draggable = true;      // Don't cancel Moved when pointer leaves element bounds

// Static scaling default for Blazor events
TouchEffect.Density = 1f;       // CSS pixels = points on web; set to devicePixelRatio for physical-pixel canvases
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
    var delta = args.Wheel.Delta;   // Positive = wheel up / away from the user
    var center = args.Wheel.Center; // Location of wheel event
}
```

---

## Touch Handling Modes

| Mode | Description |
|------|-------------|
| `Default` | Normal behavior |
| `Lock` | Blocks all parent input — use for canvases, drawing surfaces |
| `Manual` | Dynamic control via `WIllLock` at runtime — use for carousels inside ScrollView |
| `SoftLock` | Shares gestures with parent native scrolling surfaces until your control clearly takes over |
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

## Coordinate Scaling

For cases when your app renders with a scale different from the native area gestures were attached to (Blazor etc), the following could help.

`TouchActionEventArgs.Scale` stores the source scale used when the event was produced.
If your rendering surface uses a different scale, call `args.Rescale(renderingScale)` inside `OnGestureEvent` and use the returned event for hit testing or game logic.

```csharp
var gestureArgs = args;

if (renderingScale != args.Scale)
{
    gestureArgs = args.Rescale(renderingScale);
}

UseGesture(gestureArgs);
```

`Rescale` uses the ratio `renderingScale / args.Scale` internally and returns the original instance when no rescaling is needed.


## Gesture Data Reference

```csharp
// TouchActionEventArgs
args.Scale             // float  — source scale of the coordinates carried by this event
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

// Blazor — static defaults before creating instances
TouchEffect.LongPressTimeMsDefault = 1500;
TouchEffect.TappedCancelMoveThresholdPoints = 16f;
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

## What's New

* **3.10.2** —  Invert Blazor Wheel direction to match other platforms.
* **3.10.1** — `.NET 10` support added for both `AppoMobi.Maui.Gestures` and `AppoMobi.Blazor.Gestures`, while keeping `.NET 9` targets in place. Adds `TouchActionEventArgs.Scale` for source-coordinate scaling and `TouchActionEventArgs.Rescale(float)` for remapping gesture data to the consumer rendering scale.
* **1.11.9.2** — Android: built-in programmatic tap for sensitive screens (Galaxy S sends microscopic pans instead of tap). `TappedCancelMoveThresholdPoints` defaults to 16 (was 5).

---

## Contributing

1. 💬 [Discussion](https://github.com/taublast/AppoMobi.Maui.Gestures/discussions)
2. 🐛 [Issue](https://github.com/taublast/AppoMobi.Maui.Gestures/issues)
3. 🔧 [Pull Request](https://github.com/taublast/AppoMobi.Maui.Gestures/pulls)
