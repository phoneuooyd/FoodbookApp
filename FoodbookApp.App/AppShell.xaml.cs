using System.Linq;

namespace FoodbookApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
            // Ustawienie HomePage jako domyślnej zakładki przy starcie
            CurrentItem = Items.OfType<TabBar>().FirstOrDefault()?.Items[2]; // HomePage jest na pozycji 2 (indeks zaczyna się od 0)
        }

        protected override void OnNavigating(ShellNavigatingEventArgs args)
        {
            try
            {
                // Sprawdź czy użytkownik klika na zakładkę Home
                if (args.Target?.Location?.OriginalString?.Contains("HomePage") == true || 
                    args.Target?.Location?.OriginalString?.Contains("//Home") == true)
                {
                    // Jeśli jesteśmy już na stronie Home, nie rób nic
                    if (Current.CurrentState?.Location?.OriginalString?.Contains("HomePage") == true)
                    {
                        args.Cancel();
                        return;
                    }

                    // Przejdź bezpośrednio do HomePage, resetując stos nawigacji
                    args.Cancel();
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await GoToAsync("//HomePage", true);
                    });
                    
                    return; // Anuluj normalną nawigację
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnNavigating: {ex.Message}");
            }
            
            base.OnNavigating(args);
        }
    }
}
