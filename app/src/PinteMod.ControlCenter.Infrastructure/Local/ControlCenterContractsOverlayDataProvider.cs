using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ControlCenterContractsOverlayDataProvider(
    IControlCenterDataProvider baselineProvider,
    IControlCenterContractReader contractReader) : IControlCenterDataProvider
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var baseline = await baselineProvider.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var sessionReady = baseline.DataContext.Mode == ControlCenterDataMode.HybridLocal &&
                           baseline.DataContext.SessionSource.ReadStatus == LocalReadStatus.Success &&
                           baseline.DataContext.SessionSource.Provenance == DataProvenance.LocalFile &&
                           baseline.Server.SessionProvenance == DataProvenance.LocalFile;
        var contracts = await contractReader.ReadAsync(
            sessionReady ? baseline.Server.SessionId : null,
            sessionReady ? baseline.Server.MapCode : null,
            cancellationToken).ConfigureAwait(false);

        return baseline with
        {
            DataContext = baseline.DataContext with
            {
                SimulatedAreas = sessionReady
                    ? ["Changement de carte", "Événements génériques", "Définition du mot de passe joueur"]
                    : baseline.DataContext.SimulatedAreas
            },
            LocalObservation = baseline.LocalObservation with
            {
                ControlCenterContracts = contracts
            }
        };
    }
}
