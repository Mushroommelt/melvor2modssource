using System.Runtime.InteropServices;

namespace MelvorModLoaderLauncher;

internal static partial class NativeMethods
{
    public const uint MB_OK = 0x0, MB_YESNO = 0x4, MB_ICONERROR = 0x10, MB_ICONQUESTION = 0x20, MB_ICONINFORMATION = 0x40;
    public const int IDYES = 6;

    public const int TDF_ALLOW_DIALOG_CANCELLATION = 0x8, TDF_USE_COMMAND_LINKS = 0x10;
    // Resource id the .NET SDK gives the ApplicationIcon in the exe.
    public const nint APPLICATION_ICON_ID = 32512;

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CreateHardLink(string fileName, string existingFileName, nint securityAttributes);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBox(nint owner, string text, string caption, uint type);

    // Needs Common Controls 6 (see app.manifest).
    [LibraryImport("comctl32.dll")]
    public static unsafe partial int TaskDialogIndirect(TaskDialogConfig* config, int* button, int* radioButton, int* verificationChecked);

    // Both structs are declared with 1-byte packing in commctrl.h. Strings are UTF-16 pointers.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TaskDialogButton
    {
        public int Id;
        public nint Text;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct TaskDialogConfig
    {
        public uint Size;
        public nint Parent;
        public nint Instance;
        public int Flags;
        public int CommonButtons;
        public nint WindowTitle;
        public nint MainIcon;
        public nint MainInstruction;
        public nint Content;
        public uint ButtonCount;
        public TaskDialogButton* Buttons;
        public int DefaultButton;
        public uint RadioButtonCount;
        public nint RadioButtons;
        public int DefaultRadioButton;
        public nint VerificationText;
        public nint ExpandedInformation;
        public nint ExpandedControlText;
        public nint CollapsedControlText;
        public nint FooterIcon;
        public nint Footer;
        public nint Callback;
        public nint CallbackData;
        public uint Width;
    }
}
