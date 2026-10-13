(() => {
    const saved = localStorage.getItem("theme");
    const theme = saved || (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
    document.documentElement.dataset.theme = theme;

    window.theme = {
        current() {
            return document.documentElement.dataset.theme || "light";
        },
        toggle() {
            const next = this.current() === "dark" ? "light" : "dark";
            document.documentElement.dataset.theme = next;
            localStorage.setItem("theme", next);
            return next;
        },
    };
})();
