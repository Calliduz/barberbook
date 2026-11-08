/**
 * BarberBook - Enterprise Micro-Interactions, Convenience & Accessibility
 * Core Script for Theme Toggle, Copy to Clipboard, Password Toggle,
 * Double-Click Prevention, Scroll-to-Top, and Global Command Palette (Ctrl+K).
 */

(function () {
    'use strict';

    // 1. THEME / DARK MODE MANAGEMENT
    window.initTheme = function () {
        const storedTheme = localStorage.getItem('barberbook_theme');
        const systemPrefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        
        if (storedTheme === 'dark' || (!storedTheme && systemPrefersDark)) {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
        updateThemeIcons();
    };

    window.toggleDarkMode = function () {
        const isDark = document.documentElement.classList.contains('dark');
        if (isDark) {
            document.documentElement.classList.remove('dark');
            localStorage.setItem('barberbook_theme', 'light');
        } else {
            document.documentElement.classList.add('dark');
            localStorage.setItem('barberbook_theme', 'dark');
        }
        updateThemeIcons();
        window.dispatchEvent(new CustomEvent('barberbook-theme-change', { detail: { isDark: !isDark } }));
    };

    function updateThemeIcons() {
        const isDark = document.documentElement.classList.contains('dark');
        document.querySelectorAll('.theme-toggle-sun').forEach(el => {
            el.classList.toggle('hidden', !isDark);
        });
        document.querySelectorAll('.theme-toggle-moon').forEach(el => {
            el.classList.toggle('hidden', isDark);
        });
    }

    // Initialize theme immediately to prevent FOUC
    initTheme();

    // 2. COPY TO CLIPBOARD CONVENIENCE
    window.copyToClipboard = function (text, triggerEl) {
        if (!text) return;

        function showSuccess() {
            if (!triggerEl) return;
            const originalTitle = triggerEl.getAttribute('title') || '';
            const existingTooltip = triggerEl.querySelector('.copy-tooltip');
            if (existingTooltip) existingTooltip.remove();

            const tooltip = document.createElement('span');
            tooltip.className = 'copy-tooltip show';
            tooltip.textContent = 'Copied!';
            triggerEl.appendChild(tooltip);

            // Subtle tactile pulse
            triggerEl.classList.add('text-emerald-500', 'scale-110');
            setTimeout(() => {
                tooltip.classList.remove('show');
                setTimeout(() => tooltip.remove(), 200);
                triggerEl.classList.remove('text-emerald-500', 'scale-110');
                if (originalTitle) triggerEl.setAttribute('title', originalTitle);
            }, 1800);
        }

        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(showSuccess).catch(() => fallbackCopy(text, showSuccess));
        } else {
            fallbackCopy(text, showSuccess);
        }
    };

    function fallbackCopy(text, callback) {
        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.position = 'fixed';
        textArea.style.left = '-999999px';
        textArea.style.top = '-999999px';
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        try {
            document.execCommand('copy');
            if (callback) callback();
        } catch (err) {
            console.error('Copy fallback failed:', err);
        }
        document.body.removeChild(textArea);
    }

    // 3. PASSWORD VISIBILITY TOGGLE
    window.togglePasswordVisibility = function (inputId, btnEl) {
        const input = document.getElementById(inputId);
        if (!input) return;

        const isPassword = input.type === 'password';
        input.type = isPassword ? 'text' : 'password';

        if (btnEl) {
            const eyeIcon = btnEl.querySelector('.icon-eye');
            const eyeSlashIcon = btnEl.querySelector('.icon-eye-slash');
            if (eyeIcon && eyeSlashIcon) {
                eyeIcon.classList.toggle('hidden', isPassword);
                eyeSlashIcon.classList.toggle('hidden', !isPassword);
            }
            btnEl.setAttribute('aria-label', isPassword ? 'Hide password' : 'Show password');
        }
    };

    // 4. DOUBLE-CLICK SUBMISSION PREVENTION & LOADING SPINNER
    window.initDoubleSubmitPrevention = function () {
        document.querySelectorAll('form').forEach(form => {
            if (form.dataset.preventDoubleSubmitBound) return;
            form.dataset.preventDoubleSubmitBound = 'true';

            form.addEventListener('submit', function (e) {
                if (form.dataset.submitting === 'true') {
                    e.preventDefault();
                    return false;
                }

                // If form uses HTML5 validation and is invalid, don't lock
                if (!form.checkValidity()) {
                    return;
                }

                const submitBtn = form.querySelector('button[type="submit"]');
                if (submitBtn && !submitBtn.dataset.noSpinner) {
                    form.dataset.submitting = 'true';
                    submitBtn.disabled = true;
                    submitBtn.classList.add('opacity-70', 'cursor-not-allowed');

                    // Prepend or show spinner
                    const spinner = document.createElement('span');
                    spinner.className = 'inline-block w-4 h-4 mr-2 border-2 border-current border-t-transparent rounded-full animate-spin align-middle';
                    spinner.setAttribute('role', 'status');
                    submitBtn.prepend(spinner);

                    // Safety timeout: unlock after 12s in case of validation/network error
                    setTimeout(() => {
                        form.dataset.submitting = 'false';
                        submitBtn.disabled = false;
                        submitBtn.classList.remove('opacity-70', 'cursor-not-allowed');
                        spinner.remove();
                    }, 12000);
                }
            });
        });
    };

    // 5. BACK TO TOP SCROLL
    window.initBackToTop = function () {
        let btn = document.getElementById('back-to-top-btn');
        if (!btn) {
            btn = document.createElement('button');
            btn.id = 'back-to-top-btn';
            btn.setAttribute('type', 'button');
            btn.setAttribute('aria-label', 'Back to top of page');
            btn.title = 'Back to top';
            btn.innerHTML = `<svg class="w-5 h-5" fill="none" stroke="currentColor" stroke-width="2.5" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M4.5 15.75l7.5-7.5 7.5 7.5" /></svg>`;
            document.body.appendChild(btn);
        }

        window.addEventListener('scroll', () => {
            if (window.scrollY > 300) {
                btn.classList.add('visible');
            } else {
                btn.classList.remove('visible');
            }
        }, { passive: true });

        btn.addEventListener('click', () => {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    };

    // 6. GLOBAL COMMAND PALETTE (Ctrl+K or /)
    window.initCommandPalette = function () {
        const palette = document.getElementById('global-command-palette');
        const input = document.getElementById('command-palette-input');
        if (!palette || !input) return;

        window.openCommandPalette = function () {
            palette.classList.remove('hidden');
            input.value = '';
            filterCommands('');
            setTimeout(() => input.focus(), 50);
        };

        window.closeCommandPalette = function () {
            palette.classList.add('hidden');
        };

        // Open shortcut
        document.addEventListener('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
                e.preventDefault();
                if (palette.classList.contains('hidden')) {
                    openCommandPalette();
                } else {
                    closeCommandPalette();
                }
            } else if (e.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement.tagName)) {
                e.preventDefault();
                openCommandPalette();
            } else if (e.key === 'Escape' && !palette.classList.contains('hidden')) {
                closeCommandPalette();
            }
        });

        // Click backdrop to close
        palette.addEventListener('click', function (e) {
            if (e.target === palette) {
                closeCommandPalette();
            }
        });

        // Filter commands
        input.addEventListener('input', function () {
            filterCommands(input.value.trim().toLowerCase());
        });

        function filterCommands(query) {
            const items = palette.querySelectorAll('.command-item');
            let hasMatch = false;
            items.forEach(item => {
                const text = (item.dataset.keywords || item.textContent).toLowerCase();
                const match = !query || text.includes(query);
                item.classList.toggle('hidden', !match);
                if (match) hasMatch = true;
            });
            const noResults = document.getElementById('command-no-results');
            if (noResults) {
                noResults.classList.toggle('hidden', hasMatch);
            }
        }
    };

    // 6. TOP NAVIGATION CLICK-TO-PIN DROPDOWNS & ESCAPE HANDLER
    window.initNavDropdowns = function () {
        document.querySelectorAll('header nav .group > button').forEach(btn => {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                const group = btn.closest('.group');
                if (!group) return;

                const isAlreadyPinned = group.classList.contains('is-pinned');
                // Close any other open dropdowns
                document.querySelectorAll('header nav .group.is-pinned').forEach(g => {
                    if (g !== group) g.classList.remove('is-pinned');
                });

                if (isAlreadyPinned) {
                    group.classList.remove('is-pinned');
                } else {
                    group.classList.add('is-pinned');
                }
            });
        });

        // Close pinned dropdowns on outside click
        document.addEventListener('click', function (e) {
            if (!e.target.closest('header nav .group')) {
                document.querySelectorAll('header nav .group.is-pinned').forEach(g => g.classList.remove('is-pinned'));
            }
        });

        // Close on Escape key
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                document.querySelectorAll('header nav .group.is-pinned').forEach(g => g.classList.remove('is-pinned'));
            }
        });
    };

    // DOM Ready Initialization
    document.addEventListener('DOMContentLoaded', () => {
        initTheme();
        initDoubleSubmitPrevention();
        initBackToTop();
        initCommandPalette();
        initNavDropdowns();
    });
})();
