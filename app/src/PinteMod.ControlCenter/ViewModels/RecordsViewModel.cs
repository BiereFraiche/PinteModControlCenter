using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class RecordsViewModel : PageViewModel
{
    private readonly IControlCenterSnapshotStore _snapshotStore;
    private readonly List<RecordItemViewModel> _allRecords = [];
    private IReadOnlyList<RecordItemViewModel> _records = Array.Empty<RecordItemViewModel>();
    private bool _suppressRecordFilterRefresh;
    private ServerState? _server;
    private RankRecordsSnapshot _rankRecords = RankRecordsSnapshot.Simulation;
    private EasterEggRecordsSnapshot _easterEggRecords = EasterEggRecordsSnapshot.Simulation;
    private bool _isHybridLocal;
    private SelectionOption _selectedRecordType;
    private SelectionOption _selectedRecordMap;
    private SelectionOption _selectedRecordPlayerCount;
    private SelectionOption _selectedRecordHolder;
    private SelectionOption _selectedRecordSort;
    private SelectionOption _selectedRecordPageSize;
    private int _matchingRecordCount;

    public RecordsViewModel(IControlCenterSnapshotStore snapshotStore)
        : base("Records", "Profils Ranks, records de manches et Easter Egg Records")
    {
        _snapshotStore = snapshotStore;

        RecordTypeOptions =
        [
            new SelectionOption("all", "Tous les types"),
            new SelectionOption("round", "Records de manches"),
            new SelectionOption("ee", "Easter Egg")
        ];
        RecordSortOptions =
        [
            new SelectionOption("source", "Ordre des records"),
            new SelectionOption("ranking", "Classement"),
            new SelectionOption("map", "Carte A → Z"),
            new SelectionOption("holder", "Joueur A → Z"),
            new SelectionOption("duration", "Durée la plus rapide")
        ];
        RecordPageSizeOptions =
        [
            new SelectionOption("50", "50 records"),
            new SelectionOption("100", "100 records"),
            new SelectionOption("200", "200 records"),
            new SelectionOption("all", "Tous · plus lourd")
        ];

        _selectedRecordType = RecordTypeOptions[0];
        _selectedRecordMap = RecordMapOptions[0];
        _selectedRecordPlayerCount = RecordPlayerCountOptions[0];
        _selectedRecordHolder = RecordHolderOptions[0];
        _selectedRecordSort = RecordSortOptions[0];
        _selectedRecordPageSize = RecordPageSizeOptions[0];
    }

    private IReadOnlyList<RankProfileItemViewModel> _rankProfiles = Array.Empty<RankProfileItemViewModel>();

    public IReadOnlyList<RankProfileItemViewModel> RankProfiles
    {
        get => _rankProfiles;
        private set => SetProperty(ref _rankProfiles, value);
    }

    public IReadOnlyList<RecordItemViewModel> Records
    {
        get => _records;
        private set => SetProperty(ref _records, value);
    }

    public IReadOnlyList<SelectionOption> RecordTypeOptions { get; }

    private IReadOnlyList<SelectionOption> _recordMapOptions = [new SelectionOption("all", "Toutes les cartes")];
    private IReadOnlyList<SelectionOption> _recordPlayerCountOptions = [new SelectionOption("all", "Tous les nombres")];
    private IReadOnlyList<SelectionOption> _recordHolderOptions = [new SelectionOption("all", "Tous les joueurs")];

    public IReadOnlyList<SelectionOption> RecordMapOptions
    {
        get => _recordMapOptions;
        private set => SetProperty(ref _recordMapOptions, value);
    }

    public IReadOnlyList<SelectionOption> RecordPlayerCountOptions
    {
        get => _recordPlayerCountOptions;
        private set => SetProperty(ref _recordPlayerCountOptions, value);
    }

    public IReadOnlyList<SelectionOption> RecordHolderOptions
    {
        get => _recordHolderOptions;
        private set => SetProperty(ref _recordHolderOptions, value);
    }

    public IReadOnlyList<SelectionOption> RecordSortOptions { get; }

    public IReadOnlyList<SelectionOption> RecordPageSizeOptions { get; }

    public SelectionOption SelectedRecordType
    {
        get => _selectedRecordType;
        set
        {
            if (value is not null && SetProperty(ref _selectedRecordType, value))
            {
                OnPropertyChanged(nameof(SelectedRecordTypeKey));
                if (!_suppressRecordFilterRefresh) ApplyRecordFilters();
            }
        }
    }

    public SelectionOption SelectedRecordMap
    {
        get => _selectedRecordMap;
        set
        {
            if (value is not null && SetProperty(ref _selectedRecordMap, value))
            {
                OnPropertyChanged(nameof(SelectedRecordMapKey));
                if (!_suppressRecordFilterRefresh) ApplyRecordFilters();
            }
        }
    }

    public SelectionOption SelectedRecordPlayerCount
    {
        get => _selectedRecordPlayerCount;
        set
        {
            if (value is not null && SetProperty(ref _selectedRecordPlayerCount, value))
            {
                OnPropertyChanged(nameof(SelectedRecordPlayerCountKey));
                if (!_suppressRecordFilterRefresh) ApplyRecordFilters();
            }
        }
    }

    public SelectionOption SelectedRecordHolder
    {
        get => _selectedRecordHolder;
        set
        {
            if (value is not null && SetProperty(ref _selectedRecordHolder, value))
            {
                OnPropertyChanged(nameof(SelectedRecordHolderKey));
                if (!_suppressRecordFilterRefresh) ApplyRecordFilters();
            }
        }
    }

    public SelectionOption SelectedRecordSort
    {
        get => _selectedRecordSort;
        set
        {
            if (value is not null && SetProperty(ref _selectedRecordSort, value))
            {
                OnPropertyChanged(nameof(SelectedRecordSortKey));
                if (!_suppressRecordFilterRefresh) ApplyRecordFilters();
            }
        }
    }

    public SelectionOption SelectedRecordPageSize
    {
        get => _selectedRecordPageSize;
        set
        {
            if (value is not null && SetProperty(ref _selectedRecordPageSize, value))
            {
                OnPropertyChanged(nameof(SelectedRecordPageSizeKey));
                if (!_suppressRecordFilterRefresh) ApplyRecordFilters();
            }
        }
    }

    public string SelectedRecordTypeKey
    {
        get => SelectedRecordType.Key;
        set => SelectByKey(RecordTypeOptions, value, option => SelectedRecordType = option);
    }

    public string SelectedRecordMapKey
    {
        get => SelectedRecordMap.Key;
        set => SelectByKey(RecordMapOptions, value, option => SelectedRecordMap = option);
    }

    public string SelectedRecordPlayerCountKey
    {
        get => SelectedRecordPlayerCount.Key;
        set => SelectByKey(RecordPlayerCountOptions, value, option => SelectedRecordPlayerCount = option);
    }

    public string SelectedRecordHolderKey
    {
        get => SelectedRecordHolder.Key;
        set => SelectByKey(RecordHolderOptions, value, option => SelectedRecordHolder = option, StringComparison.CurrentCultureIgnoreCase);
    }

    public string SelectedRecordSortKey
    {
        get => SelectedRecordSort.Key;
        set => SelectByKey(RecordSortOptions, value, option => SelectedRecordSort = option);
    }

    public string SelectedRecordPageSizeKey
    {
        get => SelectedRecordPageSize.Key;
        set => SelectByKey(RecordPageSizeOptions, value, option => SelectedRecordPageSize = option);
    }

    public ServerState? Server
    {
        get => _server;
        private set
        {
            if (SetProperty(ref _server, value))
            {
                OnPropertyChanged(nameof(RankedStatus));
                OnPropertyChanged(nameof(CurrentMapProfile));
            }
        }
    }

    public RankedStatus? RankedStatus => Server?.RankedStatus;

    public string RankedStatusCaption => _isHybridLocal
        ? Server?.RankedStatusAvailable == true ? "STATUT INFÉRÉ DES LOGS" : "STATUT NON DISPONIBLE"
        : "STATUT SIMULÉ";

    public string CurrentMapProfile => Server is null
        ? "CARTE INCONNUE"
        : $"{Server.MapName.ToUpperInvariant()} · {(_isHybridLocal ? "RECORDS LOCAUX" : "RECORDS SIMULÉS")}";

    public int RankProfileCount => RankProfiles.Count;

    public int StandardRecordCount => _allRecords.Count(record => !record.IsEasterEgg);

    public int EasterEggRecordCount => _allRecords.Count(record => record.IsEasterEgg);

    public int FilteredRecordCount => _matchingRecordCount;

    public int DisplayedRecordCount => Records.Count;

    public int TotalRecordCount => _allRecords.Count;

    public bool HasRankProfiles => RankProfiles.Count > 0;

    public bool HasRecords => Records.Count > 0;

    public string FilterSummary => TotalRecordCount == 0
        ? "Aucun record chargé"
        : DisplayedRecordCount == FilteredRecordCount
            ? $"{DisplayedRecordCount} / {TotalRecordCount} record(s) affiché(s)"
            : $"{DisplayedRecordCount} affiché(s) · {FilteredRecordCount} correspondant(s) / {TotalRecordCount} total";

    public string DataBadge => _isHybridLocal
        ? "LECTURE LOCALE READ-ONLY"
        : "DONNÉES SIMULÉES";

    public string SectionDescription => _isHybridLocal
        ? "Profils Ranks v2, records de manches v4 et Easter Egg Records officiels v2 locaux."
        : "Aperçu local simulé · aucun profil joueur ni record réel n’est lu.";

    public string RankSourceSummary => FormatSource(_rankRecords.ProfilesSource);

    public string RankSourceLabel => _rankRecords.ProfilesSource.SourceLabel;

    public string RoundRecordSourceSummary => FormatSource(_rankRecords.RoundRecordsSource);

    public string RoundRecordSourceLabel => _rankRecords.RoundRecordsSource.SourceLabel;

    public string EasterEggRecordSourceSummary => FormatSource(_easterEggRecords.Source);

    public string EasterEggRecordSourceLabel => _easterEggRecords.Source.SourceLabel;

    public string EasterEggKpiCaption => _isHybridLocal
        ? "OFFICIELS LOCAUX · TOP 5"
        : "SIMULÉS";

    public string RankFilesSummary => _isHybridLocal
        ? $"{_rankRecords.ProfileFilesScanned} fichier(s) examiné(s) · {_rankRecords.ProfileFilesSkipped} ignoré(s)"
        : "Profils déterministes du snapshot simulé";

    public string RecordFilesSummary => _isHybridLocal
        ? $"{_rankRecords.MapFilesScanned} carte(s) examinée(s) · {_rankRecords.MapFilesSkipped} fichier(s) et {_rankRecords.RecordSlotsSkipped} entrée(s) ignoré(s)"
        : "Records déterministes du snapshot simulé";

    public string EasterEggFilesSummary => _isHybridLocal
        ? $"{_easterEggRecords.OfficialProfileCount} profil(s) officiel(s) · {_easterEggRecords.MapFilesScanned} carte(s) examinée(s) · {_easterEggRecords.MapFilesSkipped} fichier(s) et {_easterEggRecords.RecordSlotsSkipped} entrée(s) ignoré(s)"
        : "Easter Egg Record déterministe du snapshot simulé";

    public string RankEmptyMessage => _isHybridLocal
        ? "Aucun profil local valide n’est disponible. Consultez l’état de lecture ci-dessus."
        : "Le snapshot simulé ne contient aucun profil Rank.";

    public string RecordsEmptyMessage => _allRecords.Count > 0
        ? "Aucun record ne correspond aux filtres sélectionnés."
        : _isHybridLocal
            ? "Aucun record local officiel valide n’est disponible."
            : "Le snapshot simulé ne contient aucun record.";

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = await _snapshotStore.GetSnapshotAsync(cancellationToken);
        _isHybridLocal = snapshot.DataContext.Mode == ControlCenterDataMode.HybridLocal;
        _rankRecords = snapshot.RankRecords;
        _easterEggRecords = snapshot.EasterEggRecords;
        Server = snapshot.Server;

        RankProfiles = snapshot.RankRecords.Profiles
            .Select(profile => new RankProfileItemViewModel(profile))
            .ToArray();

        _allRecords.Clear();
        foreach (var record in snapshot.Records)
        {
            _allRecords.Add(new RecordItemViewModel(record));
        }

        RebuildRecordFilterOptions();
        ApplyRecordFilters();

        OnPropertyChanged(nameof(CurrentMapProfile));
        OnPropertyChanged(nameof(RankedStatusCaption));
        OnPropertyChanged(nameof(RankProfileCount));
        OnPropertyChanged(nameof(StandardRecordCount));
        OnPropertyChanged(nameof(EasterEggRecordCount));
        OnPropertyChanged(nameof(HasRankProfiles));
        OnPropertyChanged(nameof(DataBadge));
        OnPropertyChanged(nameof(SectionDescription));
        OnPropertyChanged(nameof(RankSourceSummary));
        OnPropertyChanged(nameof(RankSourceLabel));
        OnPropertyChanged(nameof(RoundRecordSourceSummary));
        OnPropertyChanged(nameof(RoundRecordSourceLabel));
        OnPropertyChanged(nameof(EasterEggRecordSourceSummary));
        OnPropertyChanged(nameof(EasterEggRecordSourceLabel));
        OnPropertyChanged(nameof(EasterEggKpiCaption));
        OnPropertyChanged(nameof(RankFilesSummary));
        OnPropertyChanged(nameof(RecordFilesSummary));
        OnPropertyChanged(nameof(EasterEggFilesSummary));
        OnPropertyChanged(nameof(RankEmptyMessage));
    }

    private void RebuildRecordFilterOptions()
    {
        var selectedMap = SelectedRecordMap.Key;
        var selectedCount = SelectedRecordPlayerCount.Key;
        var selectedHolder = SelectedRecordHolder.Key;

        _suppressRecordFilterRefresh = true;
        try
        {
            RecordMapOptions =
            [
                new SelectionOption("all", "Toutes les cartes"),
                .. _allRecords
                    .GroupBy(record => record.MapCode, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(record => record.MapName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(record => new SelectionOption(record.MapCode, record.MapName))
            ];

            RecordPlayerCountOptions =
            [
                new SelectionOption("all", "Tous les nombres"),
                .. _allRecords
                    .Select(record => record.PlayerCount)
                    .Distinct()
                    .OrderBy(value => value)
                    .Select(count => new SelectionOption(count.ToString(), $"{count} joueur{(count > 1 ? "s" : string.Empty)}"))
            ];

            RecordHolderOptions =
            [
                new SelectionOption("all", "Tous les joueurs"),
                .. _allRecords
                    .SelectMany(record => record.HolderNames)
                    .Where(holder => !string.IsNullOrWhiteSpace(holder))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(holder => holder, StringComparer.CurrentCultureIgnoreCase)
                    .Select(holder => new SelectionOption(holder, holder))
            ];

            SelectedRecordMap = RecordMapOptions.FirstOrDefault(option =>
                string.Equals(option.Key, selectedMap, StringComparison.OrdinalIgnoreCase)) ?? RecordMapOptions[0];
            SelectedRecordPlayerCount = RecordPlayerCountOptions.FirstOrDefault(option =>
                string.Equals(option.Key, selectedCount, StringComparison.OrdinalIgnoreCase)) ?? RecordPlayerCountOptions[0];
            SelectedRecordHolder = RecordHolderOptions.FirstOrDefault(option =>
                string.Equals(option.Key, selectedHolder, StringComparison.CurrentCultureIgnoreCase)) ?? RecordHolderOptions[0];
        }
        finally
        {
            _suppressRecordFilterRefresh = false;
        }
    }

    private void ApplyRecordFilters()
    {
        IEnumerable<RecordItemViewModel> filtered = _allRecords;

        filtered = SelectedRecordType.Key switch
        {
            "round" => filtered.Where(record => !record.IsEasterEgg),
            "ee" => filtered.Where(record => record.IsEasterEgg),
            _ => filtered
        };

        if (!string.Equals(SelectedRecordMap.Key, "all", StringComparison.Ordinal))
        {
            filtered = filtered.Where(record =>
                string.Equals(record.MapCode, SelectedRecordMap.Key, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedRecordPlayerCount.Key, "all", StringComparison.Ordinal) &&
            int.TryParse(SelectedRecordPlayerCount.Key, out var playerCount))
        {
            filtered = filtered.Where(record => record.PlayerCount == playerCount);
        }

        if (!string.Equals(SelectedRecordHolder.Key, "all", StringComparison.Ordinal))
        {
            filtered = filtered.Where(record => record.HolderNames.Any(holder =>
                string.Equals(holder, SelectedRecordHolder.Key, StringComparison.CurrentCultureIgnoreCase)));
        }

        filtered = SelectedRecordSort.Key switch
        {
            "ranking" => filtered.OrderBy(record => record.PositionValue <= 0 ? int.MaxValue : record.PositionValue)
                                 .ThenBy(record => record.MapName, StringComparer.CurrentCultureIgnoreCase),
            "map" => filtered.OrderBy(record => record.MapName, StringComparer.CurrentCultureIgnoreCase)
                             .ThenBy(record => record.PlayerCount)
                             .ThenBy(record => record.PositionValue <= 0 ? int.MaxValue : record.PositionValue),
            "holder" => filtered.OrderBy(record => record.Holder, StringComparer.CurrentCultureIgnoreCase)
                                .ThenBy(record => record.MapName, StringComparer.CurrentCultureIgnoreCase),
            "duration" => filtered.OrderBy(record => record.DurationValue)
                                  .ThenBy(record => record.MapName, StringComparer.CurrentCultureIgnoreCase),
            _ => filtered
        };

        var matching = filtered.ToArray();
        _matchingRecordCount = matching.Length;
        Records = string.Equals(SelectedRecordPageSize.Key, "all", StringComparison.Ordinal)
            ? matching
            : int.TryParse(SelectedRecordPageSize.Key, out var displayLimit)
                ? matching.Take(displayLimit).ToArray()
                : matching.Take(50).ToArray();

        OnPropertyChanged(nameof(FilteredRecordCount));
        OnPropertyChanged(nameof(DisplayedRecordCount));
        OnPropertyChanged(nameof(TotalRecordCount));
        OnPropertyChanged(nameof(FilterSummary));
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(RecordsEmptyMessage));
    }

    private static void SelectByKey(
        IReadOnlyList<SelectionOption> options,
        string? key,
        Action<SelectionOption> apply,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var option = options.FirstOrDefault(candidate => string.Equals(candidate.Key, key, comparison));
        if (option is not null)
        {
            apply(option);
        }
    }

    private static string FormatSource(LocalSourceMetadata source) =>
        $"Lecture : {DisplayText.ReadStatus(source.ReadStatus)} · " +
        $"Fraîcheur : {DisplayText.Freshness(source.Freshness)} · " +
        $"Âge : {DisplayText.FormatAge(source.Age)} · " +
        $"Provenance : {DisplayText.Provenance(source.Provenance)} · {source.Message}";
}
