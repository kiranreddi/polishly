using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;

namespace Polishly.WindowsIntegration.Security;

public class CredentialManager : ICredentialStore
{
    private const string TargetPrefix = "Polishly_ApiKey_";
    private readonly Dictionary<string, string> _inMemoryStore = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveApiKeyAsync(string providerId, string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be null or empty.", nameof(providerId));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be empty.", nameof(apiKey));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var targetName = TargetPrefix + providerId;
            var bytes = Encoding.Unicode.GetBytes(apiKey);
            var blobPtr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, blobPtr, bytes.Length);

                var credential = new Native.Win32Native.CREDENTIAL
                {
                    Type = 1, // CRED_TYPE_GENERIC
                    TargetName = targetName,
                    CredentialBlobSize = (uint)bytes.Length,
                    CredentialBlob = blobPtr,
                    Persist = 2, // CRED_PERSIST_LOCAL_MACHINE
                    UserName = providerId,
                    Comment = "Polishly AI Provider Key"
                };

                if (!Native.Win32Native.CredWrite(ref credential, 0))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(errorCode, $"Failed to write credential to Windows Credential Manager for provider '{providerId}'. Error code: {errorCode}");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                Marshal.FreeHGlobal(blobPtr);
            }
        }

        _inMemoryStore[providerId] = apiKey;
        return Task.CompletedTask;
    }

    public Task<string?> GetApiKeyAsync(string providerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be null or empty.", nameof(providerId));
        }

        if (_inMemoryStore.TryGetValue(providerId, out var key))
        {
            return Task.FromResult<string?>(key);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var targetName = TargetPrefix + providerId;
            if (Native.Win32Native.CredRead(targetName, 1, 0, out var credPtr))
            {
                try
                {
                    var credential = Marshal.PtrToStructure<Native.Win32Native.CREDENTIAL>(credPtr);
                    if (credential.CredentialBlob != IntPtr.Zero && credential.CredentialBlobSize > 0)
                    {
                        var bytes = new byte[credential.CredentialBlobSize];
                        Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                        string retrievedKey = Encoding.Unicode.GetString(bytes);
                        CryptographicOperations.ZeroMemory(bytes);
                        _inMemoryStore[providerId] = retrievedKey;
                        return Task.FromResult<string?>(retrievedKey);
                    }
                }
                finally
                {
                    Native.Win32Native.CredFree(credPtr);
                }
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                const int ErrorNotFound = 1168;
                if (error != ErrorNotFound)
                    throw new System.ComponentModel.Win32Exception(
                        error, "Windows Credential Manager read failed.");
            }
        }

        return Task.FromResult<string?>(null);
    }

    public Task DeleteApiKeyAsync(string providerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider ID cannot be null or empty.", nameof(providerId));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var targetName = TargetPrefix + providerId;
            if (!Native.Win32Native.CredDelete(targetName, 1, 0))
            {
                int error = Marshal.GetLastWin32Error();
                const int ErrorNotFound = 1168;
                if (error != ErrorNotFound)
                    throw new System.ComponentModel.Win32Exception(
                        error, "Windows Credential Manager delete failed.");
            }
        }

        _inMemoryStore.Remove(providerId);
        return Task.CompletedTask;
    }
}
