// The `init` accessor needs this type to exist; it ships in newer target frameworks but not in
// netstandard2.0, which this analyzer targets so it can run on older Roslyn/SDK versions. The
// compiler only checks for the type's presence, so an empty polyfill is enough.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
