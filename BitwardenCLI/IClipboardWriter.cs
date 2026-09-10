using System.Threading.Tasks;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Puts text on the system clipboard. Exists so the actions can be tested without
    /// touching the real clipboard.
    /// </summary>
    internal interface IClipboardWriter
    {
        Task SetText(string text);
    }
}
