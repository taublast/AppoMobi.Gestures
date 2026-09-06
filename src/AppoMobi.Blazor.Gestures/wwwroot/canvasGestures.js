function getOffset(element, event) {
    const rect = element.getBoundingClientRect();
    return {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top,
        inside: event.clientX >= rect.left && event.clientX <= rect.right && event.clientY >= rect.top && event.clientY <= rect.bottom
    };
}

const POLICY_PREVENT_DEFAULT = 1;
const POLICY_CAPTURE_POINTER = 1 << 1;
const POLICY_RELEASE_POINTER = 1 << 2;

let densityWatcherState = null;

function getCurrentDensity() {
    const density = window.devicePixelRatio;
    return Number.isFinite(density) && density > 0 ? density : 1;
}

function detachDensityMediaListener() {
    const state = densityWatcherState;
    if (!state?.mediaQuery || !state.mediaListener) {
        return;
    }

    if (typeof state.mediaQuery.removeEventListener === 'function') {
        state.mediaQuery.removeEventListener('change', state.mediaListener);
    } else if (typeof state.mediaQuery.removeListener === 'function') {
        state.mediaQuery.removeListener(state.mediaListener);
    }

    state.mediaQuery = null;
    state.mediaListener = null;
}

function attachDensityMediaListener() {
    const state = densityWatcherState;
    if (!state || typeof window.matchMedia !== 'function') {
        return;
    }

    detachDensityMediaListener();

    state.mediaQuery = window.matchMedia(`(resolution: ${state.currentDensity}dppx)`);
    state.mediaListener = () => {
        queueDensityNotification();
    };

    if (typeof state.mediaQuery.addEventListener === 'function') {
        state.mediaQuery.addEventListener('change', state.mediaListener);
    } else if (typeof state.mediaQuery.addListener === 'function') {
        state.mediaQuery.addListener(state.mediaListener);
    }
}

async function notifyDensity(force = false) {
    const state = densityWatcherState;
    if (!state) {
        return;
    }

    const density = getCurrentDensity();
    if (!force && state.hasNotified && Math.abs(density - state.currentDensity) < 0.01) {
        return;
    }

    state.currentDensity = density;
    state.hasNotified = true;
    attachDensityMediaListener();

    const deadRefs = [];
    for (const dotNetRef of state.refs) {
        try {
            await dotNetRef.invokeMethodAsync('OnDensityChanged', density);
        } catch {
            deadRefs.push(dotNetRef);
        }
    }

    for (const deadRef of deadRefs) {
        state.refs.delete(deadRef);
    }
}

function queueDensityNotification(force = false) {
    const state = densityWatcherState;
    if (!state) {
        return;
    }

    if (force) {
        state.forceNotify = true;
    }

    if (state.notifyQueued) {
        return;
    }

    state.notifyQueued = true;
    Promise.resolve().then(async () => {
        const activeState = densityWatcherState;
        if (!activeState) {
            return;
        }

        activeState.notifyQueued = false;
        const shouldForce = activeState.forceNotify;
        activeState.forceNotify = false;
        await notifyDensity(shouldForce);
    });
}

function ensureDensityWatcher() {
    if (densityWatcherState) {
        return densityWatcherState;
    }

    const queue = () => {
        queueDensityNotification();
    };

    densityWatcherState = {
        refs: new Set(),
        currentDensity: getCurrentDensity(),
        hasNotified: false,
        notifyQueued: false,
        forceNotify: false,
        mediaQuery: null,
        mediaListener: null,
        resizeHandler: queue,
        orientationHandler: queue,
        viewportResizeHandler: queue
    };

    window.addEventListener('resize', densityWatcherState.resizeHandler, { passive: true });
    window.addEventListener('orientationchange', densityWatcherState.orientationHandler, { passive: true });

    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', densityWatcherState.viewportResizeHandler, { passive: true });
    }

    return densityWatcherState;
}

function cleanupDensityWatcherIfUnused() {
    const state = densityWatcherState;
    if (!state || state.refs.size > 0) {
        return;
    }

    window.removeEventListener('resize', state.resizeHandler);
    window.removeEventListener('orientationchange', state.orientationHandler);

    if (window.visualViewport) {
        window.visualViewport.removeEventListener('resize', state.viewportResizeHandler);
    }

    detachDensityMediaListener();
    densityWatcherState = null;
}

function registerDensityRef(dotNetRef) {
    const state = ensureDensityWatcher();
    state.refs.add(dotNetRef);
    queueDensityNotification(true);
}

function unregisterDensityRef(dotNetRef) {
    const state = densityWatcherState;
    if (!state) {
        return;
    }

    state.refs.delete(dotNetRef);
    cleanupDensityWatcherIfUnused();
}

function suppressBrowserDefault(event) {
    if (event.cancelable) {
        event.preventDefault();
    }

    event.stopPropagation();
}

function detachInternal(element) {
    const state = element.__drawnUiGestures;
    if (!state) {
        return;
    }

    unregisterDensityRef(state.dotNetRef);

    for (const [name, handler] of Object.entries(state.handlers)) {
        element.removeEventListener(name, handler);
    }

    for (const [name, handler] of Object.entries(state.documentHandlers ?? {})) {
        document.removeEventListener(name, handler, true);
    }

    delete element.__drawnUiGestures;
}

