using System;
using System.Windows.Forms;

namespace kursovaya
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // ѕоказать форму входа Ч пока не успешно, приложение не запуститс€
            using (var login = new LoginForm())
            {
                var res = login.ShowDialog();
                if (res != DialogResult.OK)
                {
                    // ѕользователь отменил вход Ч выходим
                    return;
                }
            }

            // ”спешный вход Ч запускаем основную форму
            Application.Run(new Form1());
        }
    }
}