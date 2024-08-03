using System;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;

namespace Uno.Windows;

public unsafe class UnoInterface: Window, IDisposable
{
    //  From: https://git.anna.lgbt/anna/XivCommon/src/branch/main/XivCommon/Functions/Chat.cs
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9")]
    private readonly delegate* unmanaged<UIModule*, Utf8String*, nint, byte, void> processChatBox = null!;
    
    
    private Plugin plugin;
    private int index = 0;
    
    public UnoInterface(Plugin plugin) : base("Uno###001", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;

        Services.GameInteropProvider.InitializeFromAttributes(this);
        
    }
    
    //  From: https://github.com/Infiziert90/ChatTwo/blob/main/ChatTwo/GameFunctions/ChatBox.cs
    public void SendMsgUnsafe(byte[] message)
    {
        if (processChatBox == null)
            throw new InvalidOperationException("Could not find signature for chat sending");
        
        var mes = Utf8String.FromSequence(message);
        processChatBox(UIModule.Instance(), mes, IntPtr.Zero, 0);
        mes->Dtor(true);
    }
    public unsafe void SendMsg(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        
        if (index > 0)
        {
            return;
        }
        
        if (bytes.Length == 0)
            throw new ArgumentException("message is empty", nameof(message));

        if (bytes.Length > 500)
            throw new ArgumentException("message is longer than 500 bytes", nameof(message));

        if (message.Length != SanitiseText(message).Length)
            throw new ArgumentException("message contained invalid characters", nameof(message));
        
        SendMsgUnsafe(bytes);
    }
    private string SanitiseText(string text)
    {
        var uText = Utf8String.FromString(text);

        uText->SanitizeString( 0x27F, (Utf8String*)nint.Zero);
        var sanitised = uText->ToString();
        uText->Dtor(true);

        return sanitised;
    }
    

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("test"))
        {
                
            SendMsg("B-4");
            index++;
        }
            
        if (ImGui.Button("Reset"))
        {
            index = 0;
        }
    }
    
}

