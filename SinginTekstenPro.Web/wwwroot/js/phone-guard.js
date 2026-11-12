// Phones get a notice instead of the app. Blazor is started manually (autostart="false"),
// so on phones the .NET runtime and app assemblies are never downloaded.
window.isPhone = (() => {
    if (navigator.userAgentData && typeof navigator.userAgentData.mobile === "boolean") {
        return navigator.userAgentData.mobile;
    }

    // "Mobi" covers iPhone, Android phones (Android tablets omit it) and most other phones.
    if (/Mobi|iPhone|iPod|Windows Phone/i.test(navigator.userAgent)) {
        return true;
    }

    // Fallback: touch-only device with a small screen.
    const coarse = window.matchMedia("(pointer: coarse)").matches
        && !window.matchMedia("(any-pointer: fine)").matches;
    return coarse && Math.min(screen.width, screen.height) < 600;
})();

window.startApp = () => {
    if (!window.isPhone) {
        Blazor.start();
        return;
    }

    document.documentElement.classList.add("phone");
    document.getElementById("app").innerHTML = `
        <main class="phone-notice">
            <svg class="icon phone-notice-icon" viewBox="0 0 24 24" aria-hidden="true">
                <rect x="2" y="4" width="20" height="13" rx="2" fill="none" stroke="currentColor" stroke-width="1.8" />
                <path d="M8 21h8M12 17v4" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            </svg>
            <h1>Singin Teksten Pro</h1>
            <p>This app is made for computers and is not available on smartphones.</p>
            <p class="phone-notice-hint">Open this page on a laptop, desktop or tablet to get started.</p>
        </main>`;
};
