using UnityEngine;

namespace Vander.Logger
{
    public class LoggerBootstrapper : MonoBehaviour
    {
        [SerializeField] private LoggerSettings settings;

        private void Awake()
        {
            Logger.Configure(settings);
            Logger.SetAllowedSources(new[]
            {
                "StreamingAssetsToPersistentInstaller",
                // "GameBootstrapper",
                // "GeoJsonLoader", 
                // "CustomGameObjects", 
                // "NetworkColorMapper", 
                // "BootstrapPing",
                // "FeatureRenderParams",
                // "DefaultGeoJsonFeatureParser",
                // "LegacyGeoJsonFeatureRenderer",
                // "MeshGeoJsonFeatureRenderer",
                // "OfficialFeature",
                // "SelectionNameOverlay",
                // "Picker",
                // "SelectionNameOverlay",
                // "SelectionController",
                // "ARCameraPoseDebug",
                // "ARSessionStateDebug",
                // "ARSessionStateVerboseDebug",
                "GpsWorldAnchor",
                "GpsLabelController",
                // "NetworkStyleResolver",
                // "MapCameraController",
            });
            Logger.SetEnabled(true);
            Logger.SetMinLevel(LogLevel.Debug);

            // exemple de log
            Logger.Info(LogCategory.General, "Logger configured.");
            if (Logger.FileLoggingEnabled)
                Logger.Info(LogCategory.General, $"LogFilePath = {Logger.LogFilePath}");
        }

        private void OnApplicationQuit()
        {
            Logger.Shutdown();
        }
    }
}