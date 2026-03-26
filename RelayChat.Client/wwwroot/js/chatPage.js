let composer = null;
let dotNetRef = null;
let documentKeydownHandler = null;
let composerKeydownHandler = null;
let registeredComposerId = null;

function isEditableTarget(target) {
    if (!target) {
        return false;
    }

    const tagName = target.tagName?.toLowerCase();
    return tagName === "input" ||
        tagName === "textarea" ||
        target.isContentEditable;
}

function dispatchInput(element) {
    element.dispatchEvent(new Event("input", { bubbles: true }));
}

function insertText(element, text) {
    const start = element.selectionStart ?? element.value.length;
    const end = element.selectionEnd ?? element.value.length;
    element.setRangeText(text, start, end, "end");
    dispatchInput(element);
}

export function registerComposer(dotNetObjectReference, composerId) {
    unregisterComposer();

    composer = document.getElementById(composerId);
    dotNetRef = dotNetObjectReference;
    registeredComposerId = composerId;
    if (!composer || !dotNetRef) {
        return;
    }

    documentKeydownHandler = event => {
        if (!composer || event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) {
            return;
        }

        if (document.activeElement === composer || isEditableTarget(event.target)) {
            return;
        }

        if (event.key.length === 1) {
            event.preventDefault();
            composer.focus();
            insertText(composer, event.key);
            return;
        }

        if (event.key === "Backspace") {
            event.preventDefault();
            composer.focus();
        }
    };

    composerKeydownHandler = event => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            dotNetRef.invokeMethodAsync("HandleComposerSubmit");
        }
    };

    document.addEventListener("keydown", documentKeydownHandler);
    composer.addEventListener("keydown", composerKeydownHandler);
}

export function unregisterComposer() {
    if (composer && composerKeydownHandler) {
        composer.removeEventListener("keydown", composerKeydownHandler);
    }

    if (documentKeydownHandler) {
        document.removeEventListener("keydown", documentKeydownHandler);
    }

    composer = null;
    dotNetRef = null;
    documentKeydownHandler = null;
    composerKeydownHandler = null;
    registeredComposerId = null;
}

export function scrollToBottom(elementId) {
    const element = document.getElementById(elementId);
    if (!element) {
        return;
    }

    element.scrollTop = element.scrollHeight;
}

export function focusComposer() {
    if (!composer && registeredComposerId) {
        composer = document.getElementById(registeredComposerId);
    }

    composer?.focus();
}
