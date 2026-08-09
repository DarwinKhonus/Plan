using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Plan.Data;
using Plan.Data.Domain;
using Plan.Mvvm;
using Plan.Services;

namespace Plan.ViewModels;

public class MainViewModel : ObservableObject
{
    /// <summary>Kolik dnů se v ose zobrazí před první a za poslední zakázkou.</summary>
    private const int RezervaDnu = 14;

    private const int MinimalniRozsahDnu = 60;

    private readonly ZakazkyRepository _zakazkyRepository;
    private readonly NastaveniRepository _nastaveniRepository;
    private readonly UpdateChecker _updateChecker;

    private PracovniKalendar _kalendar = new(new PracovniNastaveni());
    private ZakazkaViewModel? _vybranaZakazka;
    private DateOnly _prvniDen = DateOnly.FromDateTime(DateTime.Today);
    private int _pocetDnu = MinimalniRozsahDnu;
    private double _sirkaDne = 26;
    private string? _informaceOAktualizaci;
    private string? _urlAktualizace;

    /// <summary>Tlumí přepočty během hromadných změn kolekce (načtení, přeřazení).</summary>
    private bool _hromadnaZmena;

    public MainViewModel(
        ZakazkyRepository zakazkyRepository,
        NastaveniRepository nastaveniRepository,
        UpdateChecker updateChecker)
    {
        _zakazkyRepository = zakazkyRepository;
        _nastaveniRepository = nastaveniRepository;
        _updateChecker = updateChecker;

        InfoCommand = new RelayCommand(
            () => PozadavekNaInfo?.Invoke(this, EventArgs.Empty),
            () => VybranaZakazka is not null);
        PridatCommand = new RelayCommand(() => PozadavekNaPridani?.Invoke(this, EventArgs.Empty));
        UpravitCommand = new RelayCommand(
            () => PozadavekNaUpravu?.Invoke(this, EventArgs.Empty),
            () => VybranaZakazka is not null);
        SmazatCommand = new RelayCommand(
            () => PozadavekNaSmazani?.Invoke(this, EventArgs.Empty),
            () => VybranaZakazka is not null);
        NastaveniCommand = new RelayCommand(() => PozadavekNaNastaveni?.Invoke(this, EventArgs.Empty));
        PriblizitCommand = new RelayCommand(() => SirkaDne = Math.Min(SirkaDne + 6, 60));
        OddalitCommand = new RelayCommand(() => SirkaDne = Math.Max(SirkaDne - 6, 8));
        SkocitNaDnesekCommand = new RelayCommand(() => PozadavekNaSkokNaDnesek?.Invoke(this, EventArgs.Empty));

        Zakazky.CollectionChanged += (_, _) =>
        {
            if (!_hromadnaZmena)
            {
                PrepocitejVse();
            }
        };
    }

    /// <summary>
    /// Události, na které reaguje okno otevřením dialogu. ViewModel tak nemusí znát
    /// žádný WPF typ a zůstává testovatelný.
    /// </summary>
    public event EventHandler? PozadavekNaInfo;

    public event EventHandler? PozadavekNaPridani;

    public event EventHandler? PozadavekNaUpravu;

    public event EventHandler? PozadavekNaSmazani;

    public event EventHandler? PozadavekNaNastaveni;

    public event EventHandler? PozadavekNaSkokNaDnesek;

    public ObservableCollection<ZakazkaViewModel> Zakazky { get; } = [];

    public ICommand InfoCommand { get; }

    public ICommand PridatCommand { get; }

    public ICommand UpravitCommand { get; }

    public ICommand SmazatCommand { get; }

    public ICommand NastaveniCommand { get; }

    public ICommand PriblizitCommand { get; }

    public ICommand OddalitCommand { get; }

    public ICommand SkocitNaDnesekCommand { get; }

    public PracovniKalendar Kalendar
    {
        get => _kalendar;
        private set => SetProperty(ref _kalendar, value);
    }

    public ZakazkaViewModel? VybranaZakazka
    {
        get => _vybranaZakazka;
        set => SetProperty(ref _vybranaZakazka, value);
    }

    public DateOnly PrvniDen
    {
        get => _prvniDen;
        private set => SetProperty(ref _prvniDen, value);
    }

    public int PocetDnu
    {
        get => _pocetDnu;
        private set => SetProperty(ref _pocetDnu, value);
    }

    public double SirkaDne
    {
        get => _sirkaDne;
        set => SetProperty(ref _sirkaDne, value);
    }

    public string? InformaceOAktualizaci
    {
        get => _informaceOAktualizaci;
        private set
        {
            if (SetProperty(ref _informaceOAktualizaci, value))
            {
                OnPropertyChanged(nameof(JeDostupnaAktualizace));
            }
        }
    }

    public string? UrlAktualizace
    {
        get => _urlAktualizace;
        private set => SetProperty(ref _urlAktualizace, value);
    }

    public bool JeDostupnaAktualizace => !string.IsNullOrEmpty(_informaceOAktualizaci);

    public string SouhrnStavu
    {
        get
        {
            var pocetKolizi = Zakazky.Count(z => z.MaKolizi);
            var zakladni = $"{Zakazky.Count} {SklonujZakazky(Zakazky.Count)}";

            return pocetKolizi == 0
                ? $"{zakladni} · bez konfliktů"
                : $"{zakladni} · {pocetKolizi} v konfliktu";
        }
    }

    /// <summary>České skloňování: 1 zakázka, 2–4 zakázky, 0 a 5+ zakázek.</summary>
    private static string SklonujZakazky(int pocet) => pocet switch
    {
        1 => "zakázka",
        >= 2 and <= 4 => "zakázky",
        _ => "zakázek",
    };

