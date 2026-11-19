using System.Runtime.InteropServices;

namespace MelvorModLoaderLauncher;

/// <summary>A dialog with the launcher's icon and custom choices (Windows task dialog with command links).</summary>
internal static class TaskDialog
{
    /// <summary>
    /// Shows the dialog and returns the index of the chosen button, or null if it was closed. A button's text may
    /// contain a second line ("\n"), shown smaller as its description.
    /// </summary>
    public static unsafe int? Show(string title, string instruction, string content, params string[] buttons)
    {
        var strings = new List<nint>();
        nint Native(string text)
        {
            var pointer = Marshal.StringToHGlobalUni(text);
            strings.Add(pointer);
            return pointer;
        }

        try
        {
            // Ids start after the common ids (IDCANCEL, ...).
            const int firstId = 100;
            var nativeButtons = stackalloc NativeMethods.TaskDialogButton[buttons.Length];
            for (var i = 0; i < buttons.Length; i++)
            {
                nativeButtons[i] = new NativeMethods.TaskDialogButton { Id = firstId + i, Text = Native(buttons[i]) };
            }
            var config = new NativeMethods.TaskDialogConfig
            {
                Size = (uint)sizeof(NativeMethods.TaskDialogConfig),
                Flags = NativeMethods.TDF_USE_COMMAND_LINKS | NativeMethods.TDF_ALLOW_DIALOG_CANCELLATION,
                WindowTitle = Native(title),
                // An icon resource id: loaded from Instance, at the size the dialog needs.
                Instance = NativeMethods.GetModuleHandle(null),
                MainIcon = NativeMethods.APPLICATION_ICON_ID,
                MainInstruction = Native(instruction),
                Content = Native(content),
                ButtonCount = (uint)buttons.Length,
                Buttons = nativeButtons,
                DefaultButton = firstId,
            };
            int chosen;
            Marshal.ThrowExceptionForHR(NativeMethods.TaskDialogIndirect(&config, &chosen, null, null));
            return chosen >= firstId ? chosen - firstId : null;
        }
        finally
        {
            strings.ForEach(Marshal.FreeHGlobal);
        }
    }
}
