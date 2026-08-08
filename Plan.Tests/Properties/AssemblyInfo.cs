// Testovací projekt netvoří žádné WPF okno, ale odkazuje na projekt s UseWPF.
// Bez tohohle atributu by build spadl na chybějící definici tématu.
using System.Windows;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.None)]
