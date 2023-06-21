using BarRaider.SdTools;
using CliWrap;
using CliWrap.Buffered;
using StreamDeck_Tools_Template1;
using System.Collections.Generic;

namespace com.thejoeiaut.bitwarden
{
    internal static class BwCliWrapper
    {

        private static Command _cli;

        internal static Command GetCli()
        {
            return _cli ??= Cli.Wrap("bw");
        }

        internal static void SetCli(Command cli)
        {
            _cli = cli;
        }
    }
}
