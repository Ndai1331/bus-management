(() => {
    const root = document.querySelector('.hcs-auth-login-page');
    if (!root) {
        return;
    }

    const password = document.getElementById('LoginInput_Password');
    const passwordToggle = document.getElementById('PasswordVisibilityButton');

    if (password && passwordToggle) {
        passwordToggle.addEventListener('click', () => {
            const isVisible = password.type === 'text';
            password.type = isVisible ? 'password' : 'text';
            passwordToggle.setAttribute('aria-pressed', String(!isVisible));
            passwordToggle.setAttribute(
                'aria-label',
                isVisible ? passwordToggle.dataset.showLabel : passwordToggle.dataset.hideLabel);

            const icon = passwordToggle.querySelector('i');
            icon?.classList.toggle('fa-eye-slash', isVisible);
            icon?.classList.toggle('fa-eye', !isVisible);
        });
    }

    root.querySelectorAll('form').forEach((form) => {
        form.addEventListener('submit', (event) => {
            if (form.dataset.submitting === 'true') {
                event.preventDefault();
                return;
            }

            form.dataset.submitting = 'true';
            form.setAttribute('aria-busy', 'true');

            const submitter = event.submitter;
            const loadingText = submitter?.dataset.loadingText;
            if (submitter && loadingText) {
                submitter.dataset.originalText = submitter.textContent || '';
                submitter.textContent = loadingText;
            }

            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach((button) => {
                button.disabled = true;
            });
        });
    });
})();
