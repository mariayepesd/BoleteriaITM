namespace BoleteriaITM
{
    public partial class App : Application
    {
        private readonly IServiceProvider _services;

        public App(IServiceProvider services)
        {
            InitializeComponent();
            _services = services;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // AppShell se resuelve desde DI, que a su vez inyecta MainPage
            return new Window(_services.GetRequiredService<AppShell>());
        }
    }
}
