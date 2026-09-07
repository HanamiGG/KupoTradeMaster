using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace KupoTradeMaster.UI;

public static class ImGuiIconExtensions
{
    public static readonly Vector2 SmallIcon = new(20, 20);

    /// Draw a small game icon inline. Silently no-ops when iconId is 0 or the texture isn't ready.
    public static void DrawIcon(ITextureProvider provider, ushort iconId, Vector2? size = null)
    {
        if (iconId == 0)
        {
            ImGui.Dummy(size ?? SmallIcon);
            return;
        }
        var tex = provider.GetFromGameIcon(new GameIconLookup(iconId));
        var wrap = tex.GetWrapOrEmpty();
        ImGui.Image(wrap.Handle, size ?? SmallIcon);
    }

    /// Icon + text on the same line, aligned nicely inside a table cell.
    public static void IconAndText(ITextureProvider provider, ushort iconId, string text)
    {
        DrawIcon(provider, iconId);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(text);
    }
}
