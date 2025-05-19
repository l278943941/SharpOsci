using System.Diagnostics;

namespace SharpOsci
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
            Application.Run(new Form1());
        }

        private static void HandleException(Exception ex)
        {
            if (ex != null)
            {
                Debug.WriteLine($"δ������쳣: {ex}");
                MessageBox.Show($"��������: {ex.Message}", "����", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}