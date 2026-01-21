(function () {
    const storageKey = "skynet-theme";

    function getPreferredTheme() {
        const saved = localStorage.getItem(storageKey);
        if (saved === "dark" || saved === "light") {
            return saved;
        }

        const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
        return prefersDark ? "dark" : "light";
    }

    function applyTheme(theme) {
        const value = theme === "dark" ? "dark" : "light";
        document.documentElement.dataset.theme = value;
        return value;
    }

    window.skynetTheme = {
        init: function () {
            applyTheme(getPreferredTheme());
        },
        get: function () {
            return document.documentElement.dataset.theme || null;
        },
        set: function (theme) {
            const applied = applyTheme(theme);
            localStorage.setItem(storageKey, applied);
            return applied;
        },
        toggle: function () {
            const current = window.skynetTheme.get() || getPreferredTheme();
            const next = current === "dark" ? "light" : "dark";
            return window.skynetTheme.set(next);
        },
        clear: function () {
            localStorage.removeItem(storageKey);
            delete document.documentElement.dataset.theme;
        }
    };
})();
