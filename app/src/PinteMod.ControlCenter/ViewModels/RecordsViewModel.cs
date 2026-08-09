using System.Collections.ObjectModel;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.ViewModels;

public sealed class RecordsViewModel(IControlCenterSnapshotStore snapshotStore)
    : PageViewModel("Records", "Profils Ranks, records de manches et Easter Egg Records")
{
    private ServerState? _server;
    private RankRecordsSnapshot _rankRecords = RankRecordsSnapshot.Simulation;
    private EasterEggRecordsSnapshot _easterEggRecords = EasterEggRecordsSnapshot.Simulation;
    private bool _isHybridLocal;

    public ObservableCollection<RankProfileItemViewModel> RankProfiles { get; } = [];

    public ObservableCollection<RecordItemViewModel> Records { get; } = [];

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

    public int StandardRecordCount => Records.Count(record => !record.IsEasterEgg);

    public int EasterEggRecordCount => Records.Count(record => record.IsEasterEgg);

    public bool HasRankProfiles => RankProfiles.Count > 0;

    public bool HasRecords => Records.Count > 0;

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

    public string RecordsEmptyMessage => _isHybridLocal
        ? "Aucun record local officiel valide n’est disponible."
        : "Le snapshot simulé ne contient aucun record.";

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = await snapshotStore.GetSnapshotAsync(cancellationToken);
        _isHybridLocal = snapshot.DataContext.Mode == ControlCenterDataMode.HybridLocal;
        _rankRecords = snapshot.RankRecords;
        _easterEggRecords = snapshot.EasterEggRecords;
        Server = snapshot.Server;

        RankProfiles.Clear();
        foreach (var profile in snapshot.RankRecords.Profiles)
        {
            RankProfiles.Add(new RankProfileItemViewModel(profile));
        }

        Records.Clear();
        foreach (var record in snapshot.Records)
        {
            Records.Add(new RecordItemViewModel(record));
        }

        OnPropertyChanged(nameof(CurrentMapProfile));
        OnPropertyChanged(nameof(RankedStatusCaption));
        OnPropertyChanged(nameof(RankProfileCount));
        OnPropertyChanged(nameof(StandardRecordCount));
        OnPropertyChanged(nameof(EasterEggRecordCount));
        OnPropertyChanged(nameof(HasRankProfiles));
        OnPropertyChanged(nameof(HasRecords));
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
        OnPropertyChanged(nameof(RecordsEmptyMessage));
    }

    private static string FormatSource(LocalSourceMetadata source) =>
        $"Lecture : {DisplayText.ReadStatus(source.ReadStatus)} · " +
        $"Fraîcheur : {DisplayText.Freshness(source.Freshness)} · " +
        $"Âge : {DisplayText.FormatAge(source.Age)} · " +
        $"Provenance : {DisplayText.Provenance(source.Provenance)} · {source.Message}";
}
