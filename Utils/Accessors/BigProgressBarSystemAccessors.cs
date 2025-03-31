using System.Runtime.CompilerServices;
using Terraria.GameContent.UI.BigProgressBar;

namespace FancyLighting.Utils.Accessors;

internal static class BigProgressBarSystemAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_currentBar")]
    public static extern ref IBigProgressBar _currentBar(BigProgressBarSystem obj);
}
