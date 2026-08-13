namespace WebTools.NET.Internal;

internal static class StealthScripts
{
    internal const string Minimal =
        "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });";

    internal static readonly string[] FullLines =
        [
            // Overwrite the `navigator.webdriver` property to undefined
            "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });",

            // Remove Chrome automation extensions
            "window.chrome = { runtime: {}, csi: function() {}, loadTimes: function() {} };",

            // Override permissions
            "const originalQuery = window.navigator.permissions.query;",
            "window.navigator.permissions.query = (parameters) => (",
            "    parameters.name === 'notifications' ?",
            "        Promise.resolve({ state: Notification.permission }) :",
            "        originalQuery(parameters)",
            ");",

            // WebGL vendor/renderer spoofing (common bot detection vector)
            "const getParameter = WebGLRenderingContext.prototype.getParameter;",
            "WebGLRenderingContext.prototype.getParameter = function(param) {",
            "    if (param === 37445) return 'Intel Inc.';",
            "    if (param === 37446) return 'Intel Iris Xe Graphics';",
            "    return getParameter.call(this, param);",
            "};",

            // Plugins spoofing - realistic plugin list
            "Object.defineProperty(navigator, 'plugins', {",
            "    get: () => {",
            "        const plugins = [];",
            "        const names = ['PDF Viewer', 'Chrome PDF Viewer', 'Chromium PDF Viewer',",
            "                       'Microsoft Edge PDF Viewer', 'WebKit built-in PDF',",
            "                       'Widevine Content Decryption Module', 'Widevine Content Decryption Module'];",
            "        for (let i = 0; i < names.length; i++) {",
            "            plugins.push({",
            "                name: names[i],",
            "                filename: names[i].replace(/ /g, '_') + '.plugin'",
            "            });",
            "        }",
            "        return plugins;",
            "    }",
            "});",

            // Languages and hardware concurrency
            "Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });",
            "Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8 });",
            "Object.defineProperty(navigator, 'deviceMemory', { get: () => 8 });",
            "Object.defineProperty(navigator, 'maxTouchPoints', { get: () => 0 });",

            // Screen properties
            "Object.defineProperty(screen, 'colorDepth', { get: () => 24 });",
            "Object.defineProperty(screen, 'pixelDepth', { get: () => 24 });",

            // Override toString/functions to avoid detection
            "const originalToString = Function.prototype.toString;",
            "Function.prototype.toString = function() {",
            "    if (this === navigator.permissions.query) {",
            "        return 'function query() { [native code] }';",
            "    }",
            "    return originalToString.call(this);",
            "};"
        ];

    internal static string ForMode(bool headless) =>
        headless ? string.Join("\n", FullLines) : Minimal;
}
