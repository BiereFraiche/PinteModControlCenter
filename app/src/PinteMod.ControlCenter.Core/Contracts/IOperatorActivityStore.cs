using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Contracts;

public interface IOperatorActivityStore
{
    IReadOnlyList<LiveEvent> GetSnapshot();

    void RecordRconResult(RconExecutionResult result);

    void RecordPauseResult(CommunityPauseExecutionResult result);

    void RecordServerAdministrationResult(ServerAdministrationExecutionResult result);

    void RecordPlayerAdministrationResult(PlayerAdministrationExecutionResult result);
}
