using System;
using System.Threading.Tasks;
using BarRaider.SdTools;

namespace BitwardenStreamdeckPlugin
{
    [PluginActionId("com.thejoeiaut.bitwardenlock")]
    public class Lock : KeypadBase
    {
        #region Private Members

        private readonly IBwCli cli;

        #endregion

        public Lock(ISDConnection connection, InitialPayload payload)
            : this(connection, payload, BwCli.Shared)
        {
        }

        internal Lock(ISDConnection connection, InitialPayload payload, IBwCli cli) : base(connection, payload)
        {
            this.cli = cli;
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

        internal async Task LockVault()
        {
            try
            {
                await cli.Run("lock");
                await Connection.ShowOk();
            }
            catch (Exception e)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, e.Message);
                await Connection.ShowAlert();
            }
        }


        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
        }

        #region Private Methods



        #endregion
    }
}
