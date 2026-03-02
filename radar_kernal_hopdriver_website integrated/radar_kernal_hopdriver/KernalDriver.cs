using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public class KernelDriver : IDisposable
{
    private readonly SafeFileHandle driverHandle;

    // IOCTL codes that MUST match Driver.c
    private const uint FILE_DEVICE_UNKNOWN = 0x00000022;
    private const uint METHOD_BUFFERED = 0;
    private const uint FILE_SPECIAL_ACCESS = 0;

    private static uint CTL_CODE(uint deviceType, uint function, uint method, uint access)
    {
        return ((deviceType << 16) | (access << 14) | (function << 2) | method);
    }

    private static readonly uint IOCTL_READ = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x776, METHOD_BUFFERED, FILE_SPECIAL_ACCESS);
    private static readonly uint IOCTL_WRITE = CTL_CODE(FILE_DEVICE_UNKNOWN, 0x777, METHOD_BUFFERED, FILE_SPECIAL_ACCESS);

    // =========================================================================================
    //  FIX: The signature of CreateFile is changed to accept numbers (uint) instead of enums.
    // =========================================================================================
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
    // =========================================================================================

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct Info_t
    {
        public ulong TargetPid;
        public ulong TargetAddress;
        public ulong BufferAddress;
        public ulong Size;
        public ulong ReturnSize;
    }

    public KernelDriver(string symbolicLink)
    {
        string driverPath = $"\\\\.\\{symbolicLink}";

        // =========================================================================================
        //  FIX: We are now using the raw numbers directly. This bypasses the compiler error.
        // =========================================================================================
        uint GENERIC_READ = 0x80000000;
        uint GENERIC_WRITE = 0x40000000;
        uint OPEN_EXISTING = 3; // This is the value of FileMode.OpenExisting

        driverHandle = CreateFile(
            driverPath,
            GENERIC_READ | GENERIC_WRITE,
            0, // FileShare.None
            IntPtr.Zero,
            OPEN_EXISTING, // Using the number 3 directly
            0x80, // FileAttributes.Normal
            IntPtr.Zero);
        // =========================================================================================

        if (driverHandle.IsInvalid)
        {
            throw new Exception($"Failed to connect to driver. Win32 Error: {Marshal.GetLastWin32Error()}");
        }
    }

    public T ReadMemory<T>(uint processId, ulong address) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        IntPtr bufferPtr = Marshal.AllocHGlobal(size);
        try
        {
            var request = new Info_t { TargetPid = processId, TargetAddress = address, BufferAddress = (ulong)bufferPtr, Size = (ulong)size };
            SendIoControl(IOCTL_READ, ref request);
            return Marshal.PtrToStructure<T>(bufferPtr);
        }
        finally { Marshal.FreeHGlobal(bufferPtr); }
    }

    public byte[] ReadMemory(uint processId, ulong address, uint size)
    {
        IntPtr bufferPtr = Marshal.AllocHGlobal((int)size);
        try
        {
            var request = new Info_t { TargetPid = processId, TargetAddress = address, BufferAddress = (ulong)bufferPtr, Size = size };
            SendIoControl(IOCTL_READ, ref request);
            byte[] buffer = new byte[size];
            Marshal.Copy(bufferPtr, buffer, 0, (int)size);
            return buffer;
        }
        finally { Marshal.FreeHGlobal(bufferPtr); }
    }

    public void WriteMemory<T>(uint processId, ulong address, T value) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        IntPtr bufferPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, bufferPtr, false);
            var request = new Info_t { TargetPid = processId, TargetAddress = address, BufferAddress = (ulong)bufferPtr, Size = (ulong)size };
            SendIoControl(IOCTL_WRITE, ref request);
        }
        finally { Marshal.FreeHGlobal(bufferPtr); }
    }

    private void SendIoControl(uint ioctl, ref Info_t requestData)
    {
        int requestSize = Marshal.SizeOf(typeof(Info_t));
        IntPtr requestPtr = Marshal.AllocHGlobal(requestSize);
        try
        {
            Marshal.StructureToPtr(requestData, requestPtr, false);
            bool success = DeviceIoControl(driverHandle, ioctl, requestPtr, (uint)requestSize, IntPtr.Zero, 0, out _, IntPtr.Zero);
            if (!success) { throw new Exception($"DeviceIoControl failed with Win32 error: {Marshal.GetLastWin32Error()}"); }
        }
        finally { Marshal.FreeHGlobal(requestPtr); }
    }

    public void Dispose() => driverHandle?.Dispose();
}