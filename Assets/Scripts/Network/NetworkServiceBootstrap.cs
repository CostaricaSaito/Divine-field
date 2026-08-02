using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Lazy initializer for Unity Gaming Services (Core + anonymous Authentication)
/// and the runtime-created NetworkManager (NGO + UnityTransport).
/// No scene / prefab setup is required: everything is built from code on demand.
/// </summary>
public static class NetworkServiceBootstrap
{
    static Task _initTask;
    static GameObject _networkManagerGo;

    /// <summary>Initialize UGS and sign in anonymously (idempotent).</summary>
    public static Task EnsureServicesInitializedAsync()
    {
        if (_initTask == null || _initTask.IsFaulted || _initTask.IsCanceled)
            _initTask = InitializeInternalAsync();
        return _initTask;
    }

    static async Task InitializeInternalAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
#if UNITY_EDITOR
            // Distinct auth profile per editor instance so ParrelSync clones
            // (same machine, same project keys) get separate anonymous accounts.
            var options = new InitializationOptions();
            options.SetProfile($"editor{System.Diagnostics.Process.GetCurrentProcess().Id % 1000}");
            await UnityServices.InitializeAsync(options);
#else
            await UnityServices.InitializeAsync();
#endif
        }

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"[NetworkServiceBootstrap] Signed in. PlayerId={AuthenticationService.Instance.PlayerId}");
    }

    /// <summary>
    /// Get (or create) the persistent NetworkManager configured with UnityTransport.
    /// Scene management is disabled: scene transitions stay on SceneTransitionManager.
    /// </summary>
    public static NetworkManager EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null)
            return NetworkManager.Singleton;

        _networkManagerGo = new GameObject("NetworkManager (runtime)");
        UnityEngine.Object.DontDestroyOnLoad(_networkManagerGo);

        var transport = _networkManagerGo.AddComponent<UnityTransport>();
        var nm = _networkManagerGo.AddComponent<NetworkManager>();
        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = false,
        };
        return nm;
    }

    /// <summary>Shut down the NGO session (keeps the NetworkManager object for reuse).</summary>
    public static void ShutdownNetworkSession()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && (nm.IsClient || nm.IsServer || nm.IsHost))
        {
            nm.Shutdown();
            Debug.Log("[NetworkServiceBootstrap] Network session shut down");
        }
    }
}
