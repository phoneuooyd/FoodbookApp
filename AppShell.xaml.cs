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
    }
}
