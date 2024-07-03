using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HomeAssistantGenerated;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NetDaemon.HassModel.Integration;
using YamlDotNet.Core.Tokens;

namespace cloudinteractive_homnetbridge_serial.apps.AppModel.HomNetIntegration.Services
{
    public class SerialSendEventArgs : EventArgs
    {
        public string Content;
        public SerialSendEventArgs(string content) => Content = content;
    }

    public static class LightManagementService
    {
        private static ILogger _logger;
        private static readonly Dictionary<int, InputBooleanEntity> _lightStatusEntities = new Dictionary<int, InputBooleanEntity>();
        private static bool[] _lightStatus = { false, false, false };
        private static bool _isUpdated = false;
        private static object _updateLock = new object();
        private static ConcurrentQueue<LightCallbackData> _requestQueue = new ConcurrentQueue<LightCallbackData>();
        public record LightCallbackData(int idx, bool state);

        public static EventHandler<SerialSendEventArgs> SerialSendEvent;

        private static readonly Dictionary<char, bool[]> _codeToLightStatus = new Dictionary<char, bool[]>()
        {
            {'0', new[] { false, false, false}},
            {'2', new[] { false, false, true }},
            {'4', new[] { false, true, false }},
            {'6', new[] { false, true, true }},
            {'8', new[] { true, false, false }},
            {'A', new[] { true, false, true }},
            {'C', new[] { true, true, false }},
            {'E', new[] { true, true, true }}
        };

        private static readonly Dictionary<char, string[]> _lightStatusToCode = new Dictionary<char, string[]>()
        {
            {'0', new[] { "4C", "000C" }},
            {'2', new[] { "2C", "20EC" }},
            {'4', new[] { "0C", "40CC" }},
            {'6', new[] { "EC", "60AC" }},
            {'8', new[] { "CC", "808C" }},
            {'A', new[] { "AC", "A06C" }},
            {'C', new[] { "8C", "C04C" }},
            {'E', new[] { "6C", "E02C" }}
        };

        public static void Init(IHaContext context, ILogger logger)
        {
            _logger = logger;
            _logger.LogInformation("LightManagementService Init...");
            
            context.RegisterServiceCallBack<LightCallbackData>("callback_light", LightEntityCallbackEvent);

            _lightStatusEntities.Add(0, new InputBooleanEntity(context, "input_boolean.homnet_light_0_state"));
            _lightStatusEntities.Add(1, new InputBooleanEntity(context, "input_boolean.homnet_light_1_state"));
            _lightStatusEntities.Add(2, new InputBooleanEntity(context, "input_boolean.homnet_light_2_state"));

            Task.Run(RequestTask);
        }

        private static async void RequestTask()
        {
            while (true)
            {
                LightCallbackData x;
                if (_requestQueue.TryDequeue(out x))
                {
                    while(!_isUpdated) Thread.Sleep(100);

                    lock (_updateLock)
                    {
                        _isUpdated = false;
                        _lightStatus[x.idx] = x.state;
                        char key = _codeToLightStatus.FirstOrDefault(y => y.Value[0] == _lightStatus[0] && y.Value[1] == _lightStatus[1] && y.Value[2] == _lightStatus[2]).Key;
                        var codes = _lightStatusToCode[key];

                        _logger.LogInformation($"Light status change request => {x.idx}:{x.state} states:({_lightStatus[0]}, {_lightStatus[1]}, {_lightStatus[2]})");

                        if (SerialSendEvent is not null)
                            SerialSendEvent(null,
                                new SerialSendEventArgs(
                                    $"0219010A00B003{key}0{codes[0]}030219010B40B00003{codes[1]}03"));
                    }
                }
                Thread.Sleep(100);
            }
        }

        private static void LightEntityCallbackEvent(LightCallbackData e)
        {
            _logger.LogInformation($"LightEntityCallback : {e.idx} => {e.state}");

            if (e.state) _lightStatusEntities[e.idx].TurnOn();
            else _lightStatusEntities[e.idx].TurnOff();

            _requestQueue.Enqueue(e);
        }

        public static void LightStatusUpdate(string content)
        {
            lock (_updateLock)
            {
                if (!_codeToLightStatus.TryGetValue(content[34], out var value))
                {
                    _logger.LogWarning("Invalid light status code :" + content[34]);
                    return;
                }

                if (!_isUpdated || !CompareBoolArray(value, _lightStatus))
                {
                    _logger.LogInformation($"Light status update => {content[34]} ({value[0]}, {value[1]}, {value[2]})");

                    _lightStatus = (bool[])value.Clone();

                    for (int i = 0; i < 3; i++)
                    {
                        if (_lightStatus[i]) _lightStatusEntities[i].TurnOn();
                        else _lightStatusEntities[i].TurnOff();
                    }

                    _isUpdated = true;
                }
            }
        }

        public static bool CompareBoolArray(bool[] lhs, bool[] rhs)
        {
            if (lhs.Length != rhs.Length) return false;

            for(int i =0; i < lhs.Length; i++) if(lhs[i] != rhs[i]) return false;
            return true;
        }
    }
}
