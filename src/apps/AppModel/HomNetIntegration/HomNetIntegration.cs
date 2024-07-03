using System.Security.Cryptography.X509Certificates;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;

namespace AppModel;

[NetDaemonApp]
public class HomNetIntegration
{
    private readonly ILogger _logger;
    private readonly ButtonEntity _evCallSwitch;

    public HomNetIntegration(IHaContext ha, IAppConfig<HomNetIntegrationConfig> config, ITriggerManager trigger, ILogger<HomNetIntegration> logger)
    {
        _logger = logger;
        _evCallSwitch = new ButtonEntity(ha, "input_button.homnet_evcall");

        _evCallSwitch.StateAllChanges().Subscribe(e => ElevatorCall());
    }

    private void ElevatorCall()
    {
        _logger.LogInformation("Calling elevator.");
    }
}
