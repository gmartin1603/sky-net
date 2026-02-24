(function () {
    window.skynetLayoutEditor = {
        getElementClientRect: function (element) {
            if (!element || !element.getBoundingClientRect) {
                return null;
            }

            const rect = element.getBoundingClientRect();
            return {
                left: rect.left,
                top: rect.top,
                width: rect.width,
                height: rect.height
            };
        }
    };
})();
