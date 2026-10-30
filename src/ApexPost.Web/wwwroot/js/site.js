// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Print buttons (data-print-page). Wired here because the CSP blocks inline
// event handlers.
document.querySelectorAll("[data-print-page]").forEach(function (button) {
    button.addEventListener("click", function () { window.print(); });
});
