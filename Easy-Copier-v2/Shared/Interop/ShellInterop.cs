using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace EasyCopier.Shared.Interop;

/// <summary>
/// Represents the native SIZE structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SIZE : IEquatable<SIZE>
{
    /// <summary>The x extent (width).</summary>
    public int cx;

    /// <summary>The y extent (height).</summary>
    public int cy;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is SIZE size && Equals(size);

    /// <inheritdoc />
    public readonly bool Equals(SIZE other) => cx == other.cx && cy == other.cy;

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(cx, cy);

    public static bool operator ==(SIZE left, SIZE right) => left.Equals(right);
    public static bool operator !=(SIZE left, SIZE right) => !left.Equals(right);
}

/// <summary>
/// Flags that specify how the image should be generated for <see cref="IShellItemImageFactory"/>.
/// </summary>
[Flags]
[SuppressMessage("Naming", "CA1712:Do not prefix enum values with type name", Justification = "Matches native Win32 Shell API enumeration SIIGBF.")]
[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "Matches native Win32 Shell API enumeration SIIGBF where SIIGBF_RESIZETOFIT is 0.")]
public enum SIIGBF
{
    SIIGBF_RESIZETOFIT = 0x00,
    SIIGBF_BIGGERSIZEOK = 0x01,
    SIIGBF_MEMORYONLY = 0x02,
    SIIGBF_ICONONLY = 0x04,
    SIIGBF_THUMBNAILONLY = 0x08,
    SIIGBF_INCACHEONLY = 0x10
}

/// <summary>
/// Exposes a method to return either icons or thumbnails for Shell items.
/// </summary>
[ComImport]
[Guid("bcc18b79-ba16-442f-8a90-8c303536fa9f")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(
        [In] SIZE size,
        [In] SIIGBF flags,
        [Out] out IntPtr phbm);
}

/// <summary>
/// Provides interop helper constants and P/Invoke declarations for GDI32.
/// </summary>
public static partial class Gdi32Interop
{
    /// <summary>
    /// Deletes a logical pen, brush, font, bitmap, region, or palette, freeing all system resources associated with the object.
    /// </summary>
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr hObject);
}

