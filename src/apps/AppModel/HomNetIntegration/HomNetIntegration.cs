using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;

namespace AppModel;

[NetDaemonApp]
public class HomNetIntegration
{
    private readonly ILogger _logger;
    private readonly SerialClient _serialClient;

    private readonly ButtonEntity _evCallSwitch;

    public HomNetIntegration(IHaContext ha, IAppConfig<HomNetIntegrationConfig> config, ILogger<HomNetIntegration> logger, ILogger<SerialClient> serialClientLogger)
    {
        //Init SerialClient
        _logger = logger;
        _serialClient =
            new SerialClient(serialClientLogger, config.Value.SerialServerHost, config.Value.SerialServerPort);
        _serialClient.Connect();
        _serialClient.ReceivedEvent += SerialClientOnReceivedEvent;
        _serialClient.DisconnectedEvent += SerialClientOnDisconnectedEvent;

        //Init HA Entities
        _evCallSwitch = new ButtonEntity(ha, "input_button.homnet_evcall");
        _evCallSwitch.StateAllChanges().Subscribe(e => ElevatorCall());
    }

    private void SerialClientOnDisconnectedEvent(object? sender, EventArgs e)
    {
        _logger.LogWarning("SerialClient disconnected. connect retry in 10 seconds.");
        Thread.Sleep(10000);
        _serialClient.Connect();
    }

    private void SerialClientOnReceivedEvent(object? sender, SerialClient.ReceiveEventArgs e)
    {
        
    }

    private void ElevatorCall()
    {
        _logger.LogInformation("Calling elevator.");
        _serialClient.SendPacket("021C410A00D401012903021C410A40D40101E903");
        _serialClient.SendPacket("021C410800D22D03021C410A40D20103E903");
    }
}