    public async Task NactiAsync()
    {
        Kalendar = new PracovniKalendar(await _nastaveniRepository.NactiAsync());

        var zakazky = await _zakazkyRepository.NactiVseAsync();
        var vybraneId = VybranaZakazka?.Id;

        _hromadnaZmena = true;
        try
        {
            foreach (var zakazka in Zakazky)
            {
                zakazka.PropertyChanged -= NaZmenuZakazky;
            }

            Zakazky.Clear();
            foreach (var zakazka in zakazky)
            {
                var vm = new ZakazkaViewModel(zakazka);
                vm.PropertyChanged += NaZmenuZakazky;
                Zakazky.Add(vm);
            }
        }
        finally
        {
            _hromadnaZmena = false;
        }

        // Znovunačtení vymění instance ViewModelů, takže výběr je potřeba obnovit podle Id.
        VybranaZakazka = vybraneId is null ? null : Zakazky.FirstOrDefault(z => z.Id == vybraneId);

        PrepocitejVse();
    }

    public async Task PridejAsync(ZakazkaEditViewModel editace)
    {
        await _zakazkyRepository.PridejAsync(editace.Nazev.Trim(), editace.VyslednyDatumOd, editace.VyslednyDatumDo);
        await NactiAsync();
    }

    public async Task UpravAsync(int id, ZakazkaEditViewModel editace)
    {
        await _zakazkyRepository.UpravAsync(id, editace.Nazev.Trim(), editace.VyslednyDatumOd, editace.VyslednyDatumDo);
        await NactiAsync();
    }

    public async Task SmazAsync(int id)
    {
        await _zakazkyRepository.SmazAsync(id);
        await NactiAsync();
    }

    /// <summary>Uloží termín posunutý tažením na časové ose.</summary>
    public async Task UlozPosunutyTerminAsync(ZakazkaViewModel zakazka)
    {
        await _zakazkyRepository.UlozTerminAsync(zakazka.Id, zakazka.DatumOd, zakazka.DatumDo);
        SeradZakazky();
        PrepocitejRozsahOsy();
    }

    public async Task UlozNastaveniAsync(PracovniNastaveni nastaveni)
    {
        await _nastaveniRepository.UlozAsync(nastaveni);
        Kalendar = new PracovniKalendar(nastaveni);

        // Kolize závisí na pracovních dnech, takže změna nastavení je musí přepočítat taky.
        PrepocitejKolize();
        PrepocitejHodiny();
    }

    /// <summary>
    /// Kontrola aktualizací na pozadí. Nikdy nevyhazuje výjimku a nikdy neblokuje start —
    /// když se nepovede, uživatel prostě jen neuvidí notifikaci.
    /// </summary>
    public async Task ZkontrolujAktualizaceAsync()
    {
        var aktualizace = await _updateChecker.ZkontrolujAsync();
        if (aktualizace is null)
        {
            return;
        }

        UrlAktualizace = aktualizace.StrankaReleaseUrl;
        InformaceOAktualizaci = $"K dispozici je novější verze {aktualizace.Verze}.";
    }

    private void NaZmenuZakazky(object? sender, PropertyChangedEventArgs e)
    {
        // Přepočet při každé změně termínu, tedy i průběžně během tažení pruhu.
        if (e.PropertyName is nameof(ZakazkaViewModel.DatumOd) or nameof(ZakazkaViewModel.DatumDo))
        {
            PrepocitejKolize();
            PrepocitejHodiny();
        }
    }

    private void PrepocitejVse()
    {
        PrepocitejKolize();
        PrepocitejHodiny();
        PrepocitejRozsahOsy();
    }

    private void PrepocitejKolize()
    {
        // S kalendářem, aby překryv jen přes víkend nebo svátek nehlásil konflikt.
        var kolidujici = KolizeDetektor.NajdiKolidujici(Zakazky.Select(z => z.ToEntity()), Kalendar);

        foreach (var zakazka in Zakazky)
        {
            zakazka.MaKolizi = kolidujici.Contains(zakazka.Id);
        }

        OnPropertyChanged(nameof(SouhrnStavu));
    }

    private void PrepocitejHodiny()
    {
        foreach (var zakazka in Zakazky)
        {
            zakazka.OdhadHodin = Kalendar.OdhadHodin(zakazka.DatumOd, zakazka.DatumDo);
        }
    }

    private void PrepocitejRozsahOsy()
    {
        var dnes = DateOnly.FromDateTime(DateTime.Today);

        var zacatek = Zakazky.Count > 0 ? Zakazky.Min(z => z.DatumOd) : dnes;
        var konec = Zakazky.Count > 0 ? Zakazky.Max(z => z.DatumDo) : dnes;

        // Dnešek musí být v ose vidět i tehdy, když jsou všechny zakázky v budoucnu nebo minulosti.
        if (dnes < zacatek)
        {
            zacatek = dnes;
        }

        if (dnes > konec)
        {
            konec = dnes;
        }

        PrvniDen = zacatek.AddDays(-RezervaDnu);
        PocetDnu = Math.Max(konec.DayNumber - PrvniDen.DayNumber + 1 + RezervaDnu, MinimalniRozsahDnu);
    }

    private void SeradZakazky()
    {
        var serazene = Zakazky.OrderBy(z => z.DatumOd).ThenBy(z => z.DatumDo).ToList();

        _hromadnaZmena = true;
        try
        {
            for (var cilovyIndex = 0; cilovyIndex < serazene.Count; cilovyIndex++)
            {
                var aktualniIndex = Zakazky.IndexOf(serazene[cilovyIndex]);
                if (aktualniIndex != cilovyIndex)
                {
                    Zakazky.Move(aktualniIndex, cilovyIndex);
                }
            }
        }
        finally
        {
            _hromadnaZmena = false;
        }
    }
}
