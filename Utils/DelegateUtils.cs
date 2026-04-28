using MonoMod.Utils;

namespace FancyLighting.Utils;

public static class DelegateUtils
{
    public static bool IsDelegate<T>(this object obj, out T function)
        where T : Delegate
    {
        try
        {
            if (obj is Delegate del && del.CastDelegate<T>() is { } fun)
            {
                function = fun;
                return true;
            }
        }
        catch (Exception)
        {
            // incompatible type
        }

        function = null;
        return false;
    }
}
