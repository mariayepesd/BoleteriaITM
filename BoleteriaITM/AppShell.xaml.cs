namespace BoleteriaITM
{
    public partial class AppShell : Shell
    {
        public AppShell(MainPage mainPage)
        {
            InitializeComponent();

            // Sobreescribimos el DataTemplate del XAML con la instancia creada por DI
            var shellContent = Items.OfType<ShellContent>().FirstOrDefault();
            if (shellContent != null)
                shellContent.Content = mainPage;
        }
    }
}
