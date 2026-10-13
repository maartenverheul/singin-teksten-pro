window.reviewGuard = {
    enabled: false,

    setEnabled(enabled) {
        this.enabled = enabled;
    }
};

window.addEventListener("beforeunload", (event) => {
    if (!window.reviewGuard.enabled) {
        return;
    }

    event.preventDefault();
    event.returnValue = "";
});
