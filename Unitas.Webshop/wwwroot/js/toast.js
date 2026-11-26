const toast = document.querySelector("[data-toast]");
const close = toast.querySelector("[data-toast-close]");
const announcement = document.querySelector("[data-toast-announcement]");
const duration = Number(toast.dataset.toastDuration);
let timer;

function stopTimer() {
    window.clearTimeout(timer);
}

function dismissToast() {
    stopTimer();
    toast.remove();
    announcement.remove();
}

function startTimer() {
    stopTimer();
    if (duration > 0 && toast.isConnected && !toast.matches(":hover, :focus-within")) {
        timer = window.setTimeout(dismissToast, duration);
    }
}

close.hidden = false;
close.addEventListener("click", dismissToast);
toast.addEventListener("pointerenter", stopTimer);
toast.addEventListener("focusin", stopTimer);
toast.addEventListener("pointerleave", () => window.requestAnimationFrame(startTimer));
toast.addEventListener("focusout", () => window.requestAnimationFrame(startTimer));
window.setTimeout(() => {
    announcement.textContent = toast.querySelector("[data-toast-message]").textContent;
}, 80);
startTimer();
