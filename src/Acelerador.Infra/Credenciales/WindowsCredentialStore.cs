using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Acelerador.Core.Puertos;
using Acelerador.Sap;

namespace Acelerador.Infra.Credenciales;

/// <summary>
/// Credenciales en el Administrador de credenciales de Windows (ADR-04, HU-04). Windows cifra el
/// secreto con DPAPI ligado al usuario: otro usuario del equipo no puede leerlo y nunca queda en
/// SQLite, archivos de configuración ni registros (RNF-Seg-02).
/// Se ve en «Administrador de credenciales → Credenciales de Windows» con el prefijo CMConsulting.Acelerador/.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int TamanoMaximoBytes = 5 * 512;

    public bool EsPersistente => true;

    public void Guardar(string referencia, string usuario, CredencialSap credencial)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(credencial.Revelar());
        if (bytes.Length > TamanoMaximoBytes)
        {
            throw new ArgumentException("La credencial supera el tamaño que admite el Administrador de credenciales.", nameof(credencial));
        }

        var blob = Marshal.AllocHGlobal(bytes.Length);
        var target = Marshal.StringToHGlobalUni(referencia);
        var user = Marshal.StringToHGlobalUni(usuario);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                UserName = user,
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
            };
            if (!CredWrite(ref cred, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo guardar la credencial en el Administrador de credenciales.");
            }
        }
        finally
        {
            Array.Clear(bytes);
            ZeroMemory(blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            Marshal.FreeHGlobal(target);
            Marshal.FreeHGlobal(user);
        }
    }

    public CredencialSap? Leer(string referencia)
    {
        if (!CredRead(referencia, CredTypeGeneric, 0, out var puntero))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error, "No se pudo leer la credencial del Administrador de credenciales.");
        }

        try
        {
            var cred = Marshal.PtrToStructure<Credential>(puntero);
            if (cred.CredentialBlobSize == 0)
            {
                return null;
            }

            var valor = Marshal.PtrToStringUni(cred.CredentialBlob, cred.CredentialBlobSize / 2);
            return new CredencialSap(valor);
        }
        finally
        {
            CredFree(puntero);
        }
    }

    public void Eliminar(string referencia)
    {
        if (!CredDelete(referencia, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error, "No se pudo eliminar la credencial del Administrador de credenciales.");
            }
        }
    }

    private static void ZeroMemory(IntPtr destino, int longitud)
    {
        for (var i = 0; i < longitud; i++)
        {
            Marshal.WriteByte(destino, i, 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);
}
