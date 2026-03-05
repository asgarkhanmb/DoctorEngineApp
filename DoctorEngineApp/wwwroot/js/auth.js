function togglePasswordVisibility() {
    const passwordInput = document.getElementById('password');
    const showPasswordCheckbox = document.getElementById('show-password');
    passwordInput.type = showPasswordCheckbox.checked ? 'text' : 'password';
}