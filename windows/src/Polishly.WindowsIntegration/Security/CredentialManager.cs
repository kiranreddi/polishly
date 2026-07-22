using System.Runtime.InteropServices;
using System.Text;

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

        if (apiKey == null)
        {
            throw new ArgumentNullException(nameof(apiKey));
        }

        _inMemoryStore[providerId] = apiKey;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
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

                    Native.Win32Native.CredWrite(ref credential, 0);
                }
                finally
                {
                    Marshal.FreeHGlobal(blobPtr);
                }
            }
            catch
            {
                // Fall back to safely isolated in-memory storage if Win32 API fails
            }
        }

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
            try
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
                            _inMemoryStore[providerId] = retrievedKey;
                            return Task.FromResult<string?>(retrievedKey);
                        }
                    }
                    finally
                    {
                        Native.Win32Native.CredFree(credPtr);
                    }
                }
            }
            catch
            {
                // Win32 lookup fallback
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

        _inMemoryStore.Remove(providerId);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var targetName = TargetPrefix + providerId;
                Native.Win32Native.CredDelete(targetName, 1, 0);
            }
            catch
            {
                // Win32 delete fallback
            }
        }

        return Task.CompletedTask;
    }
}

