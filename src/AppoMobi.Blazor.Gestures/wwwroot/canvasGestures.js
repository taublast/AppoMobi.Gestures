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

    for (const [name, handler] of Object.entries(state.handlers)) {
        element.removeEventListener(name, handler);
    }

    for (const [name, handler] of Object.entries(state.documentHandlers ?? {})) {
        document.removeEventListener(name, handler, true);
    }

    delete element.__drawnUiGestures;
}

export function attachCanvasGestures(element, dotNetRef, enabled) {
    detachInternal(element);

    if (!enabled) {
        return;
    }

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

    const handlers = {
        pointerdown: pointerHandler('pointerdown'),
        pointermove: pointerHandler('pointermove'),
        pointerup: pointerHandler('pointerup'),
        pointercancel: pointerHandler('pointercancel'),
        pointerleave: pointerHandler('pointerleave'),
        lostpointercapture: lostPointerCaptureHandler,
        wheel: wheelHandler,
        contextmenu: suppressBrowserFallbackHandler,
        selectstart: suppressBrowserFallbackHandler,
        dragstart: suppressBrowserFallbackHandler
    };

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

    element.__drawnUiGestures = { handlers, documentHandlers };
}

export function detachCanvasGestures(element) {
    detachInternal(element);
}