export async function attachCanvasGestures(element, dotNetRef, enabled, lockTouches) {
    detachInternal(element);

    if (!enabled) {
        return;
    }

    registerDensityRef(dotNetRef);

    const activeDirectPointers = new Set();

    const invokePointer = (type, event) => {
        const offset = getOffset(element, event);
        const isDirectTouchPointer = event.pointerType === 'touch' || event.pointerType === 'pen';

        try {
            const policy = dotNetRef.invokeMethod('OnCanvasPointer', {
                type,
                pointerId: event.pointerId ?? 0,
                offsetX: offset.x,
                offsetY: offset.y,
                button: event.button ?? 0,
                buttons: event.buttons ?? 0,
                pointerType: event.pointerType ?? 'mouse',
                pressure: event.pressure ?? 0,
                isInsideView: offset.inside
            });

            if ((policy & POLICY_PREVENT_DEFAULT) !== 0) {
                suppressBrowserDefault(event);
            }

            if (type === 'pointerdown' && isDirectTouchPointer) {
                activeDirectPointers.add(event.pointerId);
            }

            if (((policy & POLICY_CAPTURE_POINTER) !== 0 || (type === 'pointerdown' && isDirectTouchPointer)) && typeof element.setPointerCapture === 'function') {
                try {
                    element.setPointerCapture(event.pointerId);
                } catch {
                }
            }

            if (type === 'pointerup' || type === 'pointercancel' || type === 'pointerleave') {
                activeDirectPointers.delete(event.pointerId);
            }

            if (((policy & POLICY_RELEASE_POINTER) !== 0 || ((type === 'pointerup' || type === 'pointercancel' || type === 'pointerleave') && isDirectTouchPointer)) && typeof element.releasePointerCapture === 'function') {
                try {
                    if (element.hasPointerCapture?.(event.pointerId)) {
                        element.releasePointerCapture(event.pointerId);
                    }
                } catch {
                }
            }
        } catch (error) {
            console.error('[canvasGestures] pointer failed', type, error?.message ?? error);
        }
    };

    const pointerHandler = (type) => (event) => invokePointer(type, event);

    const documentPointerHandler = (type) => (event) => {
        if (!activeDirectPointers.has(event.pointerId)) {
            return;
        }

        if (event.target === element || element.contains(event.target)) {
            return;
        }

        invokePointer(type, event);
    };

    const lostPointerCaptureHandler = (event) => {
        if (!activeDirectPointers.has(event.pointerId)) {
            return;
        }

        invokePointer('pointercancel', event);
    };

    const wheelHandler = (event) => {
        const offset = getOffset(element, event);
        try {
            const policy = dotNetRef.invokeMethod('OnCanvasWheel', {
                offsetX: offset.x,
                offsetY: offset.y,
                deltaY: event.deltaY ?? 0,
                buttons: event.buttons ?? 0
            });

            if ((policy & POLICY_PREVENT_DEFAULT) !== 0) {
                suppressBrowserDefault(event);
            }
        } catch (error) {
            console.error('[canvasGestures] wheel failed', error?.message ?? error);
        }
    };

    const suppressBrowserFallbackHandler = (event) => {
        suppressBrowserDefault(event);
    };

    // right click / long press / Menu key: .NET decides (TouchActionResult.ContextMenu, args.Handled) whether the
    // browser menu is suppressed; the sync call exists on WebAssembly only — on the server the menu stays suppressed
    const contextMenuHandler = (event) => {
        const offset = getOffset(element, event);
        try {
            const policy = dotNetRef.invokeMethod('OnCanvasContextMenu', {
                offsetX: offset.x,
                offsetY: offset.y,
                pointerType: event.pointerType ?? 'mouse'
            });
            if ((policy & POLICY_PREVENT_DEFAULT) !== 0) {
                suppressBrowserDefault(event);
            }
        } catch {
            suppressBrowserDefault(event);
            dotNetRef.invokeMethodAsync?.('OnCanvasContextMenu', { offsetX: offset.x, offsetY: offset.y, pointerType: event.pointerType ?? 'mouse' }).catch(() => { });
        }
    };

    const handlers = {
        pointerdown: pointerHandler('pointerdown'),
        pointermove: pointerHandler('pointermove'),
        pointerup: pointerHandler('pointerup'),
        pointercancel: pointerHandler('pointercancel'),
        pointerleave: pointerHandler('pointerleave'),
        lostpointercapture: lostPointerCaptureHandler,
        wheel: wheelHandler,
        contextmenu: contextMenuHandler,
        selectstart: suppressBrowserFallbackHandler,
        dragstart: suppressBrowserFallbackHandler
    };

    if (lockTouches) {
        // In-app browsers (iOS WKWebView in Telegram etc.) intercept swipes at the native
        // touch layer before pointer events can respond, dismissing the browser window.
        // touchmove only — touchstart.preventDefault() can kill pointer events on Windows touch.
        handlers.touchmove = (event) => { if (event.cancelable) event.preventDefault(); };
    }

    const documentHandlers = {
        pointermove: documentPointerHandler('pointermove'),
        pointerup: documentPointerHandler('pointerup'),
        pointercancel: documentPointerHandler('pointercancel')
    };

    for (const [name, handler] of Object.entries(handlers)) {
        element.addEventListener(name, handler, { passive: false });
    }

    for (const [name, handler] of Object.entries(documentHandlers)) {
        document.addEventListener(name, handler, { passive: false, capture: true });
    }

    element.__drawnUiGestures = { handlers, documentHandlers, dotNetRef };
}

export function detachCanvasGestures(element) {
    detachInternal(element);
}
