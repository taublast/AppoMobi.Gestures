using AppoMobi.Gestures;

namespace AppleTouchTests;

/// Two phases. First a full-screen probe reports where two taps landed, so the harness can map
/// screen coordinates to device points without knowing the simulator's zoom or window chrome.
/// Then the real scene is built and its targets are published for the harness to tap.
public class TestPage : ContentPage
{
    readonly bool _runTests;

    // calibration
    readonly ProbeControl _calibration = new() { Name = "calib", ReportLocation = true };

    // the scene
    Button _outside = null!;
    TouchHost _hostDefault = null!, _hostLock = null!, _hostManual = null!, _hostSoftLock = null!;
    ProbeControl _probeDefault = null!, _probeLock = null!;
    Button _hosted = null!;
    Button _finish = null!;

    int _outsideClicked, _hostedClicked;
    bool _sceneBuilt, _evaluated;

    public TestPage()
    {
        _runTests = TestArgs.RunTests();
        Content = _runTests ? Calibration() : new Label { Text = "start with --run-tests" };
    }

    View Calibration()
    {
        _calibration.HorizontalOptions = LayoutOptions.Fill;
        _calibration.VerticalOptions = LayoutOptions.Fill;
        return _calibration;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_runTests) return;

        // The handler exists only once the page has been laid out.
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () => Log.Line("CALIB READY"));

        // Two calibration taps are enough to solve the mapping.
        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(200), () =>
        {
            if (_sceneBuilt) return false;
            if (_calibration.Ended < 2) return true;
            BuildScene();
            return false;
        });
    }

    void BuildScene()
    {
        _sceneBuilt = true;

        _outside = new Button { Text = "OUTSIDE", BackgroundColor = Colors.Magenta, TextColor = Colors.White };
        _outside.Clicked += (_, _) => { _outsideClicked++; Log.Line("EVENT outside clicked"); };

        _probeDefault = new ProbeControl { Name = "probeDefault" };
        _hosted = new Button { Text = "HOSTED", BackgroundColor = Colors.Magenta, TextColor = Colors.White };
        _hosted.Clicked += (_, _) => { _hostedClicked++; Log.Line("EVENT hosted clicked"); };

        _hostDefault = new TouchHost("hostDefault", TouchHandlingStyle.Default)
        {
            Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star) },
                Children = { _probeDefault, _hosted }
            }
        };
        Grid.SetRow(_hosted, 1);

        _probeLock = new ProbeControl { Name = "probeLock" };
        _hostLock = new TouchHost("hostLock", TouchHandlingStyle.Lock) { Content = _probeLock };

        // Only ever inspected, never tapped.
        _hostManual = new TouchHost("hostManual", TouchHandlingStyle.Manual)
        { Content = new BoxView { Color = Colors.DarkGreen }, WidthRequest = 40, HeightRequest = 40 };
        _hostSoftLock = new TouchHost("hostSoftLock", TouchHandlingStyle.SoftLock)
        { Content = new BoxView { Color = Colors.DarkOliveGreen }, WidthRequest = 40, HeightRequest = 40 };

        _finish = new Button { Text = "FINISH", BackgroundColor = Colors.Black, TextColor = Colors.White };
        _finish.Clicked += (_, _) => Evaluate();

        var inspected = new HorizontalStackLayout { Spacing = 8, Children = { _hostManual, _hostSoftLock } };

        // Fixed heights throughout: a Star row collapses to nothing here, and every row must have a
        // real height for the harness to have something to tap.
        var grid = new Grid
        {
            RowSpacing = 4,
            VerticalOptions = LayoutOptions.Fill,
            RowDefinitions =
            {
                new RowDefinition(70),    // outside button
                new RowDefinition(44),    // the two inspected-only hosts
                new RowDefinition(240),   // hostDefault: probe + hosted button
                new RowDefinition(160),   // hostLock: probe only
                new RowDefinition(60)     // finish
            },
            Children = { _outside, inspected, _hostDefault, _hostLock, _finish }
        };
        Grid.SetRow(_outside, 0);
        Grid.SetRow(inspected, 1);
        Grid.SetRow(_hostDefault, 2);
        Grid.SetRow(_hostLock, 3);
        Grid.SetRow(_finish, 4);

        Content = grid;

        // Publish the targets once this layout has reached the platform.
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), PublishTargets);
    }

    void PublishTargets()
    {
#if IOS || MACCATALYST
        Log.Line($"TARGET outside {Platform.RectOf(_outside)}");
        Log.Line($"TARGET probeDefault {Platform.RectOf(_probeDefault)}");
        Log.Line($"TARGET hosted {Platform.RectOf(_hosted)}");
        Log.Line($"TARGET probeLock {Platform.RectOf(_probeLock)}");
        Log.Line($"TARGET finish {Platform.RectOf(_finish)}");
#endif
        Log.Line("SCENE READY");

        // If the bug under test swallows the FINISH tap, still report rather than hang.
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(90), () =>
        {
            if (!_evaluated) { Log.Line("EVENT evaluating without a FINISH tap"); Evaluate(); }
        });
    }

    void Evaluate()
    {
        if (_evaluated) return;
        _evaluated = true;

        int passed = 0, failed = 0;
        void Check(string name, bool ok, string detail)
        {
            Log.Result(name, ok, detail);
            if (ok) passed++; else failed++;
        }

#if IOS || MACCATALYST
        // a. the recognizer is configured so UIKit does not hold the hosted views' touch end
        foreach (var host in new[] { _hostDefault, _hostLock, _hostManual, _hostSoftLock })
        {
            var info = Platform.Inspect(host);
            var wantsPan = Platform.ExpectsPan(host.Mode);
            var ok = info.Found
                     && !info.DelaysTouchesEnded
                     && !info.CancelsTouchesInView
                     && (!wantsPan || (info.PanFound && !info.PanDelaysTouchesEnded && !info.PanCancelsTouchesInView));
            Check($"config.{host.Mode}", ok, info.ToString());
        }
#endif

        // b. the hosted plain UIView sees both halves of both taps, and the second is not swallowed
        Check("hostedView.touches",
            _probeDefault.Began == 2 && _probeDefault.Ended == 2 && _probeDefault.Cancelled == 0,
            $"{_probeDefault.Counters} (want began=2 ended=2 cancelled=0)");

        // c. the hosted MAUI button completes its tap, twice
        Check("hostedButton.clicked", _hostedClicked == 2, $"clicked={_hostedClicked} (want 2)");

        // d. our own events still arrive for the host while its children are being touched
        Check("host.ownEvents", _hostDefault.Down >= 2 && _hostDefault.Up >= 2,
            $"{_hostDefault.Counters} (want down>=2 up>=2)");

        // e. the control outside the host: proves the harness can land a tap at all
        Check("outsideButton.clicked", _outsideClicked >= 1, $"clicked={_outsideClicked} (want >=1)");

        // f. a drag over the hosted view in a mode that installs the pan recognizer
        Check("hostedView.drag",
            _probeLock.Began >= 1 && _probeLock.Moved >= 1 && _probeLock.Ended >= 1 && _hostLock.Panning >= 1,
            $"{_probeLock.Counters} host({_hostLock.Counters}) (want began>=1 moved>=1 ended>=1 panning>=1)");

        Log.Line($"DONE passed={passed} failed={failed}");
    }
}
