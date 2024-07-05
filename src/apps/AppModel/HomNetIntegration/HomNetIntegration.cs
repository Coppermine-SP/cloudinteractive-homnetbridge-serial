using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using cloudinteractive_homnetbridge_serial.apps.AppModel.HomNetIntegration.Services;
using HomeAssistantGenerated;
using Microsoft.Extensions.DependencyInjection;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Integration;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace cloudinteractive_homnetbridge_serial.apps.AppModel.HomNetIntegration;

[NetDaemonApp]
public class HomNetIntegration
{
    private readonly ILogger _logger;
    private readonly SerialClient _serialClient;
    private readonly ButtonEntity _evCallSwitch;

    public HomNetIntegration(IHaContext ha, IAppConfig<HomNetIntegrationConfig> config, ILogger<HomNetIntegration> logger, IServiceProvider provider)
    {
        _logger = logger;
        _logger.LogInformation("cloudinteractive-homnetbridge-serial 2 Init..");

        //Init Services
        LightManagementService.Init(ha, provider.GetService<ILoggerFactory>().CreateLogger("LightManagementService"));
        LightManagementService.SerialSendEvent += SerialSendEvent;
        ha.RegisterServiceCallBack<LightCallbackParameter>("callback_light", LightEntityCallbackEvent);

        //Init SerialClient
        _serialClient =
            new SerialClient(provider.GetService<ILogger<SerialClient>>(), config.Value.SerialServerHost, config.Value.SerialServerPort);
        _serialClient.Connect();
        _serialClient.ReceivedEvent += SerialClientOnReceivedEvent;
        _serialClient.DisconnectedEvent += SerialClientOnDisconnectedEvent;

        //Init HA Entities
        _evCallSwitch = new ButtonEntity(ha, "input_button.homnet_evcall");
        _evCallSwitch.StateAllChanges().Subscribe(e => ElevatorCall());
    }

    ~HomNetIntegration()
    {
        _serialClient.ReceivedEvent -= SerialClientOnReceivedEvent;
        _serialClient.DisconnectedEvent -= SerialClientOnDisconnectedEvent;
        _serialClient.Dispose();

        LightManagementService.SerialSendEvent -= SerialSendEvent;
    }

    private void SerialSendEvent(object? sender, SerialSendEventArgs e)
    {
        _serialClient.SendPacket(e.Content);
    }

    private void SerialClientOnDisconnectedEvent(object? sender, EventArgs e)
    {
        _logger.LogWarning("SerialClient disconnected. connect retry in 10 seconds.");
        Thread.Sleep(10000);
        _serialClient.Connect();
    }

    private void SerialClientOnReceivedEvent(object? sender, SerialClient.ReceiveEventArgs e)
    {
        if (e.Content.Substring(0, 2) != "02" || e.Content.Substring(e.Content.Length - 2, 2) != "03")
        {
            _logger.LogWarning("Packet header check failed: " + e.Content);
            return;
        }

        if(e.Content.Length > 34 && e.Content.Substring(0, 34) == "0219010800C23D030219010D40C2000003") LightManagementService.LightStatusUpdate(e.Content);
    }

    public record LightCallbackParameter(int idx, JsonElement state);
    private void LightEntityCallbackEvent(LightCallbackParameter e)
    {
        bool x;
        try
        {
            x = e.state.GetBoolean();
        }
        catch
        {
            _logger.LogWarning("Invalid LightCallbackData paramater.");
            return;
        }

        _logger.LogInformation($"LightEntityCallback : {e.idx} => {x}");
        LightManagementService.ChangeLightStatus(new LightManagementService.LightCallbackData(e.idx, x));
    }

    private void ElevatorCall()
    {
        _logger.LogInformation("Calling elevator.");
        _serialClient.SendPacket("021C410A00D401012903021C410A40D40101E903");
        _serialClient.SendPacket("021C410800D22D03021C410A40D20103E903");
    }
}
