using System;
using System.Windows.Forms;

namespace kursovaya
{
    public partial class LoginForm : Form
    {
        public LoginForm()
        {
            InitializeComponent();
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            // фокус на логине
            tbUsername.Focus();
        }

        private void tbUsername_TextChanged(object sender, EventArgs e) { }

        private void lblUsername_Click(object sender, EventArgs e) { }

        private void tbPassword_TextChanged(object sender, EventArgs e) { }

        private void lblPassword_Click(object sender, EventArgs e) { }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            // Валидация полей
            var user = tbUsername.Text.Trim();
            var pwd = tbPassword.Text ?? string.Empty;

            if (string.IsNullOrEmpty(user))
            {
                MessageBox.Show(this, "Введите логин.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbUsername.Focus();
                return;
            }

            if (string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show(this, "Введите пароль.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tbPassword.Focus();
                return;
            }

            // Блокируем кнопку, чтобы предотвратить множественные клики
            btnLogin.Enabled = false;
            btnCancel.Enabled = false;

            try
            {
                // Синхронный вызов достаточно быстрый — можно оставить
                if (AuthService.Authenticate(AppSession.DbPath, user, pwd, out var role))
                {
                    // Сохраняю данные сессии
                    AppSession.CurrentUsername = user;
                    AppSession.CurrentUserRole = role ?? string.Empty;

                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                else
                {
                    MessageBox.Show(this, "Неверный логин или пароль.", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при проверке учётных данных:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLogin.Enabled = true;
                btnCancel.Enabled = true;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
