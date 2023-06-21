using System.Threading.Tasks;
using BarRaider.SdTools;

namespace com.thejoeiaut.bitwarden
{
    [PluginActionId("com.thejoeiaut.bitwardenlock")]
    public class Lock : PluginBase
    {
        #region Private Members


        #endregion

        public Lock(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed - Lock");
            LockVault().GetAwaiter().GetResult();
        }
        
        public override void KeyReleased(KeyPayload payload)
        {
        }

        public override void OnTick()
        {
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {

        }

        private static async Task LockVault()
        {
            await BwCliWrapper.GetCli().WithArguments(new[] {"lock"}).ExecuteAsync();
        }


        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
        }

        #region Private Methods



        #endregion
    }
}