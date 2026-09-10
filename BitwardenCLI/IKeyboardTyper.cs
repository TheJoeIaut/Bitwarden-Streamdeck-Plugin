using System.Threading.Tasks;

namespace BitwardenStreamdeckPlugin
{
    /// <summary>
    /// Types text into whatever window currently has focus. Exists so the actions can be
    /// tested without actually driving the keyboard.
    /// </summary>
    internal interface IKeyboardTyper
    {
        Task TypeText(string text);

        Task PressTab();
    }
}
